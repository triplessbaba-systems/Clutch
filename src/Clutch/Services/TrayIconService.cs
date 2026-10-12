using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using Clutch.Core.Native;

namespace Clutch.Services;

public enum TrayState
{
    Idle,
    ReplayActive,
    Recording
}

/// <summary>Tepsi simgesi: uygulama logosu; ortadaki nokta durumu gösterir (gri boşta, yeşil tekrar açık, kırmızı kayıtta).</summary>
public sealed class TrayIconService : IDisposable
{
    private const uint TrayIconId = 1;

    private readonly MessageWindow _window;
    private NativeMethods.NOTIFYICONDATA _nid;
    private IntPtr _hIcon;

    public event Action? LeftClicked;

    /// <summary>Sağ tıklamada gösterilecek menüyü üretir; her açılışta güncel durumla yeniden kurulur.</summary>
    public Func<ContextMenu>? MenuFactory { get; set; }

    public TrayIconService(MessageWindow window)
    {
        _window = window;
        _window.AddHook(HwndHook);

        _hIcon = CreateDotIcon(TrayState.Idle);
        _nid = new NativeMethods.NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
            hWnd = _window.Handle,
            uID = TrayIconId,
            uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON | NativeMethods.NIF_TIP,
            uCallbackMessage = NativeMethods.WM_TRAYICON,
            hIcon = _hIcon,
            szTip = "Clutch"
        };

        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref _nid);
    }

    public void Update(TrayState state, string tooltip)
    {
        var old = _hIcon;
        _hIcon = CreateDotIcon(state);
        _nid.hIcon = _hIcon;
        _nid.szTip = tooltip.Length > 127 ? tooltip[..127] : tooltip;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref _nid);

        if (old != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(old);
        }
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_TRAYICON || wParam.ToInt32() != (int)TrayIconId)
        {
            return IntPtr.Zero;
        }

        switch (lParam.ToInt32())
        {
            case NativeMethods.WM_LBUTTONUP:
                LeftClicked?.Invoke();
                handled = true;
                break;
            case NativeMethods.WM_RBUTTONUP:
                ShowMenu();
                handled = true;
                break;
        }

        return IntPtr.Zero;
    }

    private void ShowMenu()
    {
        if (MenuFactory?.Invoke() is not { } menu)
        {
            return;
        }

        menu.Placement = PlacementMode.MousePoint;
        menu.Opened += (_, _) =>
        {
            // Menünün dışına tıklanınca kapanması için menü penceresi öne alınmalı
            if (System.Windows.PresentationSource.FromVisual(menu) is HwndSource source)
            {
                NativeMethods.SetForegroundWindow(source.Handle);
            }
        };
        menu.IsOpen = true;
    }

    /// <summary>
    /// Uygulama logosu (koyu zemin, geri saran halka) ve ortasında duruma göre renklenen kayıt noktası:
    /// gri boşta, yeşil anında tekrar açık, kırmızı kayıtta. Geometri UI/Theme.xaml ile aynıdır.
    /// </summary>
    private static IntPtr CreateDotIcon(TrayState state)
    {
        var dot = state switch
        {
            TrayState.Recording => Color.FromArgb(255, 75, 78),
            TrayState.ReplayActive => Color.FromArgb(118, 185, 0),
            _ => Color.FromArgb(140, 140, 146)
        };

        const int size = 32;
        const float scale = size * 0.78f / 24f;
        const float offset = size * 0.11f;
        PointF P(float x, float y) => new(offset + x * scale, offset + y * scale);

        using var bitmap = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using (var background = RoundedRect(new RectangleF(0, 0, size - 1, size - 1), 7))
            using (var bg = new SolidBrush(Color.FromArgb(27, 27, 29)))
            {
                g.FillPath(bg, background);
            }

            var ink = Color.FromArgb(242, 242, 243);
            using (var ring = new Pen(ink, 2.2f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Flat })
            {
                var center = P(12, 12);
                var r = 9 * scale;
                g.DrawArc(ring, center.X - r, center.Y - r, 2 * r, 2 * r, 210, -300);
            }

            using (var arrow = new SolidBrush(ink))
            {
                g.FillPolygon(arrow, [P(12, 0.6f), P(8.8f, 3), P(12, 5.4f)]);
            }

            using var fill = new SolidBrush(dot);
            var c = P(12, 12);
            var dr = 3.6f * scale;
            g.FillEllipse(fill, c.X - dr, c.Y - dr, 2 * dr, 2 * dr);
        }

        return bitmap.GetHicon();
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref _nid);
        _window.RemoveHook(HwndHook);

        if (_hIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }
}
