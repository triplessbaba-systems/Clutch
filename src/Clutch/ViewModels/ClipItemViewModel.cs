using System.Globalization;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Clutch.Core.Capture;

namespace Clutch.ViewModels;

/// <summary>Galerideki bir klip ya da ekran görüntüsü.</summary>
public sealed class ClipItemViewModel : ViewModelBase
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly DateTime _createdAt;
    private ImageSource? _thumbnail;
    private string _duration = "";

    public ClipItemViewModel(FileInfo file)
    {
        Path = file.FullName;
        Game = file.Directory?.Name ?? "";
        IsVideo = !file.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
        _createdAt = file.LastWriteTime;
    }

    public string Path { get; }
    public string Game { get; }
    public bool IsVideo { get; }

    /// <summary>"Az önce", "12 dk önce", "Dün 21:40", "8 Eki"</summary>
    public string When
    {
        get
        {
            var age = DateTime.Now - _createdAt;
            if (age.TotalMinutes < 1) return "Az önce";
            if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes} dk önce";
            if (_createdAt.Date == DateTime.Today) return $"{(int)age.TotalHours} sa önce";
            if (_createdAt.Date == DateTime.Today.AddDays(-1)) return $"Dün {_createdAt:HH:mm}";
            return _createdAt.ToString(_createdAt.Year == DateTime.Now.Year ? "d MMM" : "d MMM yyyy", Turkish);
        }
    }

    /// <summary>Videolarda "0:18", "12:04"; ekran görüntülerinde boş.</summary>
    public string Duration
    {
        get => _duration;
        private set => SetProperty(ref _duration, value);
    }

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        private set => SetProperty(ref _thumbnail, value);
    }

    public async Task LoadThumbnailAsync(string? ffmpegPath)
    {
        if (ffmpegPath is null)
        {
            return;
        }

        var thumb = await ThumbnailService.GetAsync(ffmpegPath, Path);
        if (thumb is null)
        {
            return;
        }

        if (thumb.Duration is { } d)
        {
            Duration = d.TotalHours >= 1 ? d.ToString(@"h\:mm\:ss") : d.ToString(@"m\:ss");
        }

        // Dosya kilitlenmesin diye belleğe okunur; küçük boyutta çözülür
        Thumbnail = await Task.Run(() =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 400;
            bitmap.UriSource = new Uri(thumb.ImagePath);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        });
    }
}
