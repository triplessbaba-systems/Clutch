using System.Windows;
using System.Windows.Media;

namespace Clutch.UI.Controls;

/// <summary>
/// İnce, yuvarlak uçlu ses seviyesi çubuğu. -60 dB ile 0 dB arası; normalde yeşil, -9 dB üstü sarı, -3 dB üstü kırmızı.
/// Tepe değeri ince bir çizgiyle kısa süre tutulur.
/// </summary>
public sealed class LevelMeter : FrameworkElement
{
    public const double MinDb = -60;
    private const double WarnDb = -9;
    private const double ClipDb = -3;

    public static readonly DependencyProperty LevelDbProperty = DependencyProperty.Register(
        nameof(LevelDb), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(MinDb, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PeakDbProperty = DependencyProperty.Register(
        nameof(PeakDb), typeof(double), typeof(LevelMeter),
        new FrameworkPropertyMetadata(MinDb, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Brush Track = Freeze(new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2E)));
    private static readonly Brush Normal = Freeze(new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00)));
    private static readonly Brush Warn = Freeze(new SolidColorBrush(Color.FromRgb(0xF0, 0xB4, 0x29)));
    private static readonly Brush ClipBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0x4B, 0x4E)));
    private static readonly Brush PeakBrush = Freeze(new SolidColorBrush(Color.FromArgb(0xCC, 0xF2, 0xF2, 0xF3)));

    public double LevelDb
    {
        get => (double)GetValue(LevelDbProperty);
        set => SetValue(LevelDbProperty, value);
    }

    public double PeakDb
    {
        get => (double)GetValue(PeakDbProperty);
        set => SetValue(PeakDbProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 200 : availableSize.Width, 4);

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0)
        {
            return;
        }

        var radius = h / 2;
        dc.DrawRoundedRectangle(Track, null, new Rect(0, 0, w, h), radius, radius);

        var levelX = ToX(LevelDb, w);
        if (levelX > 0.5)
        {
            var fill = LevelDb >= ClipDb ? ClipBrush : LevelDb >= WarnDb ? Warn : Normal;
            dc.DrawRoundedRectangle(fill, null, new Rect(0, 0, Math.Max(levelX, h), h), radius, radius);
        }

        if (PeakDb > MinDb + 1)
        {
            var peakX = Math.Clamp(ToX(PeakDb, w), 1, w - 2);
            dc.DrawRectangle(PeakBrush, null, new Rect(peakX, 0, 1.5, h));
        }
    }

    private static double ToX(double db, double width) => Math.Clamp((db - MinDb) / -MinDb, 0, 1) * width;

    private static Brush Freeze(Brush b)
    {
        b.Freeze();
        return b;
    }
}
