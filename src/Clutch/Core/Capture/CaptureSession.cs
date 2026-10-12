using Clutch.Core.Audio;

namespace Clutch.Core.Capture;

/// <summary>Ekran + ses yakalayan tek bir ffmpeg süreci. Kayıt ve anında tekrar yalnızca çıkış argümanlarında ayrışır.</summary>
public sealed class CaptureSession : IDisposable
{
    private readonly FfmpegProcess _ffmpeg;
    private readonly List<AudioPipe> _audio;

    public event Action<string>? Failed;
    public event Action<FfmpegProgress>? Progress;

    private CaptureSession(FfmpegProcess ffmpeg, List<AudioPipe> audio)
    {
        _ffmpeg = ffmpeg;
        _audio = audio;
        _ffmpeg.UnexpectedExit += p => Failed?.Invoke(LastErrorLine(p.RecentLog));
        _ffmpeg.Progress += p => Progress?.Invoke(p);
    }

    public static CaptureSession Start(CaptureProfile profile, IEnumerable<string> outputArgs, string? workingDirectory = null)
    {
        var audio = new List<AudioPipe>();
        try
        {
            if (profile.SystemAudio)
            {
                TryAdd(audio, AudioPipe.ForSystemAudio, "Sistem sesi");
            }

            if (profile.Microphone)
            {
                TryAdd(audio, AudioPipe.ForMicrophone, "Mikrofon");
            }

            foreach (var pipe in audio)
            {
                pipe.BeginListening();
            }

            var args = FfmpegArgs.Build(profile, audio);
            args.AddRange(outputArgs);

            return new CaptureSession(FfmpegProcess.Start(profile.FfmpegPath, args, workingDirectory), audio);
        }
        catch
        {
            audio.ForEach(p => p.Dispose());
            throw;
        }
    }

    public async Task StopAsync()
    {
        await _ffmpeg.StopAsync(TimeSpan.FromSeconds(8));
        Dispose();
    }

    public void Dispose()
    {
        _ffmpeg.Dispose();
        _audio.ForEach(p => p.Dispose());
        _audio.Clear();
    }

    private static void TryAdd(List<AudioPipe> audio, Func<AudioPipe> factory, string label)
    {
        try
        {
            audio.Add(factory());
        }
        catch (Exception ex)
        {
            // Mikrofon takılı değilse vb. kayıt sessiz devam eder
            Log.Write($"{label} açılamadı: {ex.Message}");
        }
    }

    private static string LastErrorLine(string log)
    {
        var line = log.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return string.IsNullOrWhiteSpace(line) ? "ffmpeg beklenmedik şekilde kapandı" : line;
    }
}
