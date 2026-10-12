using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Clutch.UI.Controls;

/// <summary>
/// Kırpma şeridi: iki tutamaçla başlangıç/bitiş seçilir, seçili aralık vurgulanır, oynatma çizgisi
/// gezdirilerek ya da şeride tıklanarak videoda konum değiştirilir. Tüm değerler saniye cinsinden.
/// </summary>
public sealed class TrimTimeline : FrameworkElement
{
    private const double HandleWidth = 12;
    private const double TrackInset = HandleWidth / 2;
    private const double MinLength = 0.5;

    public static readonly DependencyProperty DurationProperty = Register(nameof(Duration), 1.0);
    public static readonly DependencyProperty StartProperty = Register(nameof(Start), 0.0);
    public static readonly DependencyProperty EndProperty = Register(nameof(End), 1.0);
    public static readonly DependencyProperty PositionProperty = Register(nameof(Position), 0.0);

    private static readonly Brush TrackBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x2C, 0x2C, 0x30)));
    private static readonly Brush RangeBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0x76, 0xB9, 0x00)));
    private static readonly Brush AccentBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x76, 0xB9, 0x00)));
    private static readonly Brush HandleBrush = Freeze(new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF3)));
    private static readonly Brush GripBrush = Freeze(new SolidColorBrush(Color.FromRgb(0x5A, 0x5A, 0x60)));
    private static readonly Pen PlayheadPen = Freeze(new Pen(Brushes.White, 2));

    private enum DragTarget { None, Start, End, Playhead }

    private DragTarget _drag;

    /// <summary>Kullanıcı şeride tıklayıp oynatma konumunu değiştirdiğinde (saniye).</summary>
    public event Action<double>? SeekRequested;

    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public double Start
    {
        get => (double)GetValue(StartProperty);
        set => SetValue(StartProperty, value);
    }

    public double End
    {
        get => (double)GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    public double Position
    {
        get => (double)GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width, 44);

    protected override void OnRender(DrawingContext dc)
    {
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= HandleWidth * 2)
        {
            return;
        }

        // Tıklama her yerde algılansın diye görünmez zemin
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

        var trackTop = 8.0;
        var trackHeight = h - 16;
        dc.DrawRoundedRectangle(TrackBrush, null, new Rect(TrackInset, trackTop, w - HandleWidth, trackHeight), 4, 4);

        var startX = ToX(Start);
        var endX = ToX(End);
        dc.DrawRectangle(RangeBrush, null, new Rect(startX, trackTop, Math.Max(0, endX - startX), trackHeight));
        dc.DrawRectangle(AccentBrush, null, new Rect(startX, trackTop, Math.Max(0, endX - startX), 2));
        dc.DrawRectangle(AccentBrush, null, new Rect(startX, trackTop + trackHeight - 2, Math.Max(0, endX - startX), 2));

        DrawHandle(dc, startX, h);
        DrawHandle(dc, endX, h);

        var playX = ToX(Position);
        dc.DrawLine(PlayheadPen, new Point(playX, 2), new Point(playX, h - 2));
        dc.DrawEllipse(Brushes.White, null, new Point(playX, 3), 4, 4);
    }

    private static void DrawHandle(DrawingContext dc, double x, double h)
    {
        dc.DrawRoundedRectangle(HandleBrush, null, new Rect(x - HandleWidth / 2, 4, HandleWidth, h - 8), 3, 3);
        dc.DrawRectangle(GripBrush, null, new Rect(x - 0.75, h / 2 - 6, 1.5, 12));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        var x = e.GetPosition(this).X;

        _drag = HitHandle(x) switch
        {
            DragTarget.Start => DragTarget.Start,
            DragTarget.End => DragTarget.End,
            _ => DragTarget.Playhead
        };

        if (_drag == DragTarget.Playhead)
        {
            Seek(ToTime(x));
        }

        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var x = e.GetPosition(this).X;

        if (_drag == DragTarget.None)
        {
            Cursor = HitHandle(x) == DragTarget.None ? Cursors.Hand : Cursors.SizeWE;
            return;
        }

        var t = ToTime(x);
        switch (_drag)
        {
            case DragTarget.Start:
                Start = Math.Clamp(t, 0, End - MinLength);
                Seek(Start);
                break;
            case DragTarget.End:
                End = Math.Clamp(t, Start + MinLength, Duration);
                Seek(End);
                break;
            case DragTarget.Playhead:
                Seek(t);
                break;
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _drag = DragTarget.None;
        ReleaseMouseCapture();
    }

    private void Seek(double t)
    {
        Position = Math.Clamp(t, 0, Duration);
        SeekRequested?.Invoke(Position);
    }

    private DragTarget HitHandle(double x)
    {
        if (Math.Abs(x - ToX(Start)) <= HandleWidth)
        {
            return DragTarget.Start;
        }

        return Math.Abs(x - ToX(End)) <= HandleWidth ? DragTarget.End : DragTarget.None;
    }

    private double ToX(double seconds)
    {
        var usable = ActualWidth - HandleWidth;
        return TrackInset + (Duration <= 0 ? 0 : Math.Clamp(seconds / Duration, 0, 1) * usable);
    }

    private double ToTime(double x)
    {
        var usable = ActualWidth - HandleWidth;
        return usable <= 0 ? 0 : Math.Clamp((x - TrackInset) / usable, 0, 1) * Duration;
    }

    private static DependencyProperty Register(string name, double defaultValue) =>
        DependencyProperty.Register(name, typeof(double), typeof(TrimTimeline),
            new FrameworkPropertyMetadata(defaultValue,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    private static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
