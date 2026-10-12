using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Clutch.Core.Native;
using Clutch.Services;

namespace Clutch.Views;

/// <summary>Sağ üstte beliren kısa bildirim; kaydedilen klibin küçük resmini gösterir, tıklanınca klibi açar. Odak çalmaz, tıklamaları geçirir ve kayıtlara girmez.</summary>
public partial class ToastWindow : Window
{
    private const double ScreenMargin = 24;
    private string? _openPath;
    private readonly DispatcherTimer _hideTimer = new() { Interval = TimeSpan.FromSeconds(3) };

    public ToastWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            WindowStyles.MakeNonActivating(this);
            WindowStyles.ExcludeFromCapture(this);
        };
        _hideTimer.Tick += (_, _) => FadeOut();
        MouseLeftButtonUp += (_, _) => OpenClip();
    }

    public void ShowMessage(ToastMessage message)
    {
        _openPath = message.OpenPath;
        Cursor = _openPath is null ? null : System.Windows.Input.Cursors.Hand;
        TitleText.Text = message.Title;
        DetailText.Text = message.Detail ?? "";
        DetailText.Visibility = string.IsNullOrEmpty(message.Detail) ? Visibility.Collapsed : Visibility.Visible;
        var thumb = LoadThumbnail(message.ImagePath);
        Thumb.Source = thumb;
        ThumbFrame.Visibility = thumb is null ? Visibility.Collapsed : Visibility.Visible;

        // Küçük resim yoksa solda uygulama işareti; hata bildiriminde onun yerine kırmızı uyarı simgesi
        Mark.Visibility = thumb is null ? Visibility.Visible : Visibility.Collapsed;
        var isError = message.Kind == ToastKind.Error;
        Logo.Visibility = isError ? Visibility.Collapsed : Visibility.Visible;
        ErrorGlyph.Visibility = isError ? Visibility.Visible : Visibility.Collapsed;

        var wasVisible = IsVisible && Opacity > 0;
        BeginAnimation(OpacityProperty, null);
        Opacity = 1;
        Show();
        UpdateLayout();

        if (!wasVisible)
        {
            Offset.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }

        // NVIDIA gibi sağ üstte; kayıt göstergesinin altında kalır
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - ScreenMargin;
        Top = area.Top + ScreenMargin + 36;

        _hideTimer.Interval = TimeSpan.FromSeconds(message.Kind == ToastKind.Error ? 6 : 3);
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    private void OpenClip()
    {
        if (_openPath is not null && File.Exists(_openPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_openPath) { UseShellExecute = true });
        }

        FadeOut();
    }

    private static ImageSource? LoadThumbnail(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 224;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void FadeOut()
    {
        _hideTimer.Stop();
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(250));
        fade.Completed += (_, _) =>
        {
            if (Opacity == 0)
            {
                Hide();
            }
        };
        BeginAnimation(OpacityProperty, fade);
    }
}
