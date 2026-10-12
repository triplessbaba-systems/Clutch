using Clutch.Core.Settings;

namespace Clutch.Core.Capture;

/// <summary>Bir yakalama oturumunun ihtiyaç duyduğu her şey; ayarlar + çözümlenmiş kodlayıcı.</summary>
public sealed record CaptureProfile(
    string FfmpegPath,
    EncoderChoice Encoder,
    int Fps,
    int BitrateMbps,
    EncoderPreset Preset,
    RateControl RateControl,
    RecordingFormat Format,
    int MonitorIndex,
    bool CaptureCursor,
    bool SystemAudio,
    bool Microphone,
    bool SeparateAudioTracks,
    int AudioBitrateKbps)
{
    public static CaptureProfile From(AppSettings settings, string ffmpegPath, EncoderChoice encoder) => new(
        ffmpegPath,
        encoder,
        Math.Clamp(settings.Fps, 15, 240),
        Math.Clamp(settings.BitrateMbps, 2, 150),
        settings.Preset,
        settings.RateControl,
        settings.Format,
        Math.Max(0, settings.MonitorIndex),
        settings.CaptureCursor,
        settings.RecordSystemAudio,
        settings.RecordMicrophone,
        settings.SeparateAudioTracks,
        Math.Clamp(settings.AudioBitrateKbps, 64, 320));
}
