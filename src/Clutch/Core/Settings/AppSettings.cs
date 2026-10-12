using System.Text.Json.Serialization;

namespace Clutch.Core.Settings;

public enum VideoCodec
{
    H264,
    Hevc,
    Av1
}

public enum EncoderBackend
{
    Auto,
    Nvenc,
    Amf,
    Qsv,
    Cpu
}

public enum EncoderPreset
{
    Quality,
    Balanced,
    Performance
}

public enum RateControl
{
    Vbr,
    Cbr
}

public enum RecordingFormat
{
    Mp4,
    Mkv
}

public sealed class AppSettings
{
    public const int CurrentVersion = 2;

    /// <summary>Eski dosyalarda yoktur (null); SettingsStore buna bakarak taşıma yapar.</summary>
    public int? SettingsVersion { get; set; }

    public string OutputFolder { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Clutch");

    /// <summary>Boşsa uygulama klasörü ve PATH taranır.</summary>
    public string FfmpegPath { get; set; } = "";

    public VideoCodec Codec { get; set; } = VideoCodec.H264;
    public EncoderBackend Encoder { get; set; } = EncoderBackend.Auto;
    public int Fps { get; set; } = 60;
    public int BitrateMbps { get; set; } = 30;
    public EncoderPreset Preset { get; set; } = EncoderPreset.Balanced;
    public RateControl RateControl { get; set; } = RateControl.Vbr;
    public RecordingFormat Format { get; set; } = RecordingFormat.Mp4;
    public int MonitorIndex { get; set; }
    public bool CaptureCursor { get; set; } = true;

    public int ReplaySeconds { get; set; } = 60;
    public bool ReplayOnStartup { get; set; } = true;

    public bool RecordSystemAudio { get; set; } = true;
    public bool RecordMicrophone { get; set; }
    public bool SeparateAudioTracks { get; set; }
    public int AudioBitrateKbps { get; set; } = 192;

    /// <summary>Mikserdeki seviyeler (0–1). Kayıt sırasında da canlı uygulanır.</summary>
    public double SystemVolume { get; set; } = 1.0;
    public double MicrophoneVolume { get; set; } = 1.0;

    public bool ShowRecordingIndicator { get; set; } = true;

    /// <summary>Arayüz için; asıl durum kayıt defterindeki Run girdisidir (açılışta oradan okunur).</summary>
    public bool StartWithWindows { get; set; }

    public HotkeySettings Hotkeys { get; set; } = new();

    public AppSettings Clone() => SettingsStore.Deserialize(SettingsStore.Serialize(this));
}

/// <summary>
/// Ctrl+Alt+F5–F8: NVIDIA (Alt+Z, Alt+F1/F9/F10/F12), Game Bar (Win+Alt+…), Steam (F12, Shift+Tab) ve
/// Discord kullanmaz. F tuşları Türkçe klavyede AltGr ile karakter üretmediği için @, € gibi karakterleri de yutmaz.
/// </summary>
public sealed class HotkeySettings
{
    public string ToggleOverlay { get; set; } = "Ctrl+Alt+F5";
    public string Screenshot { get; set; } = "Ctrl+Alt+F6";
    public string ToggleRecording { get; set; } = "Ctrl+Alt+F7";
    public string SaveReplay { get; set; } = "Ctrl+Alt+F8";
    public string ToggleReplay { get; set; } = "Ctrl+Alt+Shift+F8";

    [JsonIgnore]
    public IEnumerable<string> All => [ToggleOverlay, Screenshot, ToggleRecording, SaveReplay, ToggleReplay];
}
