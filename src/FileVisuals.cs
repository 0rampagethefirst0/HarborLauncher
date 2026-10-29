using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Harbor;

internal sealed class FileIconConverter : IValueConverter
{
    readonly int size;
    internal FileIconConverter(int size) { this.size = size; }
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not FileEntry entry) return UiIcons.Icon("file", size);
        string ext = Path.GetExtension(entry.Path).ToLowerInvariant();
        string kind = entry.Folder ? "folder" : ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" ? "image" : ext is ".zip" or ".7z" or ".rar" or ".harbormod" ? "archive" : ext is ".cs" or ".js" or ".ts" or ".py" or ".html" or ".css" or ".json" or ".xml" ? "code" : ext is ".mp4" or ".mkv" or ".mov" ? "video" : ext is ".mp3" or ".wav" or ".flac" ? "audio" : "file";
        if (entry.Folder) return FolderArt(entry.Name, size);
        if (kind == "image") return new FileThumbnail(entry.Path, size);
        if (ext is ".exe" or ".lnk" or ".url") return Theme.AppIcon(entry.Path, entry.Name, size);
        var icon = UiIcons.Icon(kind, size);
        if (icon is Viewbox box && box.Child is Canvas canvas) foreach (var path in canvas.Children) if (path is System.Windows.Shapes.Path p) p.Stroke = Theme.Brush(entry.Folder ? "#E7BD75" : kind == "image" ? "#BA9FE8" : kind == "code" ? "#82D3B4" : "#9FBCED");
        return icon;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    internal static FrameworkElement FolderArt(string name, int size)
    {
        string lower = name.ToLowerInvariant(); string? glyph = lower.Contains("download") ? "down" : lower.Contains("picture") || lower.Contains("image") ? "image" : lower.Contains("video") ? "video" : lower.Contains("music") || lower.Contains("audio") ? "audio" : lower.Contains("document") ? "file" : lower.Contains("game") ? "apps" : lower is "desktop" ? "window" : null;
        var color = Theme.Brush(glyph is "down" or "window" ? "#80B8FF" : glyph is "image" or "video" ? "#C5A7F6" : glyph == "audio" ? "#FFABAD" : glyph == "apps" ? "#7ED8B8" : name.StartsWith(".") ? "#9AA6BA" : "#EAC178");
        var canvas = new Canvas { Width = 64, Height = 52 };
        canvas.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M3,10 Q3,5 8,5 L25,5 L31,12 L56,12 Q61,12 61,17 L61,43 Q61,48 56,48 L8,48 Q3,48 3,43 Z"), Fill = color, Opacity = .65 });
        canvas.Children.Add(new System.Windows.Shapes.Path { Data = Geometry.Parse("M3,19 Q3,14 8,14 L56,14 Q61,14 61,19 L61,43 Q61,48 56,48 L8,48 Q3,48 3,43 Z"), Fill = color });
        if (glyph != null) { var mark = UiIcons.Icon(glyph, 22); mark.Opacity = .8; Canvas.SetLeft(mark, 22); Canvas.SetTop(mark, 23); canvas.Children.Add(mark); }
        return new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = canvas };
    }
}

internal sealed class FileThumbnail : Grid
{
    static readonly ConcurrentDictionary<string, BitmapSource> cache = new();
    static readonly ConcurrentQueue<string> order = new();
    static readonly SemaphoreSlim decoding = new(2);
    internal FileThumbnail(string path, int size)
    {
        Width = Height = size; Children.Add(UiIcons.Icon("image", size));
        Loaded += async (_, _) => {
            try {
                var info = new FileInfo(path); if (info.Length > 32_000_000) return; string key = path + ":" + info.LastWriteTimeUtc.Ticks;
                if (!cache.TryGetValue(key, out var bitmap)) {
                    await decoding.WaitAsync();
                    try { bitmap = await Task.Run(() => { using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 160; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image; }); cache[key] = bitmap; order.Enqueue(key); while (cache.Count > 96 && order.TryDequeue(out var oldest)) cache.TryRemove(oldest, out _); }
                    finally { decoding.Release(); }
                }
                Children.Clear(); Children.Add(new Border { CornerRadius = new CornerRadius(7), ClipToBounds = true, Child = new Image { Source = bitmap, Stretch = Stretch.Uniform } });
            } catch (Exception) { /* Keep the type icon if a thumbnail cannot be read. */ }
        };
    }
}
