using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Clutch.ViewModels;

namespace Clutch.Views;

/// <summary>
/// Galerideki bir klibi kırpma ekranı. Seçili aralık döngüde oynatılır; kaydet deyince orijinal korunur,
/// yanına "(kırpılmış)" kopya yazılır.
/// </summary>
public partial class TrimView : UserControl
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private Func<string, TimeSpan, TimeSpan, Task<bool>>? _save;
    private Action? _close;
    private ClipItemViewModel? _clip;
    private bool _playing;

    public TrimView()
    {
        InitializeComponent();

        _tick.Tick += (_, _) => OnTick();
        Timeline.SeekRequested += seconds => Player.Position = TimeSpan.FromSeconds(seconds);

        var startDescriptor = DependencyPropertyDescriptor.FromProperty(UI.Controls.TrimTimeline.StartProperty, typeof(UI.Controls.TrimTimeline));
        var endDescriptor = DependencyPropertyDescriptor.FromProperty(UI.Controls.TrimTimeline.EndProperty, typeof(UI.Controls.TrimTimeline));
        startDescriptor.AddValueChanged(Timeline, (_, _) => UpdateSelectionText());
        endDescriptor.AddValueChanged(Timeline, (_, _) => UpdateSelectionText());

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Space)
            {
                TogglePlayback();
                e.Handled = true;
            }
        };
    }

    public void Open(ClipItemViewModel clip, Func<string, TimeSpan, TimeSpan, Task<bool>> save, Action close)
    {
        _clip = clip;
        _save = save;
        _close = close;

        ClipName.Text = $"{clip.Game}, {clip.When.ToLowerInvariant()}";
        ErrorText.Visibility = Visibility.Collapsed;
        SaveButton.IsEnabled = false;
        SaveButton.Content = "Kırpılmış kopyayı kaydet";
        Timeline.Duration = 1;
        Timeline.Start = 0;
        Timeline.End = 1;
        Timeline.Position = 0;

        Player.Source = new Uri(clip.Path);
        Player.Play();
        Player.Pause();
        Focus();
    }

    /// <summary>Videoyu kapatır; dosya kilidi kalkar (silme / yeniden açma için).</summary>
    public void Release()
    {
        _tick.Stop();
        _playing = false;
        Player.Stop();
        Player.Source = null;
    }

    private void OnMediaOpened(object sender, RoutedEventArgs e)
    {
        var duration = Player.NaturalDuration.HasTimeSpan ? Player.NaturalDuration.TimeSpan.TotalSeconds : 0;
        Timeline.Duration = Math.Max(duration, 1);
        Timeline.Start = 0;
        Timeline.End = Timeline.Duration;
        DurationText.Text = Format(TimeSpan.FromSeconds(Timeline.Duration));
        SaveButton.IsEnabled = true;
        UpdateSelectionText();
        SetPlaying(true);
    }

    private void OnMediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Visible;
        SaveButton.IsEnabled = false;
    }

    private void OnTick()
    {
        var position = Player.Position.TotalSeconds;

        // Seçili aralık döngüde oynar
        if (_playing && (position >= Timeline.End || position < Timeline.Start - 0.25))
        {
            Player.Position = TimeSpan.FromSeconds(Timeline.Start);
            position = Timeline.Start;
        }

        Timeline.Position = position;
        PositionText.Text = Format(TimeSpan.FromSeconds(position));
    }

    private void OnPlayPause(object sender, RoutedEventArgs e) => TogglePlayback();

    private void TogglePlayback() => SetPlaying(!_playing);

    private void SetPlaying(bool playing)
    {
        _playing = playing;
        if (playing)
        {
            Player.Play();
            _tick.Start();
        }
        else
        {
            Player.Pause();
        }

        PlayButton.Content = playing ? "" : "";
    }

    private void UpdateSelectionText()
    {
        StartText.Text = Format(TimeSpan.FromSeconds(Timeline.Start));
        EndText.Text = Format(TimeSpan.FromSeconds(Timeline.End));
        LengthText.Text = Format(TimeSpan.FromSeconds(Timeline.End - Timeline.Start));
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_clip is null || _save is null)
        {
            return;
        }

        SetPlaying(false);
        SaveButton.IsEnabled = false;
        SaveButton.Content = "Kaydediliyor…";

        var ok = await _save(_clip.Path, TimeSpan.FromSeconds(Timeline.Start), TimeSpan.FromSeconds(Timeline.End));
        if (ok)
        {
            Release();
            _close?.Invoke();
            return;
        }

        SaveButton.IsEnabled = true;
        SaveButton.Content = "Kırpılmış kopyayı kaydet";
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Release();
        _close?.Invoke();
    }

    private static string Format(TimeSpan t) =>
        t.TotalHours >= 1 ? t.ToString(@"h\:mm\:ss\.f") : t.ToString(@"m\:ss\.f");
}
