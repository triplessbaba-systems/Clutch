using Clutch.Core.Native;
using Clutch.Core.Settings;

namespace Clutch.Services;

public sealed class HotkeyService : IDisposable
{
    private readonly MessageWindow _window;
    private readonly Dictionary<int, Action> _actions = [];
    private int _nextId = 0x5C00;

    public HotkeyService(MessageWindow window)
    {
        _window = window;
        _window.AddHook(HwndHook);
    }

    /// <summary>Başarısız olursa (geçersiz metin ya da başka uygulama kullanıyor) false döner.</summary>
    public bool Register(string text, Action action)
    {
        if (!Hotkey.TryParse(text, out var hotkey))
        {
            return false;
        }

        var id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_window.Handle, id, hotkey.NativeModifiers | NativeMethods.MOD_NOREPEAT, hotkey.VirtualKey))
        {
            return false;
        }

        _actions[id] = action;
        return true;
    }

    public void UnregisterAll()
    {
        foreach (var id in _actions.Keys)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, id);
        }

        _actions.Clear();
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && _actions.TryGetValue(wParam.ToInt32(), out var action))
        {
            action();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window.RemoveHook(HwndHook);
    }
}
