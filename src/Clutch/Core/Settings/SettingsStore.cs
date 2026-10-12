using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clutch.Core.Settings;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Elle okunabilsin: "Ctrl+Alt+F5" "+" olarak, Türkçe karakterli yollar "ü" olarak yazılmasın
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string AppDataFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Clutch");

    public string FilePath { get; } = Path.Combine(AppDataFolder, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return Migrate(Deserialize(File.ReadAllText(FilePath)));
            }
        }
        catch (Exception ex)
        {
            Log.Write($"Ayarlar okunamadı, varsayılanlar kullanılıyor: {ex.Message}");
        }

        return new AppSettings { SettingsVersion = AppSettings.CurrentVersion };
    }

    /// <summary>v1'in NVIDIA ile çakışan varsayılan kısayollarını yenileriyle değiştirir; kullanıcının kendi seçtiklerine dokunmaz.</summary>
    private static AppSettings Migrate(AppSettings settings)
    {
        if (settings.SettingsVersion is >= AppSettings.CurrentVersion)
        {
            return settings;
        }

        var hk = settings.Hotkeys;
        var fresh = new HotkeySettings();
        if (hk.ToggleOverlay == "Alt+Z") hk.ToggleOverlay = fresh.ToggleOverlay;
        if (hk.Screenshot == "Alt+F1") hk.Screenshot = fresh.Screenshot;
        if (hk.ToggleRecording == "Alt+F9") hk.ToggleRecording = fresh.ToggleRecording;
        if (hk.SaveReplay == "Alt+F10") hk.SaveReplay = fresh.SaveReplay;
        if (hk.ToggleReplay == "Alt+Shift+F10") hk.ToggleReplay = fresh.ToggleReplay;

        settings.SettingsVersion = AppSettings.CurrentVersion;
        Log.Write("Ayarlar v2'ye taşındı (NVIDIA ile çakışan kısayollar değiştirildi).");
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDataFolder);
        File.WriteAllText(FilePath, Serialize(settings));
    }

    internal static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, JsonOptions);

    internal static AppSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
}
