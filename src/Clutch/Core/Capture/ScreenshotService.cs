namespace Clutch.Core.Capture;

public static class ScreenshotService
{
    public static async Task<string?> CaptureAsync(string ffmpegPath, int monitorIndex, string finalPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        var (exitCode, log) = await FfmpegProcess.RunAsync(
            ffmpegPath,
            [
                "-f", "lavfi",
                "-i", $"ddagrab=output_idx={monitorIndex}:draw_mouse=0,hwdownload,format=bgra",
                "-frames:v", "1",
                finalPath
            ],
            TimeSpan.FromSeconds(10));

        if (exitCode != 0 || !File.Exists(finalPath))
        {
            Log.Write($"Ekran görüntüsü alınamadı: {log}");
            return null;
        }

        return finalPath;
    }
}
