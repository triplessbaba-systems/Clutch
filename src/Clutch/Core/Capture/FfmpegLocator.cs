using System.Diagnostics;

namespace Clutch.Core.Capture;

public static class FfmpegLocator
{
    /// <summary>Sırasıyla: ayarlardaki yol, uygulama klasörü, PATH. Her aday gerçekten ffmpeg mi diye denenir.</summary>
    public static string? Find(string? configuredPath)
    {
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            candidates.Add(configuredPath);
        }

        candidates.Add(Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"));
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"));

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        candidates.AddRange(pathVar
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(dir => Path.Combine(dir.Trim(), "ffmpeg.exe")));

        return candidates.FirstOrDefault(IsUsable);
    }

    private static bool IsUsable(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-hide_banner");
            psi.ArgumentList.Add("-filters");

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            // ddagrab (Desktop Duplication) olmadan ekran yakalanamaz
            return output.Contains("ddagrab", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
