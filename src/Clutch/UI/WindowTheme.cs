using System.Windows;

namespace Clutch.UI;

/// <summary>
/// Overlay'deki açılır liste, kaydırıcı, metin kutusu gibi kontroller için WPF'in koyu Fluent temasını yükler ve
/// vurgu rengini Windows'unki (çoğunlukla mavi) yerine Clutch yeşiline çeker.
/// </summary>
internal static class WindowTheme
{
    private static readonly Uri FluentDark =
        new("pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.Dark.xaml", UriKind.Absolute);

    private static readonly ResourceDictionary Accent = new()
    {
        Source = new Uri("pack://application:,,,/Clutch;component/UI/FluentAccent.xaml", UriKind.Absolute)
    };

    public static void ApplyFluent(ResourceDictionary target)
    {
        target.MergedDictionaries.Add(new ResourceDictionary { Source = FluentDark });

        // Doğrudan girdi olarak eklenir: aynı sözlükte birleştirilmiş Fluent girdilerinden önceliklidir
        foreach (var key in Accent.Keys)
        {
            target[key] = Accent[key];
        }
    }
}
