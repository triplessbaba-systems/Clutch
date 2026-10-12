using System.Windows;
using System.Windows.Controls;
using Clutch.Core;
using Clutch.Core.Native;
using Clutch.Core.Settings;
using Clutch.Services;
using Clutch.ViewModels;
using Clutch.Views;

namespace Clutch;

public partial class App : Application
{
    private readonly Mutex _singleInstance = new(false, "Clutch.SingleInstance");

    private bool _ownsMutex;
    private SettingsStore? _store;
    private CaptureController? _controller;
    private MessageWindow? _messageWindow;
    private HotkeyService? _hotkeys;
    private TrayIconService? _tray;
    private OverlayWindow? _overlay;
    private ToastWindow? _toast;
    private ClipLibrary? _library;
    private bool _exiting;
    private bool _hotkeysSuspended;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _ownsMutex = _singleInstance.WaitOne(0, false);
        if (!_ownsMutex)
        {
            MessageBox.Show("Clutch zaten çalışıyor. Simgesi sistem tepsisinde.", "Clutch");
            Shutdown();
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Write($"Beklenmeyen hata: {args.Exception}");
            args.Handled = true;
        };

        _store = new SettingsStore();
        var settings = _store.Load();
        settings.StartWithWindows = StartupRegistration.IsEnabled();
        if (settings.StartWithWindows)
        {
            StartupRegistration.RefreshPathIfEnabled();
        }

        _controller = new CaptureController(settings, _store);

        _messageWindow = new MessageWindow();
        _toast = new ToastWindow();
        _controller.ToastRequested += _toast.ShowMessage;

        _library = new ClipLibrary(_controller);

        // Clutch'ın ana penceresi yok: her şey overlay'den yapılır
        _overlay = new OverlayWindow();
        _overlay.Bind(
            new OverlayViewModel(_controller, _library, () => _overlay.Hide()),
            () => _controller.Settings,
            ApplySettingsAsync,
            SetHotkeysSuspended,
            () => _ = ExitAsync());

        _ = new RecordingIndicatorWindow(_controller);

        _tray = new TrayIconService(_messageWindow) { MenuFactory = BuildTrayMenu };
        _tray.LeftClicked += () => _overlay.Toggle();
        _controller.StateChanged += UpdateTray;

        _hotkeys = new HotkeyService(_messageWindow);
        RegisterHotkeys();

        await _controller.InitializeAsync();
        _library.Refresh();

        _toast.ShowMessage(new ToastMessage(
            "Clutch hazır",
            $"Açmak için {_controller.Settings.Hotkeys.ToggleOverlay}",
            ToastKind.Info));
    }

    private async Task ApplySettingsAsync(AppSettings updated)
    {
        await _controller!.ApplySettingsAsync(updated);
        _hotkeysSuspended = false;
        RegisterHotkeys();
        UpdateTray();
    }

    private void RegisterHotkeys()
    {
        var hk = _controller!.Settings.Hotkeys;
        _hotkeys!.UnregisterAll();

        var failed = new List<string>();
        void Add(string text, Action action)
        {
            if (!_hotkeys.Register(text, action))
            {
                failed.Add(text);
            }
        }

        Add(hk.ToggleOverlay, () => _overlay!.Toggle());
        Add(hk.SaveReplay, () => _ = _controller.SaveReplayAsync());
        Add(hk.ToggleReplay, () => _ = _controller.ToggleReplayAsync());
        Add(hk.ToggleRecording, () => _ = _controller.ToggleRecordingAsync());
        Add(hk.Screenshot, () => _ = _controller.TakeScreenshotAsync());

        if (failed.Count > 0)
        {
            Log.Write($"Kısayol kaydedilemedi: {string.Join(", ", failed)}");
            _toast!.ShowMessage(new ToastMessage(
                "Kısayol kaydedilemedi",
                $"{string.Join(", ", failed)} başka bir uygulamada kullanılıyor. Overlay > Ayarlar > Kısayollar'dan değiştir.",
                ToastKind.Error));
            return;
        }

        // NVIDIA klavye kancası kullandığı için RegisterHotKey çakışmayı bildirmez; listeden kontrol edilir
        var conflict = hk.All.Select(h => (Key: h, Owner: KnownShortcuts.Find(h))).FirstOrDefault(x => x.Owner is not null);
        if (conflict.Owner is not null)
        {
            _toast!.ShowMessage(new ToastMessage(
                "Kısayol çakışması",
                $"{conflict.Key}, {conflict.Owner} ile aynı. Overlay > Ayarlar > Kısayollar'dan değiştir.",
                ToastKind.Error));
        }
    }

    /// <summary>Ayarlarda yeni kısayol atanırken mevcut kısayollar devre dışı kalır, yoksa basılan tuş eylemi tetikler.</summary>
    private void SetHotkeysSuspended(bool suspended)
    {
        if (suspended == _hotkeysSuspended)
        {
            return;
        }

        _hotkeysSuspended = suspended;
        if (suspended)
        {
            _hotkeys?.UnregisterAll();
        }
        else
        {
            RegisterHotkeys();
        }
    }

    private void UpdateTray()
    {
        var c = _controller!;
        var (state, tip) = c.IsRecording
            ? (TrayState.Recording, "Clutch · Kayıt sürüyor")
            : c.IsReplayActive
                ? (TrayState.ReplayActive, $"Clutch · Anında tekrar açık ({c.ReplayLengthText})")
                : (TrayState.Idle, "Clutch");

        _tray?.Update(state, tip);
    }

    private ContextMenu BuildTrayMenu()
    {
        var c = _controller!;
        var menu = new ContextMenu();

        MenuItem Item(string header, Action action, bool enabled = true, string? gesture = null)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled, InputGestureText = gesture ?? "" };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
            return item;
        }

        Item("Overlay'i aç", () => _overlay!.Toggle(), gesture: c.Settings.Hotkeys.ToggleOverlay);
        menu.Items.Add(new Separator());
        Item(c.IsReplayActive ? "Anında tekrarı kapat" : "Anında tekrarı aç", () => _ = c.ToggleReplayAsync(), c.IsReady,
            c.Settings.Hotkeys.ToggleReplay);
        Item("Tekrarı kaydet", () => _ = c.SaveReplayAsync(), c.IsReplayActive, c.Settings.Hotkeys.SaveReplay);
        Item(c.IsRecording ? "Kaydı durdur" : "Kaydı başlat", () => _ = c.ToggleRecordingAsync(), c.IsReady,
            c.Settings.Hotkeys.ToggleRecording);
        Item("Ekran görüntüsü", () => _ = c.TakeScreenshotAsync(), c.IsReady, c.Settings.Hotkeys.Screenshot);
        menu.Items.Add(new Separator());
        Item("Kayıt klasörünü aç", c.OpenGallery);
        menu.Items.Add(new Separator());
        Item("Çıkış", () => _ = ExitAsync());

        return menu;
    }

    private async Task ExitAsync()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        if (_controller is not null)
        {
            await _controller.ShutdownAsync();
        }

        _overlay?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeys?.Dispose();
        _tray?.Dispose();
        _messageWindow?.Dispose();

        if (_ownsMutex)
        {
            _singleInstance.ReleaseMutex();
        }

        _singleInstance.Dispose();
        base.OnExit(e);
    }
}
