using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Clutch.Core.Capture;

public sealed record Thumbnail(string ImagePath, TimeSpan? Duration);

/// <summary>
/// Klipler için küçük resim (JPEG) üretir ve %LocalAppData%\Clutch\thumbs altında önbellekler.
/// Video süresi aynı ffmpeg çağrısının çıktısından okunur ve yanına .dur dosyası olarak yazılır.
/// </summary>
public static partial class ThumbnailService
{
    private static readonly string CacheFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clutch", "thumbs");

    private static readonly SemaphoreSlim Gate = new(2, 2);

    [GeneratedRegex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)")]
    private static partial Regex DurationPattern();

    public static async Task<Thumbnail?> GetAsync(string ffmpegPath, string mediaPath)
    {
        if (!File.Exists(mediaPath))
        {
            return null;
        }

        if (Path.GetExtension(mediaPath).Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            return new Thumbnail(mediaPath, null);
        }

        var info = new FileInfo(mediaPath);
        var key = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes($"{mediaPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")))[..16];
        var thumbPath = Path.Combine(CacheFolder, key + ".jpg");
        var durationPath = Path.Combine(CacheFolder, key + ".dur");

        if (File.Exists(thumbPath))
        {
            return new Thumbnail(thumbPath, ReadDuration(durationPath));
        }

        await Gate.WaitAsync();
        try
        {
            Directory.CreateDirectory(CacheFolder);

            // -loglevel info: süre satırı ("Duration: 00:00:18.04") yalnızca bu düzeyde yazılır
            var (exitCode, log) = await FfmpegProcess.RunAsync(
                ffmpegPath,
                ["-loglevel", "info", "-ss", "0.5", "-i", mediaPath, "-frames:v", "1", "-vf", "scale=384:-2", "-q:v", "4", thumbPath],
                TimeSpan.FromSeconds(15));

            if (exitCode != 0 || !File.Exists(thumbPath))
            {
                return null;
            }

            var duration = ParseDuration(log);
            if (duration is { } d)
            {
                File.WriteAllText(durationPath, d.TotalSeconds.ToString(CultureInfo.InvariantCulture));
            }

            return new Thumbnail(thumbPath, duration);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static TimeSpan? ParseDuration(string log)
    {
        var m = DurationPattern().Match(log);
        if (!m.Success)
        {
            return null;
        }

        return new TimeSpan(0, int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), 0)
               + TimeSpan.FromSeconds(double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
    }

    private static TimeSpan? ReadDuration(string path)
    {
        try
        {
            return File.Exists(path) &&
                   double.TryParse(File.ReadAllText(path), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? TimeSpan.FromSeconds(seconds)
                : null;
        }
        catch
        {
            return null;
        }
    }
}
