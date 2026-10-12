using NAudio.CoreAudioApi;

namespace Clutch.Core.Audio;

public static class AudioDevices
{
    /// <summary>Windows'ta varsayılan bir kayıt (mikrofon) cihazı var mı.</summary>
    public static bool HasMicrophone()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
        }
        catch
        {
            return false;
        }
    }
}
