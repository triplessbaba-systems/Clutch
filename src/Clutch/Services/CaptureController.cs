using System.Diagnostics;
using System.Windows.Threading;
using Clutch.Core;
using Clutch.Core.Audio;
using Clutch.Core.Capture;
using Clutch.Core.Native;
using Clutch.Core.Settings;
using Clutch.ViewModels;

namespace Clutch.Services;

public enum ToastKind
{
    Info,
    Success,
    Error
}

/// <summary>
/// ImagePath verilirse bildirimde kaydedilen klibin küçük resmi gösterilir; OpenPath verilirse
/// bildirime tıklayınca o dosya açılır.
/// </summary>
public sealed record ToastMessage(string Title, string? Detail, ToastKind Kind, string? ImagePath = null, string? OpenPath = null);

/// <summary>
/// Uygulamanın beyni. Kısayollar, tepsi menüsü ve overlay butonlarının hepsi buraya gelir.
/// Tüm genel metotlar UI iş parçacığından çağrılır.
/// </summary>
public sealed class CaptureController : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly ScreenRecorder _recorder = new();
    private readonly ReplayBuffer _replay = new();
    private readonly SemaphoreSlim _recordGate = new(1, 1);
    private readonly SemaphoreSlim _replayGate = new(1, 1);
    private readonly DispatcherTimer _recordingTimer;
    private readonly Dispatcher _dispatcher;

    private string? _ffmpegPath;
    private EncoderChoice? _encoder;
    private DateTime _recordingStartedAt;

    private bool _isReady;
    private bool _isReplayActive;
    private bool _isRecording;
    private string _recordingElapsed = "00:00:00";
    private string _statusText = "Hazırlanıyor…";
    private string _encoderShortName = "—";
    private double _liveFps;
    private long _droppedFrames;
    private long _totalFrames;
    private long _outputBytes;

    public event Action<ToastMessage>? ToastRequested;
    public event Action? StateChanged;

    /// <summary>Yeni bir klip ya da ekran görüntüsü diske yazıldığında (galeri yenilensin diye).</summary>
    public event Action<string>? ClipSaved;

    public CaptureController(AppSettings settings, SettingsStore store)
    {
        Settings = settings;
        _store = store;
        _dispatcher = Dispatcher.CurrentDispatcher;

        _recordingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _recordingTimer.Tick += (_, _) => RecordingElapsed = FormatElapsed(DateTime.Now - _recordingStartedAt);

        _replay.Failed += reason => _dispatcher.BeginInvoke(() =>
        {
            IsReplayActive = false;
            Toast("Anında tekrar durdu", reason, ToastKind.Error);
        });

        _recorder.Failed += reason => _dispatcher.BeginInvoke(() =>
        {
            SetRecording(false);
            Toast("Kayıt durdu", reason, ToastKind.Error);
        });

        // Durum çubuğundaki FPS / kare kaybı: kayıt sürüyorsa kaydın, yoksa anında tekrarın değerleri
        _recorder.Progress += p => _dispatcher.BeginInvoke(() => ApplyProgress(p));
        _replay.Progress += p => _dispatcher.BeginInvoke(() =>
        {
            if (!IsRecording)
            {
                ApplyProgress(p);
            }
        });

        ApplyMixer();
    }

    public string? FfmpegPath => _ffmpegPath;

    /// <summary>Örn. "NVENC H.264"</summary>
    public string EncoderShortName
    {
        get => _encoderShortName;
        private set => SetProperty(ref _encoderShortName, value);
    }

    public double LiveFps
    {
        get => _liveFps;
        private set => SetProperty(ref _liveFps, value);
    }

    public long DroppedFrames
    {
        get => _droppedFrames;
        private set => SetProperty(ref _droppedFrames, value);
    }

    public long TotalFrames
    {
        get => _totalFrames;
        private set => SetProperty(ref _totalFrames, value);
    }

    /// <summary>Sürmekte olan kaydın şu anki dosya boyutu.</summary>
    public long OutputBytes
    {
        get => _outputBytes;
        private set => SetProperty(ref _outputBytes, value);
    }

    private void ApplyProgress(FfmpegProgress p)
    {
        LiveFps = p.Fps;
        DroppedFrames = p.DroppedFrames;
        TotalFrames = p.TotalFrames;
        OutputBytes = IsRecording ? p.TotalSizeBytes : 0;
    }

    /// <summary>Mikser kaydırıcısından gelir; kayıt sürerken de anında etkili olur.</summary>
    public void SetVolume(bool microphone, double volume)
    {
        if (microphone)
        {
            Settings.MicrophoneVolume = volume;
            AudioMixer.Microphone.Gain = (float)volume;
        }
        else
        {
            Settings.SystemVolume = volume;
            AudioMixer.System.Gain = (float)volume;
        }
    }

    /// <summary>Mikserdeki ses ayarlarını diske yazar (kaydırıcı bırakılınca).</summary>
    public void PersistSettings() => _store.Save(Settings);

    private void ApplyMixer()
    {
        AudioMixer.System.Gain = (float)Settings.SystemVolume;
        AudioMixer.Microphone.Gain = (float)Settings.MicrophoneVolume;
    }

    public AppSettings Settings { get; private set; }

    public bool IsReady
    {
        get => _isReady;
        private set => SetProperty(ref _isReady, value);
    }

    public bool IsReplayActive
    {
        get => _isReplayActive;
        private set
        {
            if (SetProperty(ref _isReplayActive, value))
            {
                StateChanged?.Invoke();
            }
        }
    }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (SetProperty(ref _isRecording, value))
            {
                StateChanged?.Invoke();
            }
        }
    }

    public string RecordingElapsed
    {
        get => _recordingElapsed;
        private set => SetProperty(ref _recordingElapsed, value);
    }

    /// <summary>Örn. "NVIDIA NVENC (h264_nvenc) · 60 FPS · 30 Mbps"</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ReplayLengthText => "Son " + FormatDuration(Settings.ReplaySeconds);

    public async Task InitializeAsync()
    {
        if (!await PrepareEncoderAsync())
        {
            return;
        }

        if (Settings.ReplayOnStartup)
        {
            await StartReplayAsync();
        }
    }

    public Task ToggleReplayAsync() => IsReplayActive ? StopReplayAsync() : StartReplayAsync();

    public async Task StartReplayAsync()
    {
        if (!IsReady || !await _replayGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            _replay.Start(Profile(), Settings.ReplaySeconds);
            IsReplayActive = true;
            Log.Write("Anında tekrar başladı.");
            Toast("Anında tekrar açık", $"Klip almak için {Settings.Hotkeys.SaveReplay}", ToastKind.Info);
        }
        catch (Exception ex)
        {
            Fail("Anında tekrar başlatılamadı", ex);
        }
        finally
        {
            _replayGate.Release();
        }
    }

    public async Task StopReplayAsync()
    {
        if (!await _replayGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            await _replay.StopAsync();
            IsReplayActive = false;
            Log.Write("Anında tekrar kullanıcı tarafından durduruldu.");
            Toast("Anında tekrar kapalı", null, ToastKind.Info);
        }
        finally
        {
            _replayGate.Release();
        }
    }

    public async Task SaveReplayAsync()
    {
        if (!IsReplayActive)
        {
            Toast("Anında tekrar kapalı", $"Önce {Settings.Hotkeys.ToggleReplay} ile aç", ToastKind.Info);
            return;
        }

        if (!await _replayGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var path = OutputPaths.For(Settings.OutputFolder, ".mp4");
            var saved = await _replay.SaveAsync(_ffmpegPath!, path);
            if (saved is null)
            {
                Toast("Klip kaydedilemedi", "Bir şeyler ters gitti, tekrar dene", ToastKind.Error);
                return;
            }

            await AnnounceSavedAsync("Klip kaydedildi", GameName(saved), saved);
        }
        catch (Exception ex)
        {
            Fail("Klip kaydedilemedi", ex);
        }
        finally
        {
            _replayGate.Release();
        }
    }

    public Task ToggleRecordingAsync() => IsRecording ? StopRecordingAsync() : StartRecordingAsync();

    public async Task StartRecordingAsync()
    {
        if (!IsReady || !await _recordGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            _recorder.Start(Profile(), OutputPaths.For(Settings.OutputFolder, Settings.Format == RecordingFormat.Mkv ? ".mkv" : ".mp4"));
            SetRecording(true);
            Toast("Kayıt başladı", $"Durdurmak için {Settings.Hotkeys.ToggleRecording}", ToastKind.Info);
        }
        catch (Exception ex)
        {
            Fail("Kayıt başlatılamadı", ex);
        }
        finally
        {
            _recordGate.Release();
        }
    }

    public async Task StopRecordingAsync()
    {
        if (!await _recordGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var duration = DateTime.Now - _recordingStartedAt;
            SetRecording(false);
            Toast("Kayıt bitiriliyor…", null, ToastKind.Info);

            var saved = await _recorder.StopAsync(_ffmpegPath!);
            if (saved is null)
            {
                Toast("Kayıt kaydedilemedi", "Bir şeyler ters gitti, tekrar dene", ToastKind.Error);
                return;
            }

            await AnnounceSavedAsync("Kayıt kaydedildi", $"{GameName(saved)}, {(duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : duration.ToString(@"m\:ss"))}", saved);
        }
        catch (Exception ex)
        {
            Fail("Kayıt durdurulamadı", ex);
        }
        finally
        {
            _recordGate.Release();
        }
    }

    public async Task TakeScreenshotAsync()
    {
        if (!IsReady)
        {
            return;
        }

        try
        {
            var path = OutputPaths.For(Settings.OutputFolder, ".png");
            var saved = await ScreenshotService.CaptureAsync(_ffmpegPath!, Settings.MonitorIndex, path);
            if (saved is null)
            {
                Toast("Ekran görüntüsü alınamadı", null, ToastKind.Error);
                return;
            }

            await AnnounceSavedAsync("Ekran görüntüsü alındı", GameName(saved), saved);
        }
        catch (Exception ex)
        {
            Fail("Ekran görüntüsü alınamadı", ex);
        }
    }

    /// <summary>Galeriden kırpma: seçili aralığı yeni bir dosyaya yazar, orijinale dokunmaz.</summary>
    public async Task<bool> TrimClipAsync(string path, TimeSpan start, TimeSpan end)
    {
        if (_ffmpegPath is null)
        {
            return false;
        }

        try
        {
            var saved = await ClipTrimmer.TrimAsync(_ffmpegPath, _encoder?.Backend ?? EncoderBackend.Cpu, path, start, end);
            if (saved is null)
            {
                Toast("Klip kırpılamadı", "Bir şeyler ters gitti, tekrar dene", ToastKind.Error);
                return false;
            }

            await AnnounceSavedAsync("Kırpılmış klip kaydedildi", GameName(saved), saved);
            return true;
        }
        catch (Exception ex)
        {
            Fail("Klip kırpılamadı", ex);
            return false;
        }
    }

    /// <summary>Klibi Geri Dönüşüm Kutusu'na taşır.</summary>
    public bool DeleteClip(string path)
    {
        if (!RecycleBin.Send(path))
        {
            Toast("Silinemedi", "Dosya başka bir programda açık olabilir", ToastKind.Error);
            return false;
        }

        Toast("Geri Dönüşüm Kutusu'na taşındı", Path.GetFileName(path), ToastKind.Info);
        return true;
    }

    private async Task AnnounceSavedAsync(string title, string detail, string path)
    {
        string? thumb = null;
        try
        {
            thumb = (await ThumbnailService.GetAsync(_ffmpegPath!, path))?.ImagePath;
        }
        catch (Exception ex)
        {
            Log.Write($"Küçük resim üretilemedi: {ex.Message}");
        }

        Toast(title, detail, ToastKind.Success, thumb, openPath: path);
        ClipSaved?.Invoke(path);
    }

    public void OpenGallery()
    {
        Directory.CreateDirectory(Settings.OutputFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Settings.OutputFolder}\"") { UseShellExecute = true });
    }

    /// <summary>Ayarlar penceresinden gelen yeni ayarları uygular; gerekirse kodlayıcıyı yeniden dener ve tekrarı yeniden başlatır.</summary>
    public async Task ApplySettingsAsync(AppSettings updated)
    {
        var old = Settings;
        Settings = updated;
        Log.Write("Ayarlar uygulanıyor.");
        _store.Save(updated);
        ApplyMixer();

        if (old.StartWithWindows != updated.StartWithWindows)
        {
            try
            {
                StartupRegistration.Set(updated.StartWithWindows);
            }
            catch (Exception ex)
            {
                Log.Write($"Başlangıç kaydı değiştirilemedi: {ex.Message}");
            }
        }
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(ReplayLengthText));

        var encoderChanged = old.Codec != updated.Codec ||
                             old.Encoder != updated.Encoder ||
                             old.FfmpegPath != updated.FfmpegPath;

        var wasReplayActive = IsReplayActive;
        if (wasReplayActive)
        {
            await _replay.StopAsync();
            IsReplayActive = false;
        }

        if ((encoderChanged || !IsReady) && !await PrepareEncoderAsync())
        {
            return;
        }

        UpdateStatus();

        if (wasReplayActive || (!old.ReplayOnStartup && updated.ReplayOnStartup))
        {
            await StartReplayAsync();
        }

        if (IsRecording)
        {
            Toast("Ayarlar kaydedildi", "Kayıt ayarları bir sonraki kayıtta geçerli", ToastKind.Info);
        }
    }

    public async Task ShutdownAsync()
    {
        if (IsRecording)
        {
            await StopRecordingAsync();
        }

        await _replay.StopAsync();
        IsReplayActive = false;
    }

    private async Task<bool> PrepareEncoderAsync()
    {
        IsReady = false;
        StatusText = "Hazırlanıyor…";

        _ffmpegPath = await Task.Run(() => FfmpegLocator.Find(Settings.FfmpegPath));
        if (_ffmpegPath is null)
        {
            StatusText = "ffmpeg bulunamadı";
            Toast("ffmpeg bulunamadı", "ffmpeg.exe uygulama klasöründe olmalı", ToastKind.Error);
            return false;
        }

        _encoder = await EncoderProbe.ResolveAsync(_ffmpegPath, Settings.Codec, Settings.Encoder);
        IsReady = true;
        UpdateStatus();
        return true;
    }

    private CaptureProfile Profile() => CaptureProfile.From(Settings, _ffmpegPath!, _encoder!);

    private void SetRecording(bool recording)
    {
        if (recording)
        {
            _recordingStartedAt = DateTime.Now;
            RecordingElapsed = "00:00:00";
            _recordingTimer.Start();
        }
        else
        {
            _recordingTimer.Stop();
            OutputBytes = 0;
            RecordingElapsed = "00:00:00";
        }

        IsRecording = recording;
    }

    private void UpdateStatus()
    {
        if (_encoder is not null)
        {
            StatusText = $"{_encoder.DisplayName} · {Settings.Fps} FPS · {Settings.BitrateMbps} Mbps";
            var backend = _encoder.Backend switch
            {
                EncoderBackend.Nvenc => "NVENC",
                EncoderBackend.Amf => "AMF",
                EncoderBackend.Qsv => "QSV",
                _ => "x264"
            };
            var codec = _encoder.FfmpegName.StartsWith("hevc") || _encoder.FfmpegName == "libx265" ? "HEVC"
                : _encoder.FfmpegName.StartsWith("av1") ? "AV1" : "H.264";
            EncoderShortName = $"{backend} {codec}";
        }
    }

    private void Toast(string title, string? detail, ToastKind kind, string? imagePath = null, string? openPath = null) =>
        ToastRequested?.Invoke(new ToastMessage(title, detail, kind, imagePath, openPath));

    private void Fail(string title, Exception ex)
    {
        Log.Write($"{title}: {ex}");
        Toast(title, ex.Message, ToastKind.Error);
    }

    private static string GameName(string path) => Path.GetFileName(Path.GetDirectoryName(path)) ?? "";

    private static string FormatElapsed(TimeSpan span) =>
        span.ToString(@"hh\:mm\:ss");

    private static string FormatDuration(int seconds)
    {
        var minutes = seconds / 60;
        var rest = seconds % 60;
        return (minutes, rest) switch
        {
            (0, _) => $"{rest} sn",
            (_, 0) => $"{minutes} dk",
            _ => $"{minutes} dk {rest} sn"
        };
    }
}
