namespace Clutch.Core.Capture;

/// <summary>
/// Manuel kayıt. Önce MKV'ye yazılır: uygulama ya da bilgisayar çökerse dosya yine oynatılabilir.
/// Durdurulunca hedef MP4 ise kayıpsız ve hızlıca dönüştürülür (-c copy), MKV ise yalnızca yeniden adlandırılır.
/// </summary>
public sealed class ScreenRecorder
{
    private CaptureSession? _session;
    private string? _tempPath;
    private string? _finalPath;

    public event Action<string>? Failed;
    public event Action<FfmpegProgress>? Progress;

    public bool IsRecording => _session is not null;

    public void Start(CaptureProfile profile, string finalPath)
    {
        if (_session is not null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);
        _finalPath = finalPath;
        _tempPath = Path.ChangeExtension(finalPath, ".kayit.mkv");

        _session = CaptureSession.Start(profile, ["-f", "matroska", _tempPath]);
        _session.Failed += OnSessionFailed;
        _session.Progress += OnSessionProgress;
    }

    /// <summary>Kaydı bitirir ve oluşan dosyanın yolunu döner.</summary>
    public async Task<string?> StopAsync(string ffmpegPath)
    {
        var session = _session;
        if (session is null || _tempPath is null || _finalPath is null)
        {
            return null;
        }

        _session = null;
        session.Failed -= OnSessionFailed;
        session.Progress -= OnSessionProgress;
        await session.StopAsync();

        return await RemuxAsync(ffmpegPath, _tempPath, _finalPath);
    }

    private static async Task<string?> RemuxAsync(string ffmpegPath, string tempPath, string finalPath)
    {
        if (!File.Exists(tempPath))
        {
            return null;
        }

        if (Path.GetExtension(finalPath).Equals(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            File.Move(tempPath, finalPath, overwrite: true);
            return finalPath;
        }

        var (exitCode, log) = await FfmpegProcess.RunAsync(
            ffmpegPath,
            ["-i", tempPath, "-map", "0", "-c", "copy", "-movflags", "+faststart", finalPath],
            TimeSpan.FromMinutes(5));

        if (exitCode == 0 && File.Exists(finalPath))
        {
            File.Delete(tempPath);
            return finalPath;
        }

        // Dönüştürme başarısız olursa MKV elde kalır, kayıt kaybolmaz
        Log.Write($"MP4'e dönüştürme başarısız, MKV korunuyor: {log}");
        return tempPath;
    }

    private void OnSessionFailed(string reason)
    {
        _session?.Dispose();
        _session = null;
        Failed?.Invoke(reason);
    }

    private void OnSessionProgress(FfmpegProgress progress) => Progress?.Invoke(progress);
}
