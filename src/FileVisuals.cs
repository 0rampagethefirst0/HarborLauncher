using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

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
        var icon = UiIcons.Icon(kind, size);
        if (icon is Viewbox box && box.Child is Canvas canvas) foreach (var path in canvas.Children) if (path is System.Windows.Shapes.Path p) p.Stroke = Theme.Brush(entry.Folder ? "#E7BD75" : kind == "image" ? "#BA9FE8" : kind == "code" ? "#82D3B4" : "#9FBCED");
        return icon;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
