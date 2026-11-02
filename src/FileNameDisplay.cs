using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace Harbor;

internal sealed class FileNameDisplay : IValueConverter
{
    readonly Settings settings;
    internal FileNameDisplay(Settings settings) => this.settings = settings;
    public object Convert(object value, Type type, object parameter, CultureInfo culture) => value is FileEntry entry ? settings.FilesShowExtensions || entry.Folder ? entry.Name : Path.GetFileNameWithoutExtension(entry.Name) : value;
    public object ConvertBack(object value, Type type, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
