using Microsoft.Win32;

namespace Clutch.Core.Native;

/// <summary>
/// "Windows ile başlat": HKCU\...\Run altında Clutch girdisi. Yönetici izni gerekmez.
/// Doğruluk kaynağı kayıt defteridir; ayar dosyası yalnızca arayüzü besler.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Clutch";

    private static string Command => $"\"{Environment.ProcessPath}\" --minimized";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName);
        }
    }

    /// <summary>Uygulama başka klasöre taşındıysa kayıtlı yolu günceller.</summary>
    public static void RefreshPathIfEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is string current && current != Command)
        {
            key.SetValue(ValueName, Command);
        }
    }
}
