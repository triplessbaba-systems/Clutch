using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Clutch.Core.Settings;

namespace Clutch.UI.Controls;

/// <summary>
/// Tıklayıp tuş kombinasyonuna basarak kısayol atanan kutu. Kayıt sırasında genel kısayollar askıya alınmalı
/// (RecordingChanged), yoksa zaten atanmış bir kombinasyona basınca kutu yerine uygulama eylemi tetiklenir.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(string),
        typeof(HotkeyBox),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (d, _) => ((HotkeyBox)d).ShowValue()));

    private bool _recording;

    public event Action<bool>? RecordingChanged;
    public event Action? ValueChanged;

    public HotkeyBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        Cursor = Cursors.Hand;
        ContextMenu = null;
        FontWeight = FontWeights.SemiBold;

        // Türetilmiş sınıflar TextBox'ın örtük (Fluent) stilini almaz; açıkça bağlanmalı
        SetResourceReference(StyleProperty, typeof(TextBox));
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        _recording = true;
        Text = "Tuşlara bas…";
        RecordingChanged?.Invoke(true);
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        _recording = false;
        ShowValue();
        RecordingChanged?.Invoke(false);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;

        // Alt basılıyken WPF tuşu Key.System olarak verir, asıl tuş SystemKey'dedir
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;

        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            Keyboard.ClearFocus();
            return;
        }

        if (Hotkey.FromKeyPress(key, modifiers) is not { } hotkey)
        {
            // Yalnızca değiştiriciler basılı: "Ctrl+Alt+…" diye ara durum göster
            var partial = new Hotkey(modifiers, Key.None).ToString();
            Text = modifiers == ModifierKeys.None ? "Tuşlara bas…" : partial.Replace("None", "…");
            return;
        }

        Value = hotkey.ToString();
        ValueChanged?.Invoke();
        Keyboard.ClearFocus();
    }

    private void ShowValue()
    {
        if (!_recording)
        {
            Text = Value;
        }
    }
}
