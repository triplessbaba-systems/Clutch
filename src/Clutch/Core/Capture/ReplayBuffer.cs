using System.Globalization;

namespace Clutch.Core.Capture;

/// <summary>
/// Anında tekrar (NVIDIA "Instant Replay"). ffmpeg sürekli 2 saniyelik .ts parçaları yazar,
/// eskileri kendisi siler (hls muxer + delete_segments). Kaydet denince son N saniyelik parçalar
/// kopyalanıp yeniden kodlamadan (-c copy) tek MP4'te birleştirilir. Bu yüzden kaydetme anlık ve GPU'yu yormaz.
/// </summary>
public sealed class ReplayBuffer
{
    public const int SegmentSeconds = 2;

    private static readonly string BufferFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Clutch", "replay");

    private CaptureSession? _session;
    private int _seconds;

    public event Action<string>? Failed;
    public event Action<FfmpegProgress>? Progress;

    public bool IsActive => _session is not null;

    public void Start(CaptureProfile profile, int seconds)
    {
        if (_session is not null)
        {
            return;
        }

        _seconds = Math.Clamp(seconds, 10, 20 * 60);
        ResetFolder();

        var listSize = SegmentCount(_seconds);
        _session = CaptureSession.Start(profile,
        [
            "-f", "hls",
            "-hls_time", SegmentSeconds.ToString(CultureInfo.InvariantCulture),
            "-hls_list_size", listSize.ToString(CultureInfo.InvariantCulture),
            "-hls_flags", "delete_segments",
            "-hls_delete_threshold", "2",
            "-hls_segment_filename", "seg%07d.ts",
            "buffer.m3u8"
        ], workingDirectory: BufferFolder);

        _session.Failed += OnSessionFailed;
        _session.Progress += OnSessionProgress;
    }

    public async Task StopAsync()
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        _session = null;
        session.Failed -= OnSessionFailed;
        session.Progress -= OnSessionProgress;
        await session.StopAsync();
        ResetFolder();
    }

    /// <summary>Son N saniyeyi MP4 olarak kaydeder. Tampon çalışmaya devam eder.</summary>
    public async Task<string?> SaveAsync(string ffmpegPath, string finalPath)
    {
        if (_session is null)
        {
            return null;
        }

        // Parça numaraları hep artar; ada göre sıralamak zamana göre sıralamaktır.
        // Son parça hâlâ yazılıyor olabilir; .ts yarım da olsa okunabilir.
        var segments = Directory.GetFiles(BufferFolder, "seg*.ts")
            .Order(StringComparer.Ordinal)
            .TakeLast(SegmentCount(_seconds) + 1)
            .ToList();

        if (segments.Count == 0)
        {
            return null;
        }

        var workFolder = Path.Combine(BufferFolder, "save_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workFolder);

        try
        {
            // Kopyalarken ffmpeg yazmaya ve eski parçaları silmeye devam edebilsin
            var names = new List<string>();
            foreach (var segment in segments)
            {
                var name = Path.GetFileName(segment);
                try
                {
                    await using var source = new FileStream(segment, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    await using var target = File.Create(Path.Combine(workFolder, name));
                    await source.CopyToAsync(target);
                    names.Add(name);
                }
                catch (FileNotFoundException)
                {
                    // Tam o anda silinen en eski parça
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            var (exitCode, log) = await FfmpegProcess.RunAsync(
                ffmpegPath,
                [
                    "-i", "concat:" + string.Join('|', names),
                    "-map", "0", "-c", "copy",
                    "-avoid_negative_ts", "make_zero",
                    "-movflags", "+faststart",
                    finalPath
                ],
                TimeSpan.FromMinutes(2),
                workingDirectory: workFolder);

            if (exitCode != 0)
            {
                Log.Write($"Anında tekrar kaydedilemedi: {log}");
                return null;
            }

            return finalPath;
        }
        finally
        {
            TryDelete(workFolder);
        }
    }

    private static int SegmentCount(int seconds) => (int)Math.Ceiling(seconds / (double)SegmentSeconds);

    private static void ResetFolder()
    {
        TryDelete(BufferFolder);
        Directory.CreateDirectory(BufferFolder);
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Klasör silinemedi ({folder}): {ex.Message}");
        }
    }

    private void OnSessionFailed(string reason)
    {
        _session?.Dispose();
        _session = null;
        Failed?.Invoke(reason);
    }

    private void OnSessionProgress(FfmpegProgress progress) => Progress?.Invoke(progress);
}
