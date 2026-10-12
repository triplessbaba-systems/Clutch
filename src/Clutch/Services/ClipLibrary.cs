using System.Collections.ObjectModel;
using System.Diagnostics;
using Clutch.Core;
using Clutch.ViewModels;

namespace Clutch.Services;

/// <summary>Kayıt klasöründeki son klipler. Overlay'deki galeri bu listeyi gösterir.</summary>
public sealed class ClipLibrary
{
    private const int MaxItems = 40;
    private static readonly string[] Extensions = [".mp4", ".mkv", ".png"];

    private readonly CaptureController _controller;

    public ClipLibrary(CaptureController controller)
    {
        _controller = controller;
        _controller.ClipSaved += _ => Refresh();
    }

    public ObservableCollection<ClipItemViewModel> Items { get; } = [];

    public void Refresh()
    {
        List<FileInfo> files;
        try
        {
            var root = new DirectoryInfo(_controller.Settings.OutputFolder);
            files = root.Exists
                ? root.EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(f => Extensions.Contains(f.Extension, StringComparer.OrdinalIgnoreCase) &&
                                !f.Name.EndsWith(".kayit.mkv", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(MaxItems)
                    .ToList()
                : [];
        }
        catch (Exception ex)
        {
            Log.Write($"Klipler okunamadı: {ex.Message}");
            files = [];
        }

        Items.Clear();
        foreach (var file in files)
        {
            var item = new ClipItemViewModel(file);
            Items.Add(item);
            _ = item.LoadThumbnailAsync(_controller.FfmpegPath);
        }
    }

    public static void Open(ClipItemViewModel? clip)
    {
        if (clip is not null && File.Exists(clip.Path))
        {
            Process.Start(new ProcessStartInfo(clip.Path) { UseShellExecute = true });
        }
    }

    public static void Reveal(ClipItemViewModel? clip)
    {
        if (clip is not null && File.Exists(clip.Path))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{clip.Path}\"") { UseShellExecute = true });
        }
    }
}
