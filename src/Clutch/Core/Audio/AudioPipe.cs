using System.IO.Pipes;
using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Clutch.Core.Audio;

/// <summary>
/// WASAPI ile yakalanan sesi isimli boru (named pipe) üzerinden ffmpeg'e ham PCM olarak aktarır.
/// ffmpeg'in kendi Windows ses girişi (dshow) sistem sesini (loopback) yakalayamadığı için bu yol seçildi.
/// </summary>
public sealed class AudioPipe : IDisposable
{
    private readonly WasapiCapture _capture;
    private readonly NamedPipeServerStream _pipe;
    private readonly Channel<byte[]> _queue = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _cts = new();
    private readonly WasapiOut? _silenceKeepAlive;
    private readonly string _name;
    private readonly AudioChannelState _state;

    private AudioPipe(WasapiCapture capture, string kind, WasapiOut? silenceKeepAlive, AudioChannelState state)
    {
        _capture = capture;
        _silenceKeepAlive = silenceKeepAlive;
        _state = state;
        _name = $"clutch_{kind}_{Guid.NewGuid():N}";
        _pipe = new NamedPipeServerStream(_name, PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 1 << 20);

        var format = capture.WaveFormat;
        SampleRate = format.SampleRate;
        Channels = format.Channels;
        FfmpegSampleFormat = ToFfmpegFormat(format);

        _capture.DataAvailable += OnDataAvailable;
    }

    public string PipePath => $@"\\.\pipe\{_name}";
    public int SampleRate { get; }
    public int Channels { get; }
    public string FfmpegSampleFormat { get; }

    /// <summary>Hoparlörden çıkan ses (oyun, Discord, müzik).</summary>
    public static AudioPipe ForSystemAudio()
    {
        // Hiçbir şey çalmıyorken WASAPI loopback veri göndermeyi keser ve ses videodan kayar.
        // Varsayılan cihaza sürekli sessizlik çalarak akış canlı tutulur.
        var keepAlive = new WasapiOut(AudioClientShareMode.Shared, 100);
        using (var enumerator = new MMDeviceEnumerator())
        using (var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
        {
            keepAlive.Init(new SilenceProvider(device.AudioClient.MixFormat));
        }

        keepAlive.Play();

        return new AudioPipe(new WasapiLoopbackCapture(), "sys", keepAlive, AudioMixer.System);
    }

    /// <summary>Varsayılan mikrofon.</summary>
    public static AudioPipe ForMicrophone() => new(new WasapiCapture(), "mic", null, AudioMixer.Microphone);

    /// <summary>ffmpeg boruya bağlandığında yakalamayı başlatır. ffmpeg başlatılmadan önce çağrılmalı.</summary>
    public void BeginListening() => _ = Task.Run(PumpAsync);

    private async Task PumpAsync()
    {
        try
        {
            await _pipe.WaitForConnectionAsync(_cts.Token);
            _capture.StartRecording();

            await foreach (var buffer in _queue.Reader.ReadAllAsync(_cts.Token))
            {
                await _pipe.WriteAsync(buffer, _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
            // ffmpeg kapandı, boru koptu
        }
        catch (Exception ex)
        {
            Log.Write($"Ses borusu hatası ({_name}): {ex.Message}");
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded > 0)
        {
            // Mikserdeki ses seviyesi / sessize alma burada, ffmpeg'e gitmeden uygulanır
            var copy = e.Buffer.AsSpan(0, e.BytesRecorded).ToArray();
            SampleOps.ApplyGain(copy, _capture.WaveFormat, _state.EffectiveGain);
            _queue.Writer.TryWrite(copy);
        }
    }

    private static string ToFfmpegFormat(WaveFormat format)
    {
        var standard = SampleOps.Standard(format);

        return (standard.Encoding, standard.BitsPerSample) switch
        {
            (WaveFormatEncoding.IeeeFloat, 32) => "f32le",
            (WaveFormatEncoding.Pcm, 16) => "s16le",
            (WaveFormatEncoding.Pcm, 24) => "s24le",
            (WaveFormatEncoding.Pcm, 32) => "s32le",
            _ => throw new NotSupportedException($"Desteklenmeyen ses biçimi: {format}")
        };
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queue.Writer.TryComplete();
        _capture.DataAvailable -= OnDataAvailable;

        try
        {
            _capture.StopRecording();
        }
        catch
        {
        }

        _capture.Dispose();
        _silenceKeepAlive?.Stop();
        _silenceKeepAlive?.Dispose();
        _pipe.Dispose();
        _cts.Dispose();
    }
}
