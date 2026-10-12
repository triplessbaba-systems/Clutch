using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Clutch.Core.Settings;
using Clutch.UI.Controls;

namespace Clutch.Views;

/// <summary>Overlay'in sağ panelindeki ayarlar. Ayarların bir kopyası üzerinde çalışır; Uygula ile geçerli olur.</summary>
public partial class SettingsPanel : UserControl
{
    private sealed record Option<T>(T Value, string Label);

    private Func<AppSettings>? _current;
    private Func<AppSettings, Task>? _apply;
    private Action? _exit;
    private AppSettings _draft = new();

    public SettingsPanel()
    {
        InitializeComponent();

        EncoderBox.ItemsSource = new[]
        {
            new Option<EncoderBackend>(EncoderBackend.Auto, "Otomatik"),
            new Option<EncoderBackend>(EncoderBackend.Nvenc, "NVIDIA NVENC"),
            new Option<EncoderBackend>(EncoderBackend.Amf, "AMD AMF"),
            new Option<EncoderBackend>(EncoderBackend.Qsv, "Intel Quick Sync"),
            new Option<EncoderBackend>(EncoderBackend.Cpu, "İşlemci (yavaş)")
        };
        CodecBox.ItemsSource = new[]
        {
            new Option<VideoCodec>(VideoCodec.H264, "H.264"),
            new Option<VideoCodec>(VideoCodec.Hevc, "HEVC (H.265)"),
            new Option<VideoCodec>(VideoCodec.Av1, "AV1")
        };
        PresetBox.ItemsSource = new[]
        {
            new Option<EncoderPreset>(EncoderPreset.Quality, "Yüksek"),
            new Option<EncoderPreset>(EncoderPreset.Balanced, "Dengeli"),
            new Option<EncoderPreset>(EncoderPreset.Performance, "Performans")
        };
        RateBox.ItemsSource = new[]
        {
            new Option<RateControl>(RateControl.Vbr, "Değişken (VBR)"),
            new Option<RateControl>(RateControl.Cbr, "Sabit (CBR)")
        };
        FormatBox.ItemsSource = new[]
        {
            new Option<RecordingFormat>(RecordingFormat.Mp4, "MP4"),
            new Option<RecordingFormat>(RecordingFormat.Mkv, "MKV")
        };
        AudioBitrateBox.ItemsSource = new[] { 128, 160, 192, 256, 320 }.Select(k => new Option<int>(k, $"{k} kb/sn")).ToArray();
        FpsBox.ItemsSource = new[] { 30, 60, 120 }.Select(f => new Option<int>(f, $"{f} FPS")).ToArray();
        ReplayBox.ItemsSource = new[] { 15, 30, 60, 120, 180, 300, 600, 1200 }
            .Select(s => new Option<int>(s, s < 60 ? $"{s} saniye" : $"{s / 60} dakika"))
            .ToArray();
        var version = typeof(SettingsPanel).Assembly.GetName().Version;
        VersionText.Text = version is null ? "" : $"Clutch {version.Major}.{version.Minor}.{version.Build}";

        MonitorBox.ItemsSource = Enumerable.Range(0, Math.Max(1, GetSystemMetrics(SmCMonitors)))
            .Select(i => new Option<int>(i, i == 0 ? "Ana ekran" : $"Ekran {i + 1}"))
            .ToArray();
    }

    private IEnumerable<HotkeyBox> HotkeyBoxes => [HkOverlay, HkSaveReplay, HkToggleReplay, HkRecord, HkScreenshot];

    public void Initialize(Func<AppSettings> current, Func<AppSettings, Task> apply, Action<bool> suspendHotkeys, Action exit)
    {
        _current = current;
        _apply = apply;
        _exit = exit;

        foreach (var box in HotkeyBoxes)
        {
            box.RecordingChanged += suspendHotkeys;
            box.ValueChanged += UpdateHotkeyWarnings;
        }

        Reload();
    }

    /// <summary>Taslağı uygulamanın güncel ayarlarından yeniden kurar (overlay açılışında, Geri al'da).</summary>
    public void Reload()
    {
        if (_current is null)
        {
            return;
        }

        _draft = _current().Clone();
        Rebind();
        ShowError(null);
        UpdateHotkeyWarnings();
    }

    private void UpdateHotkeyWarnings()
    {
        var lines = new List<string>();
        foreach (var value in _draft.Hotkeys.All)
        {
            if (KnownShortcuts.Find(value) is { } owner)
            {
                lines.Add($"{value}, {owner} ile çakışıyor.");
            }
        }

        lines.AddRange(_draft.Hotkeys.All
            .GroupBy(Hotkey.Normalize, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} iki ayrı işe atanmış."));

        ConflictText.Text = string.Join(Environment.NewLine, lines);
        ConflictPanel.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnResetHotkeys(object sender, RoutedEventArgs e)
    {
        _draft.Hotkeys = new HotkeySettings();
        Rebind();
        UpdateHotkeyWarnings();
    }

    private void OnRevert(object sender, RoutedEventArgs e) => Reload();

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        var invalid = _draft.Hotkeys.All.FirstOrDefault(h => !Hotkey.TryParse(h, out _));
        if (invalid is not null)
        {
            ShowError($"Geçersiz kısayol: \"{invalid}\"");
            return;
        }

        if (_draft.Hotkeys.All.Select(Hotkey.Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 5)
        {
            ShowError("Aynı kısayol birden fazla eyleme atanmış.");
            return;
        }

        if (_apply is null)
        {
            return;
        }

        IsEnabled = false;
        try
        {
            await _apply(_draft.Clone());
            ShowError(null);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Kayıt klasörünü seç", InitialDirectory = _draft.OutputFolder };

        // Overlay en üstte durduğu için iletişim kutusu ona bağlanır; yoksa arkasında açılır
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            _draft.OutputFolder = dialog.FolderName;
            Rebind();
        }
    }

    private void OnExit(object sender, RoutedEventArgs e) => _exit?.Invoke();

    private void ShowError(string? message)
    {
        ErrorText.Text = message ?? "";
        ErrorText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>AppSettings düz bir sınıf (INotifyPropertyChanged yok); koddan yapılan değişiklikleri göstermek için yeniden bağlanır.</summary>
    private void Rebind()
    {
        DataContext = null;
        DataContext = _draft;
    }

    private const int SmCMonitors = 80;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
