using System.Diagnostics;

namespace Clutch.Core.Native;

/// <summary>Klipleri NVIDIA gibi oyun/uygulama adına göre klasörlemek için öndeki uygulamayı bulur.</summary>
internal static class ForegroundApp
{
    private const string DesktopName = "Masaüstü";
    private static string _lastExternal = DesktopName;

    /// <summary>Overlay açılmadan hemen önce çağrılır; overlay öndeyken bile doğru oyun adı kullanılır.</summary>
    public static void Remember() => _lastExternal = Resolve() ?? _lastExternal;

    public static string GetName() => Resolve() ?? _lastExternal;

    private static string? Resolve()
    {
        try
        {
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return DesktopName;
            }

            NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == Environment.ProcessId)
            {
                return null;
            }

            using var process = Process.GetProcessById((int)pid);
            if (process.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                return DesktopName;
            }

            string? description = null;
            try
            {
                description = process.MainModule?.FileVersionInfo.FileDescription;
            }
            catch
            {
                // Yönetici olarak çalışan süreçlerin modül bilgisi okunamaz
            }

            return string.IsNullOrWhiteSpace(description) ? process.ProcessName : description.Trim();
        }
        catch
        {
            return DesktopName;
        }
    }
}
