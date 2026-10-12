using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Clutch.Core;
using Clutch.Core.Audio;
using Clutch.Core.Settings;
using Clutch.Services;

namespace Clutch.ViewModels;

public enum GalleryFilter
{
    All,
    Videos,
    Screenshots
}

/// <summary>
/// Clutch'ın tek arayüzü. Üstte eylem şeridi, solda galeri, sağda Ses / Ayarlar paneli.
/// Ses göstergeleri yalnızca overlay açıkken çalışır.
/// </summary>
public sealed class OverlayViewModel : ViewModelBase
{
    private readonly DispatcherTimer _meterTimer;
    private AudioLevelMonitor? _systemMonitor;
    private AudioLevelMonitor? _micMonitor;
    private bool _isActive;
    private bool _isSettingsTab;
    private GalleryFilter _filter;
    private bool _hasMicrophone = true;
    private ClipItemViewModel? _trimTarget;

    public OverlayViewModel(CaptureController controller, ClipLibrary library, Action close)
    {
        Controller = controller;
        Library = library;

        Clips = CollectionViewSource.GetDefaultView(library.Items);
        Clips.Filter = item => item is ClipItemViewModel clip && _filter switch
        {
            GalleryFilter.Videos => clip.IsVideo,
            GalleryFilter.Screenshots => !clip.IsVideo,
            _ => true
        };

        SystemChannel = new MixerChannelViewModel("Sistem sesi", "", AudioMixer.System,
            controller.Settings.SystemVolume, v => controller.SetVolume(false, v));
        MicChannel = new MixerChannelViewModel("Mikrofon", "", AudioMixer.Microphone,
            controller.Settings.MicrophoneVolume, v => controller.SetVolume(true, v));
        SyncChannels();

        _meterTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _meterTimer.Tick += (_, _) =>
        {
            SystemChannel.Tick(_systemMonitor?.TakePeak() ?? 0);
            MicChannel.Tick(_micMonitor?.TakePeak() ?? 0);
        };

        controller.PropertyChanged += OnControllerChanged;

        ToggleReplayCommand = new RelayCommand(() => _ = controller.ToggleReplayAsync(), () => controller.IsReady);
        SaveReplayCommand = new RelayCommand(() =>
        {
            close();
            _ = controller.SaveReplayAsync();
        }, () => controller.IsReplayActive);
        ToggleRecordingCommand = new RelayCommand(() =>
        {
            close();
            _ = controller.ToggleRecordingAsync();
        }, () => controller.IsReady);
        ScreenshotCommand = new RelayCommand(() =>
        {
            close();
            _ = controller.TakeScreenshotAsync();
        }, () => controller.IsReady);
        OpenFolderCommand = new RelayCommand(() =>
        {
            close();
            controller.OpenGallery();
        });
        OpenClipCommand = new RelayCommand<ClipItemViewModel>(clip =>
        {
            close();
            ClipLibrary.Open(clip);
        });
        RevealClipCommand = new RelayCommand<ClipItemViewModel>(clip =>
        {
            close();
            ClipLibrary.Reveal(clip);
        });
        TrimClipCommand = new RelayCommand<ClipItemViewModel>(clip =>
        {
            if (clip is { IsVideo: true })
            {
                TrimTarget = clip;
            }
        });
        DeleteClipCommand = new RelayCommand<ClipItemViewModel>(clip =>
        {
            if (clip is not null && controller.DeleteClip(clip.Path))
            {
                library.Items.Remove(clip);
            }
        });
        ShowAudioCommand = new RelayCommand(() => IsSettingsTab = false);
        ShowSettingsCommand = new RelayCommand(() => IsSettingsTab = true);
        SetFilterCommand = new RelayCommand<string>(f => Filter = Enum.TryParse<GalleryFilter>(f, out var v) ? v : GalleryFilter.All);
        SaveMixerCommand = new RelayCommand(controller.PersistSettings);
        CloseCommand = new RelayCommand(close);
    }

    public CaptureController Controller { get; }
    public ClipLibrary Library { get; }

    /// <summary>Galeride gösterilen, filtrelenmiş klip listesi.</summary>
    public ICollectionView Clips { get; }

    /// <summary>Kırpılan klip; doluyken galeri yerine kırpma ekranı görünür.</summary>
    public ClipItemViewModel? TrimTarget
    {
        get => _trimTarget;
        set
        {
            if (SetProperty(ref _trimTarget, value))
            {
                OnPropertyChanged(nameof(IsTrimming));
            }
        }
    }

    public bool IsTrimming => _trimTarget is not null;

    public GalleryFilter Filter
    {
        get => _filter;
        set
        {
            if (SetProperty(ref _filter, value))
            {
                Clips.Refresh();
            }
        }
    }

    // Kısayollar araç ipuçlarında gösterilir; eylem şeridi yalnızca kısa etiket taşır
    public string ReplayTip => $"Anında tekrarı aç veya kapat  {Controller.Settings.Hotkeys.ToggleReplay}";
    public string SaveTip => $"Son {Controller.Settings.ReplaySeconds} saniyeyi kaydet  {Controller.Settings.Hotkeys.SaveReplay}";
    public string RecordTip => $"Kaydı başlat veya durdur  {Controller.Settings.Hotkeys.ToggleRecording}";
    public string ScreenshotTip => $"Ekran görüntüsü al  {Controller.Settings.Hotkeys.Screenshot}";
    public string CloseTip => $"Kapat  Esc";

    public MixerChannelViewModel SystemChannel { get; }
    public MixerChannelViewModel MicChannel { get; }

    /// <summary>Sağ panelde Ayarlar mı (true) yoksa Ses ve Kaynaklar mı (false) görünüyor.</summary>
    public bool IsSettingsTab
    {
        get => _isSettingsTab;
        set => SetProperty(ref _isSettingsTab, value);
    }

    /// <summary>Kaynak anahtarları; değişince ayarlar hemen uygulanır (tampon yeniden başlar).</summary>
    public bool SystemAudioRecorded
    {
        get => Controller.Settings.RecordSystemAudio;
        set => _ = UpdateSettingAsync(s => s.RecordSystemAudio = value);
    }

    /// <summary>Takılı mikrofon yoksa anahtar devre dışı kalır ve "Cihaz bulunamadı" yazar.</summary>
    public bool HasMicrophone
    {
        get => _hasMicrophone;
        private set
        {
            if (SetProperty(ref _hasMicrophone, value))
            {
                MicChannel.IsAvailable = value;
            }
        }
    }

    public bool MicrophoneRecorded
    {
        get => Controller.Settings.RecordMicrophone;
        set => _ = UpdateSettingAsync(s => s.RecordMicrophone = value);
    }

    public bool CursorCaptured
    {
        get => Controller.Settings.CaptureCursor;
        set => _ = UpdateSettingAsync(s => s.CaptureCursor = value);
    }

    public ICommand ToggleReplayCommand { get; }
    public ICommand SaveReplayCommand { get; }
    public ICommand ToggleRecordingCommand { get; }
    public ICommand ScreenshotCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenClipCommand { get; }
    public ICommand RevealClipCommand { get; }
    public ICommand TrimClipCommand { get; }
    public ICommand DeleteClipCommand { get; }
    public ICommand ShowAudioCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand SetFilterCommand { get; }
    public ICommand SaveMixerCommand { get; }
    public ICommand CloseCommand { get; }

    /// <summary>Overlay açılınca göstergeler başlar ve galeri tazelenir; kapanınca göstergeler durur.</summary>
    public void SetActive(bool active)
    {
        if (active == _isActive)
        {
            return;
        }

        _isActive = active;
        if (active)
        {
            HasMicrophone = AudioDevices.HasMicrophone();
            Library.Refresh();
            StartMeters();
        }
        else
        {
            StopMeters();
        }
    }

    private void StartMeters()
    {
        StopMeters();
        try
        {
            _systemMonitor = AudioLevelMonitor.ForSystemAudio();
        }
        catch (Exception ex)
        {
            Log.Write($"Sistem sesi göstergesi açılamadı: {ex.Message}");
        }

        // Mikrofon yalnızca kayda alınıyorsa dinlenir; yoksa Windows boşuna "mikrofon kullanımda" gösterir
        if (Controller.Settings.RecordMicrophone && HasMicrophone)
        {
            try
            {
                _micMonitor = AudioLevelMonitor.ForMicrophone();
            }
            catch (Exception ex)
            {
                Log.Write($"Mikrofon göstergesi açılamadı: {ex.Message}");
            }
        }

        _meterTimer.Start();
    }

    private void StopMeters()
    {
        _meterTimer.Stop();
        _systemMonitor?.Dispose();
        _systemMonitor = null;
        _micMonitor?.Dispose();
        _micMonitor = null;
    }

    private async Task UpdateSettingAsync(Action<AppSettings> change)
    {
        var updated = Controller.Settings.Clone();
        change(updated);
        await Controller.ApplySettingsAsync(updated);
    }

    private void SyncChannels()
    {
        SystemChannel.IsRecorded = Controller.Settings.RecordSystemAudio;
        MicChannel.IsRecorded = Controller.Settings.RecordMicrophone;
    }

    private void OnControllerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CaptureController.Settings))
        {
            return;
        }

        SyncChannels();
        OnPropertyChanged(nameof(SystemAudioRecorded));
        OnPropertyChanged(nameof(MicrophoneRecorded));
        OnPropertyChanged(nameof(CursorCaptured));
        OnPropertyChanged(nameof(ReplayTip));
        OnPropertyChanged(nameof(SaveTip));
        OnPropertyChanged(nameof(RecordTip));
        OnPropertyChanged(nameof(ScreenshotTip));

        if (_isActive)
        {
            // Mikrofon tercihi değişmiş olabilir
            StartMeters();
        }
    }
}
