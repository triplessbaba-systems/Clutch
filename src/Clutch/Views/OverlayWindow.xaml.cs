using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Clutch.Core.Native;
using Clutch.Core.Settings;
using Clutch.UI;
using Clutch.ViewModels;

namespace Clutch.Views;

/// <summary>
/// Clutch'ın tek penceresi: overlay kısayoluyla açılır, ekranı karartır. Üstte eylem şeridi, solda galeri,
/// sağda Ses ve Kaynaklar / Ayarlar. Kayıtlara ve ekran görüntülerine girmez.
/// </summary>
public partial class OverlayWindow : Window
{
    private OverlayViewModel? _viewModel;

    public OverlayWindow()
    {
        // Açılır liste, kaydırıcı, metin kutusu, kaydırma çubuğu için koyu Fluent teması + Clutch yeşili.
        // XAML'daki kaynaklar bu sözlüğe eklenir; önce yüklenince stillerdeki BasedOn başvuruları da onu görür.
        WindowTheme.ApplyFluent(Resources);
        InitializeComponent();

        SourceInitialized += (_, _) => WindowStyles.ExcludeFromCapture(this);
        IsVisibleChanged += (_, _) => _viewModel?.SetActive(IsVisible);
    }

    public void Bind(OverlayViewModel viewModel, Func<AppSettings> currentSettings, Func<AppSettings, Task> applySettings,
        Action<bool> suspendHotkeys, Action exit)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        Settings.Initialize(currentSettings, applySettings, suspendHotkeys, exit);

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OverlayViewModel.TrimTarget) && viewModel.TrimTarget is { } clip)
            {
                Trim.Open(clip, viewModel.Controller.TrimClipAsync, () => viewModel.TrimTarget = null);
            }
        };

        // Overlay kapanınca video durur ve dosya kilidi kalkar
        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible && viewModel.IsTrimming)
            {
                Trim.Release();
                viewModel.TrimTarget = null;
            }
        };
    }

    public void Toggle()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        // Klip klasör adı için overlay'den önceki uygulamayı (oyunu) hatırla
        ForegroundApp.Remember();
        Settings.Reload();

        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        var duration = TimeSpan.FromMilliseconds(160);
        Opacity = 0;
        Show();
        Activate();
        BeginAnimation(OpacityProperty, new DoubleAnimation(1, duration));
        CardOffset.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(-16, 0, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        // Kırpma ekranındayken Esc önce galeriye döner
        if (_viewModel is { IsTrimming: true })
        {
            Trim.Release();
            _viewModel.TrimTarget = null;
            return;
        }

        Hide();
    }

    private void OnBackdropClick(object sender, MouseButtonEventArgs e) => Hide();

    private void OnPanelClick(object sender, MouseButtonEventArgs e) => e.Handled = true;

    private void OnVolumeDragCompleted(object sender, DragCompletedEventArgs e) => _viewModel?.SaveMixerCommand.Execute(null);
}
