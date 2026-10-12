using System.Windows;
using Clutch.Core.Native;
using Clutch.Services;

namespace Clutch.Views;

/// <summary>Kayıt sürerken sağ üstte duran küçük "● 01:23" göstergesi. Videoya girmez.</summary>
public partial class RecordingIndicatorWindow : Window
{
    private const double ScreenMargin = 16;
    private readonly CaptureController _controller;

    public RecordingIndicatorWindow(CaptureController controller)
    {
        InitializeComponent();
        _controller = controller;
        DataContext = controller;

        SourceInitialized += (_, _) =>
        {
            WindowStyles.MakeClickThroughOverlay(this);
            WindowStyles.ExcludeFromCapture(this);
        };

        controller.StateChanged += Refresh;
    }

    private void Refresh()
    {
        if (_controller.IsRecording && _controller.Settings.ShowRecordingIndicator)
        {
            Show();
            UpdateLayout();
            Left = SystemParameters.WorkArea.Right - ActualWidth - ScreenMargin;
            Top = SystemParameters.WorkArea.Top + ScreenMargin;
        }
        else
        {
            Hide();
        }
    }
}
