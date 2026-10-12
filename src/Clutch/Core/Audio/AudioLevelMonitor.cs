using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Clutch.Core.Audio;

/// <summary>
/// Mikserdeki seviye göstergesi için sesi dinler (kaydetmez). Ana pencere açıkken çalışır.
/// Gösterilen seviye mikserin ses ayarı uygulanmış halidir; yani kayda giren seviyedir (OBS'deki gibi).
/// </summary>
public sealed class AudioLevelMonitor : IDisposable
{
    private readonly WasapiCapture _capture;
    private readonly AudioChannelState _state;
    private float _peak;

    private AudioLevelMonitor(WasapiCapture capture, AudioChannelState state)
    {
        _capture = capture;
        _state = state;
        _capture.DataAvailable += OnData;
        _capture.StartRecording();
    }

    public static AudioLevelMonitor ForSystemAudio() => new(new WasapiLoopbackCapture(), AudioMixer.System);

    public static AudioLevelMonitor ForMicrophone() => new(new WasapiCapture(), AudioMixer.Microphone);

    /// <summary>Son çağrıdan bu yana görülen en yüksek tepe (0–1) ve sıfırlama.</summary>
    public float TakePeak() => Interlocked.Exchange(ref _peak, 0f);

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var peak = SampleOps.Peak(e.Buffer.AsSpan(0, e.BytesRecorded), _capture.WaveFormat) * _state.EffectiveGain;

        float current;
        do
        {
            current = _peak;
            if (peak <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref _peak, peak, current) != current);
    }

    public void Dispose()
    {
        _capture.DataAvailable -= OnData;
        try
        {
            _capture.StopRecording();
        }
        catch
        {
        }

        _capture.Dispose();
    }
}
