using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;

namespace Harbor;

internal sealed record FileEntry(string Path, string Name, bool Folder, long Bytes, DateTime Modified)
{
    public override string ToString() => Name;
    public string Display => (Folder ? "▸  " : "    ") + Name;
    public string Size => Folder ? "Folder" : FileTools.Size(Bytes);
    public string Date => Modified.ToString("MMM d, yyyy  HH:mm");
    public string Kind => Folder ? "Folder" : System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant() is string ext && ext.Length > 0 ? ext + " file" : "File";
}
internal static class FileTools
{
    internal static string Size(long bytes) => bytes < 1024 ? bytes + " B" : bytes < 1048576 ? $"{bytes / 1024.0:0.#} KB" : bytes < 1073741824 ? $"{bytes / 1048576.0:0.#} MB" : $"{bytes / 1073741824.0:0.#} GB";
    internal static string Unique(string folder, string name)
    {
        string path = System.IO.Path.Combine(folder, name); int n = 2;
        while (File.Exists(path) || Directory.Exists(path)) path = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(name) + " (" + n++ + ")" + System.IO.Path.GetExtension(name));
        return path;
    }
    internal static void Copy(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked folders and files must be copied with their original application.");
        if (!Directory.Exists(source)) { File.Copy(source, destination, false); return; }
        string src = System.IO.Path.GetFullPath(source).TrimEnd('\\') + "\\", dest = System.IO.Path.GetFullPath(destination).TrimEnd('\\') + "\\";
        if (dest.StartsWith(src, StringComparison.OrdinalIgnoreCase)) throw new IOException("A folder cannot be copied into itself.");
        Directory.CreateDirectory(destination);
        foreach (string child in Directory.EnumerateFileSystemEntries(source)) Copy(child, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(child)));
    }
    internal static void Transfer(string source, string destination, bool move)
    {
        source = System.IO.Path.GetFullPath(source); destination = System.IO.Path.GetFullPath(destination);
        if (source.Equals(destination, StringComparison.OrdinalIgnoreCase) || Directory.Exists(source) && (destination.TrimEnd('\\') + "\\").StartsWith(source.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a destination outside the source folder.");
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException("That name already exists. Choose another name.");
        if (!move) { Copy(source, destination); return; }
        if (Directory.Exists(source)) FileSystem.MoveDirectory(source, destination, UIOption.OnlyErrorDialogs); else File.Move(source, destination, false);
    }
}

