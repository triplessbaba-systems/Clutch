using Clutch.Core.Native;

namespace Clutch.Core.Capture;

/// <summary>NVIDIA düzeni: Videolar\Clutch\{Oyun}\{Oyun} 2026.10.11 - 23.15.42.mp4</summary>
public static class OutputPaths
{
    public static string For(string outputFolder, string extension, string? suffix = null)
    {
        var app = Sanitize(ForegroundApp.GetName());
        var stamp = DateTime.Now.ToString("yyyy.MM.dd - HH.mm.ss");
        var name = suffix is null ? $"{app} {stamp}" : $"{app} {stamp} {suffix}";

        var path = Path.Combine(outputFolder, app, name + extension);
        EnsureWritable(Path.GetDirectoryName(path)!);
        return path;
    }

    /// <summary>
    /// Windows Defender "Denetimli klasör erişimi" açıksa Videolar klasörüne yazma engellenir ve
    /// CreateDirectory anlamsız bir FileNotFoundException fırlatır. Kullanıcıya ne yapacağını söyleyen bir hataya çevrilir.
    /// </summary>
    private static void EnsureWritable(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is FileNotFoundException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                "Kayıt klasörüne yazılamıyor. Windows Güvenliği > Fidye yazılımı koruması'nda Clutch'e izin ver ya da ayarlardan klasörü değiştir.",
                ex);
        }
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return clean.Length == 0 ? "Masaüstü" : clean;
    }
}
