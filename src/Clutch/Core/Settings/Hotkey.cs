using System.Windows.Input;

namespace Clutch.Core.Settings;

/// <summary>"Alt+Shift+F10" gibi metinleri RegisterHotKey değerlerine çevirir.</summary>
public readonly record struct Hotkey(ModifierKeys Modifiers, Key Key)
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    public uint NativeModifiers =>
        (Modifiers.HasFlag(ModifierKeys.Alt) ? ModAlt : 0) |
        (Modifiers.HasFlag(ModifierKeys.Control) ? ModControl : 0) |
        (Modifiers.HasFlag(ModifierKeys.Shift) ? ModShift : 0) |
        (Modifiers.HasFlag(ModifierKeys.Windows) ? ModWin : 0);

    public uint VirtualKey => (uint)KeyInterop.VirtualKeyFromKey(Key);

    /// <summary>Her zaman aynı sırada yazılır: Ctrl+Alt+Shift+Win+Tuş. Karşılaştırmalar bu metin üzerinden yapılır.</summary>
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyName(Key));
        return string.Join('+', parts);
    }

    /// <summary>Ayarlardaki kısayol kutusu için: basılan tuştan kısayol üretir. Tek başına değiştirici tuşlar sayılmaz.</summary>
    public static Hotkey? FromKeyPress(Key key, ModifierKeys modifiers)
    {
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.System or Key.None)
        {
            return null;
        }

        // Oyunda yazı yazarken tetiklenmesin: F tuşları dışında en az bir değiştirici şart
        var isFunctionKey = key >= Key.F1 && key <= Key.F24;
        if (modifiers == ModifierKeys.None && !isFunctionKey)
        {
            return null;
        }

        return new Hotkey(modifiers, key);
    }

    public static string Normalize(string text) => TryParse(text, out var hotkey) ? hotkey.ToString() : text;

    private static string KeyName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        Key.Snapshot => "PrintScreen",
        Key.Next => "PageDown",
        Key.Prior => "PageUp",
        Key.Return => "Enter",
        _ => key.ToString()
    };

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        Key? key = null;

        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "alt":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "ctrl":
                case "control":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "shift":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "win":
                    modifiers |= ModifierKeys.Windows;
                    break;
                default:
                    if (key is not null)
                    {
                        return false;
                    }

                    var name = raw.Length == 1 && char.IsDigit(raw[0]) ? "D" + raw : raw;
                    if (!Enum.TryParse<Key>(name, ignoreCase: true, out var parsed))
                    {
                        return false;
                    }

                    key = parsed;
                    break;
            }
        }

        if (key is null)
        {
            return false;
        }

        hotkey = new Hotkey(modifiers, key.Value);
        return true;
    }
}
