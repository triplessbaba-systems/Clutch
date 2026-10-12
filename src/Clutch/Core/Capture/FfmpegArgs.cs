using System.Globalization;
using Clutch.Core.Audio;
using Clutch.Core.Settings;

namespace Clutch.Core.Capture;

/// <summary>Kayıt ve anında tekrar için ortak ffmpeg giriş/kodlama argümanları.</summary>
public static class FfmpegArgs
{
    /// <summary>
    /// ddagrab kareleri GPU'da (D3D11) üretir. NVENC ve AMF bunları doğrudan alır (sıfır kopya).
    /// QSV için eşleme, işlemci için ana belleğe indirme gerekir.
    /// </summary>
    public static string VideoFilterFor(EncoderBackend backend) => backend switch
    {
        EncoderBackend.Qsv => ",hwmap=derive_device=qsv,format=qsv",
        EncoderBackend.Cpu => ",hwdownload,format=bgra,format=yuv420p",
        _ => ""
    };

    public static List<string> Build(CaptureProfile profile, IReadOnlyList<AudioPipe> audio)
    {
        var args = new List<string>();

        // Giriş 0: ekran
        var grab = $"ddagrab=output_idx={profile.MonitorIndex}:framerate={profile.Fps}:draw_mouse={(profile.CaptureCursor ? 1 : 0)}";
        args.AddRange(["-f", "lavfi", "-i", grab + VideoFilterFor(profile.Encoder.Backend)]);

        // Giriş 1..n: WASAPI ses boruları (ham PCM)
        foreach (var pipe in audio)
        {
            args.AddRange([
                // Biçim zaten belli; ffmpeg gerçek zamanlı boruyu ~5 sn analiz etmeye çalışırsa
                // bu sürede ekran karesi çekilmez ve videonun başı donar
                "-probesize", "32",
                "-analyzeduration", "0",
                "-thread_queue_size", "4096",
                "-f", pipe.FfmpegSampleFormat,
                "-ar", pipe.SampleRate.ToString(CultureInfo.InvariantCulture),
                "-ac", pipe.Channels.ToString(CultureInfo.InvariantCulture),
                "-i", pipe.PipePath
            ]);
        }

        // Eşleme
        if (audio.Count >= 2 && !profile.SeparateAudioTracks)
        {
            var inputs = string.Concat(Enumerable.Range(1, audio.Count).Select(i => $"[{i}:a]"));
            args.AddRange([
                "-filter_complex", $"{inputs}amix=inputs={audio.Count}:duration=longest:dropout_transition=0:normalize=0[aout]",
                "-map", "0:v", "-map", "[aout]"
            ]);
        }
        else
        {
            args.AddRange(["-map", "0:v"]);
            for (var i = 1; i <= audio.Count; i++)
            {
                args.AddRange(["-map", $"{i}:a"]);
            }
        }

        args.AddRange(VideoEncoding(profile));

        if (audio.Count > 0)
        {
            args.AddRange(["-c:a", "aac", "-b:a", $"{profile.AudioBitrateKbps}k", "-ar", "48000"]);
        }

        return args;
    }

    private static IEnumerable<string> VideoEncoding(CaptureProfile profile)
    {
        var cbr = profile.RateControl == RateControl.Cbr;
        var bitrate = $"{profile.BitrateMbps}M";
        var maxrate = cbr ? bitrate : $"{profile.BitrateMbps * 3 / 2}M";
        var bufsize = $"{profile.BitrateMbps * 2}M";
        var gop = (profile.Fps * ReplayBuffer.SegmentSeconds).ToString(CultureInfo.InvariantCulture);

        var args = new List<string> { "-c:v", profile.Encoder.FfmpegName };

        // Ön ayar: Kalite / Dengeli / Performans, her kodlayıcının kendi adlandırmasına çevrilir
        switch (profile.Encoder.Backend)
        {
            case EncoderBackend.Nvenc:
                var nvPreset = profile.Preset switch
                {
                    EncoderPreset.Quality => "p6",
                    EncoderPreset.Performance => "p2",
                    _ => "p4"
                };
                args.AddRange(["-preset", nvPreset, "-tune", "hq", "-rc", cbr ? "cbr" : "vbr", "-forced-idr", "1"]);
                break;
            case EncoderBackend.Amf:
                var amfQuality = profile.Preset switch
                {
                    EncoderPreset.Quality => "quality",
                    EncoderPreset.Performance => "speed",
                    _ => "balanced"
                };
                args.AddRange(["-quality", amfQuality, "-rc", cbr ? "cbr" : "vbr_peak"]);
                break;
            case EncoderBackend.Qsv:
                var qsvPreset = profile.Preset switch
                {
                    EncoderPreset.Quality => "slower",
                    EncoderPreset.Performance => "veryfast",
                    _ => "faster"
                };
                args.AddRange(["-preset", qsvPreset]);
                break;
            default:
                var cpuPreset = profile.Preset switch
                {
                    EncoderPreset.Quality => "fast",
                    EncoderPreset.Performance => "ultrafast",
                    _ => "veryfast"
                };
                args.AddRange(["-preset", cpuPreset]);
                break;
        }

        args.AddRange(["-b:v", bitrate, "-maxrate", maxrate, "-bufsize", bufsize]);

        // Anında tekrar segmentlerinin tam anahtar karede bölünmesi için sabit GOP
        args.AddRange(["-g", gop, "-force_key_frames", $"expr:gte(t,n_forced*{ReplayBuffer.SegmentSeconds})"]);

        return args;
    }
}
