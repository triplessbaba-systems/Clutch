using System.Diagnostics;
using Clutch.Core.Native;

namespace Clutch.Core.Capture;

/// <summary>ffmpeg'in saniyede bir bildirdiği canlı durum (-progress).</summary>
public sealed record FfmpegProgress(double Fps, long DroppedFrames, long TotalFrames, long TotalSizeBytes, TimeSpan OutTime);

/// <summary>Uzun süre çalışan bir ffmpeg süreci. Durdurmak için stdin'e 'q' yazılır, böylece dosya düzgün kapanır.</summary>
public sealed class FfmpegProcess : IDisposable
{
    private const int LogLines = 30;

    private readonly Process _process;
    private readonly Queue<string> _recentLog = new();
    private readonly Dictionary<string, string> _progressFields = new(StringComparer.Ordinal);
    private bool _stopRequested;

    public event Action<FfmpegProcess>? UnexpectedExit;
    public event Action<FfmpegProgress>? Progress;

    private FfmpegProcess(Process process)
    {
        _process = process;
    }

    public bool HasExited => _process.HasExited;

    public string RecentLog
    {
        get
        {
            lock (_recentLog)
            {
                return string.Join(Environment.NewLine, _recentLog);
            }
        }
    }

    public static FfmpegProcess Start(string ffmpegPath, IEnumerable<string> arguments, string? workingDirectory = null)
    {
        // Canlı FPS / kare kaybı için ffmpeg her saniye stdout'a anahtar=değer satırları yazar
        var psi = CreateStartInfo(ffmpegPath, ["-progress", "pipe:1", "-stats_period", "1", .. arguments], workingDirectory);
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;

        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var wrapper = new FfmpegProcess(process);

        process.ErrorDataReceived += (_, e) => wrapper.AppendLog(e.Data);
        process.OutputDataReceived += (_, e) => wrapper.OnProgressLine(e.Data);
        process.Exited += (_, _) =>
        {
            if (!wrapper._stopRequested)
            {
                Log.Write($"ffmpeg beklenmedik şekilde kapandı (kod {SafeExitCode(process)}):{Environment.NewLine}{wrapper.RecentLog}");
                wrapper.UnexpectedExit?.Invoke(wrapper);
            }
        };

        Log.Write($"ffmpeg başlatılıyor: {string.Join(' ', psi.ArgumentList)}");
        process.Start();
        ChildProcessJob.Attach(process);
        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        return wrapper;
    }

    private void OnProgressLine(string? line)
    {
        if (line is null)
        {
            return;
        }

        var eq = line.IndexOf('=');
        if (eq <= 0)
        {
            return;
        }

        var key = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim();

        if (key != "progress")
        {
            _progressFields[key] = value;
            return;
        }

        // "progress=continue" bir bloğun sonu
        Progress?.Invoke(new FfmpegProgress(
            ParseDouble("fps"),
            ParseLong("drop_frames"),
            ParseLong("frame"),
            ParseLong("total_size"),
            TimeSpan.FromMicroseconds(ParseLong("out_time_us"))));
    }

    private double ParseDouble(string key) =>
        _progressFields.TryGetValue(key, out var v) &&
        double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d
            : 0;

    private long ParseLong(string key) =>
        _progressFields.TryGetValue(key, out var v) && long.TryParse(v, out var l) && l > 0 ? l : 0;

    /// <summary>Kısa süreli işler için (deneme, ekran görüntüsü, birleştirme). Çıkış kodu ve son log satırlarını döner.</summary>
    public static async Task<(int ExitCode, string Log)> RunAsync(
        string ffmpegPath,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        string? workingDirectory = null)
    {
        var psi = CreateStartInfo(ffmpegPath, arguments, workingDirectory);
        using var process = new Process { StartInfo = psi };
        var log = new System.Text.StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (log)
                {
                    log.AppendLine(e.Data);
                }
            }
        };

        process.Start();
        ChildProcessJob.Attach(process);
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, "Zaman aşımı");
        }

        lock (log)
        {
            return (process.ExitCode, log.ToString());
        }
    }

    public async Task StopAsync(TimeSpan timeout)
    {
        _stopRequested = true;
        if (_process.HasExited)
        {
            return;
        }

        try
        {
            await _process.StandardInput.WriteAsync('q');
            await _process.StandardInput.FlushAsync();
        }
        catch (IOException)
        {
        }

        using var cts = new CancellationTokenSource(timeout);
        try
        {
            await _process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log.Write("ffmpeg 'q' komutuna yanıt vermedi, sonlandırılıyor.");
            _process.Kill(entireProcessTree: true);
        }
    }

    public void Dispose()
    {
        _stopRequested = true;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }

        _process.Dispose();
    }

    private void AppendLog(string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (_recentLog)
        {
            _recentLog.Enqueue(line);
            while (_recentLog.Count > LogLines)
            {
                _recentLog.Dequeue();
            }
        }
    }

    private static ProcessStartInfo CreateStartInfo(string ffmpegPath, IEnumerable<string> arguments, string? workingDirectory)
    {
        var psi = new ProcessStartInfo(ffmpegPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? AppContext.BaseDirectory
        };

        psi.ArgumentList.Add("-hide_banner");
        psi.ArgumentList.Add("-loglevel");
        psi.ArgumentList.Add("warning");
        psi.ArgumentList.Add("-y");
        foreach (var argument in arguments)
        {
            psi.ArgumentList.Add(argument);
        }

        return psi;
    }

    private static string SafeExitCode(Process process)
    {
        try
        {
            return process.ExitCode.ToString();
        }
        catch
        {
            return "?";
        }
    }
}
