using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Clutch.Core.Audio;

/// <summary>Bir ses kanalının mikser durumu. Kayıt sürerken değişse bile bir sonraki ses paketinden itibaren geçerlidir.</summary>
public sealed class AudioChannelState
{
    private volatile float _gain = 1f;
    private volatile bool _muted;

    public float Gain
    {
        get => _gain;
        set => _gain = Math.Clamp(value, 0f, 1f);
    }

    public bool Muted
    {
        get => _muted;
        set => _muted = value;
    }

    public float EffectiveGain => _muted ? 0f : _gain;
}

public static class AudioMixer
{
    public static AudioChannelState System { get; } = new();
    public static AudioChannelState Microphone { get; } = new();
}

/// <summary>WASAPI paylaşımlı mod biçimleri için ortak örnek işlemleri (çoğunlukla 32 bit float, bazen 16 bit PCM).</summary>
internal static class SampleOps
{
    public static WaveFormat Standard(WaveFormat format) =>
        format is WaveFormatExtensible extensible ? extensible.ToStandardWaveFormat() : format;

    public static bool IsFloat32(WaveFormat format)
    {
        var f = Standard(format);
        return f.Encoding == WaveFormatEncoding.IeeeFloat && f.BitsPerSample == 32;
    }

    public static bool IsPcm16(WaveFormat format)
    {
        var f = Standard(format);
        return f.Encoding == WaveFormatEncoding.Pcm && f.BitsPerSample == 16;
    }

    public static void ApplyGain(Span<byte> buffer, WaveFormat format, float gain)
    {
        if (gain >= 0.999f)
        {
            return;
        }

        if (IsFloat32(format))
        {
            var samples = MemoryMarshal.Cast<byte, float>(buffer);
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }
        else if (IsPcm16(format))
        {
            var samples = MemoryMarshal.Cast<byte, short>(buffer);
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)(samples[i] * gain);
            }
        }
    }

    /// <summary>0–1 arası tepe değeri.</summary>
    public static float Peak(ReadOnlySpan<byte> buffer, WaveFormat format)
    {
        var peak = 0f;
        if (IsFloat32(format))
        {
            foreach (var s in MemoryMarshal.Cast<byte, float>(buffer))
            {
                var a = Math.Abs(s);
                if (a > peak) peak = a;
            }
        }
        else if (IsPcm16(format))
        {
            foreach (var s in MemoryMarshal.Cast<byte, short>(buffer))
            {
                var a = Math.Abs(s / 32768f);
                if (a > peak) peak = a;
            }
        }

        return Math.Min(peak, 1f);
    }
}
