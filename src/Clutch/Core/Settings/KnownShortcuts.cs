namespace Clutch.Core.Settings;

/// <summary>
/// Başka uygulamaların varsayılan kısayolları. NVIDIA ve Steam tuşları RegisterHotKey ile değil klavye kancasıyla
/// dinler; bu yüzden aynı tuşu almak hata vermez, iki uygulama birden tetiklenir. Çakışmayı ancak böyle bir listeyle yakalarız.
/// </summary>
public static class KnownShortcuts
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Alt+Z"] = "NVIDIA overlay",
        ["Alt+F1"] = "NVIDIA ekran görüntüsü",
        ["Alt+F2"] = "NVIDIA fotoğraf modu",
        ["Alt+F3"] = "NVIDIA oyun filtreleri",
        ["Alt+F9"] = "NVIDIA kayıt",
        ["Alt+F10"] = "NVIDIA anında tekrar",
        ["Alt+Shift+F10"] = "NVIDIA anında tekrar aç/kapat",
        ["Alt+F12"] = "NVIDIA FPS sayacı",
        ["Alt+R"] = "NVIDIA performans paneli",
        ["Alt+F4"] = "Windows pencere kapatma",
        ["Win+G"] = "Xbox Game Bar",
        ["Alt+Win+R"] = "Xbox Game Bar kayıt",
        ["Alt+Win+G"] = "Xbox Game Bar tekrar",
        ["Alt+Win+PrintScreen"] = "Xbox Game Bar ekran görüntüsü",
        ["Shift+Win+S"] = "Windows Ekran Alıntısı",
        ["F12"] = "Steam ekran görüntüsü",
        ["Shift+Tab"] = "Steam overlay",
        ["Ctrl+Shift+M"] = "Discord mikrofon",
        ["Ctrl+Shift+D"] = "Discord ses kapatma"
    };

    /// <summary>Çakışan uygulamanın adını döner, yoksa null.</summary>
    public static string? Find(string hotkeyText) =>
        Hotkey.TryParse(hotkeyText, out var hotkey) && Map.TryGetValue(hotkey.ToString(), out var owner) ? owner : null;
}
