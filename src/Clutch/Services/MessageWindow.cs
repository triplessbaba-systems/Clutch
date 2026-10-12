using System.Windows.Interop;

namespace Clutch.Services;

/// <summary>Uygulamanın görünür ana penceresi yok; kısayol ve tepsi mesajları bu gizli pencereye gelir.</summary>
public sealed class MessageWindow : IDisposable
{
    private readonly HwndSource _source;

    public MessageWindow()
    {
        _source = new HwndSource(new HwndSourceParameters("ClutchMessageWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0
        });
    }

    public IntPtr Handle => _source.Handle;

    public void AddHook(HwndSourceHook hook) => _source.AddHook(hook);

    public void RemoveHook(HwndSourceHook hook) => _source.RemoveHook(hook);

    public void Dispose() => _source.Dispose();
}
