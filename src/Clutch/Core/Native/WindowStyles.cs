using System.Windows;
using System.Windows.Interop;

namespace Clutch.Core.Native;

internal static class WindowStyles
{
    /// <summary>
    /// Pencere ekranda görünür ama kayıtlara ve ekran görüntülerine girmez (Windows 10 2004+).
    /// README için overlay'in ekran görüntüsünü almak gerekirse CLUTCH_SHOW_IN_CAPTURE=1 ile kapatılır.
    /// </summary>
    public static void ExcludeFromCapture(Window window)
    {
        if (Environment.GetEnvironmentVariable("CLUTCH_SHOW_IN_CAPTURE") == "1")
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
    }

    /// <summary>Odak çalmayan ve Alt+Tab'da görünmeyen ama tıklanabilen pencere (bildirim).</summary>
    public static void MakeNonActivating(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>Odak çalmayan, Alt+Tab'da görünmeyen ve tıklamaları alttaki pencereye geçiren bindirme.</summary>
    public static void MakeClickThroughOverlay(Window window)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE).ToInt64();
        style |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }
}
