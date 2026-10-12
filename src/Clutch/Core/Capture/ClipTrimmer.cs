using System.Globalization;
using Clutch.Core.Settings;

namespace Clutch.Core.Capture;

/// <summary>
/// Klibin seçilen aralığını yeni bir dosyaya yazar; orijinal dosyaya dokunmaz.
/// Kayıtlar 2 saniyede bir anahtar kare içerdiği için -c copy ile kesmek ±2 sn kayar; bu yüzden
/// görüntü yeniden kodlanır (ekran kartında, birkaç saniye sürer) ve kesim kare hassasiyetinde olur.
/// </summary>
public static class ClipTrimmer
{
    public static async Task<string?> TrimAsync(string ffmpegPath, EncoderBackend backend, string input, TimeSpan start, TimeSpan end)
    {
        var length = end - start;
        if (length <= TimeSpan.FromMilliseconds(200))
        {
            return null;
        }

        var output = UniqueOutputPath(input);
        var videoArgs = backend switch
        {
            EncoderBackend.Nvenc => new[] { "-c:v", "h264_nvenc", "-preset", "p5", "-rc", "vbr", "-cq", "19", "-b:v", "0" },
            EncoderBackend.Amf => ["-c:v", "h264_amf", "-quality", "quality", "-rc", "cqp", "-qp_i", "18", "-qp_p", "20"],
            EncoderBackend.Qsv => ["-c:v", "h264_qsv", "-global_quality", "20"],
            _ => ["-c:v", "libx264", "-preset", "veryfast", "-crf", "18"]
        };

        var args = new List<string>
        {
            // -ss girişten önce: hızlı arama; yeniden kodlandığı için kare hassasiyetinde
            "-ss", Seconds(start),
            "-i", input,
            "-t", Seconds(length),
            "-map", "0:v:0", "-map", "0:a?"
        };
        args.AddRange(videoArgs);
        args.AddRange(["-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", output]);

        var (exitCode, log) = await FfmpegProcess.RunAsync(ffmpegPath, args, TimeSpan.FromMinutes(10));
        if (exitCode != 0 || !File.Exists(output))
        {
            Log.Write($"Kırpma başarısız: {log}");
            return null;
        }

        return output;
    }

    private static string UniqueOutputPath(string input)
    {
        var folder = Path.GetDirectoryName(input)!;
        var name = Path.GetFileNameWithoutExtension(input);
        var candidate = Path.Combine(folder, $"{name} (kırpılmış).mp4");
        for (var i = 2; File.Exists(candidate); i++)
        {
            candidate = Path.Combine(folder, $"{name} (kırpılmış {i}).mp4");
        }

        return candidate;
    }

    private static string Seconds(TimeSpan t) => t.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
}
