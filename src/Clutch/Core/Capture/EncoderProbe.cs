using Clutch.Core.Settings;

namespace Clutch.Core.Capture;

public sealed record EncoderChoice(EncoderBackend Backend, string FfmpegName)
{
    public string DisplayName => Backend switch
    {
        EncoderBackend.Nvenc => $"NVIDIA NVENC ({FfmpegName})",
        EncoderBackend.Amf => $"AMD AMF ({FfmpegName})",
        EncoderBackend.Qsv => $"Intel Quick Sync ({FfmpegName})",
        _ => $"İşlemci ({FfmpegName})"
    };
}

/// <summary>
/// Hangi donanım kodlayıcısının gerçekten çalıştığını bulur. ffmpeg -encoders listesi yeterli değil:
/// h264_nvenc NVIDIA kartı olmayan makinede de listelenir. Bu yüzden 3 kare gerçekten kodlanır.
/// </summary>
public static class EncoderProbe
{
    private static readonly EncoderBackend[] AutoOrder =
        [EncoderBackend.Nvenc, EncoderBackend.Amf, EncoderBackend.Qsv];

    public static async Task<EncoderChoice> ResolveAsync(string ffmpegPath, VideoCodec codec, EncoderBackend preferred)
    {
        var order = preferred switch
        {
            EncoderBackend.Auto => AutoOrder,
            EncoderBackend.Cpu => [],
            _ => new[] { preferred }
        };

        foreach (var backend in order)
        {
            var choice = new EncoderChoice(backend, EncoderName(codec, backend));
            if (await TryEncodeAsync(ffmpegPath, choice))
            {
                Log.Write($"Kodlayıcı seçildi: {choice.DisplayName}");
                return choice;
            }

            Log.Write($"Kodlayıcı kullanılamıyor: {choice.FfmpegName}");
        }

        var fallback = new EncoderChoice(EncoderBackend.Cpu, EncoderName(codec, EncoderBackend.Cpu));
        Log.Write($"Donanım kodlayıcı bulunamadı, işlemciye düşülüyor: {fallback.FfmpegName}");
        return fallback;
    }

    public static string EncoderName(VideoCodec codec, EncoderBackend backend)
    {
        var prefix = codec switch
        {
            VideoCodec.Hevc => "hevc",
            VideoCodec.Av1 => "av1",
            _ => "h264"
        };

        return backend switch
        {
            EncoderBackend.Nvenc => $"{prefix}_nvenc",
            EncoderBackend.Amf => $"{prefix}_amf",
            EncoderBackend.Qsv => $"{prefix}_qsv",
            // İşlemcide gerçek zamanlı AV1 pratik değil, H.264'e düşülür
            _ => codec == VideoCodec.Hevc ? "libx265" : "libx264"
        };
    }

    private static async Task<bool> TryEncodeAsync(string ffmpegPath, EncoderChoice choice)
    {
        var args = new List<string>
        {
            "-f", "lavfi",
            "-i", "ddagrab=output_idx=0:framerate=30" + FfmpegArgs.VideoFilterFor(choice.Backend),
            "-frames:v", "3",
            "-c:v", choice.FfmpegName,
            "-f", "null", "-"
        };

        var (exitCode, _) = await FfmpegProcess.RunAsync(ffmpegPath, args, TimeSpan.FromSeconds(10));
        return exitCode == 0;
    }
}
