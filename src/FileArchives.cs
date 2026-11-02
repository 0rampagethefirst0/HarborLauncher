using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Harbor;

internal static class FileArchives
{
    internal static void Compress(string[] paths, string destination, Func<bool>? cancelled = null)
    {
        if (File.Exists(destination)) throw new IOException("That archive already exists.");
        foreach (string path in paths) if (Directory.Exists(path) && (Path.GetFullPath(destination) + "\\").StartsWith(Path.GetFullPath(path).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Save the archive outside the folder being compressed.");
        string staging = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var file = new FileStream(staging, FileMode.CreateNew))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                foreach (string path in paths) Add(zip, path, Path.GetFileName(path.TrimEnd('\\')));
            File.Move(staging, destination);
            void Add(ZipArchive archive, string path, string entry)
            {
                if (cancelled?.Invoke() == true) throw new OperationCanceledException("Compression cancelled.");
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return;
                if (Directory.Exists(path)) { if (!Directory.EnumerateFileSystemEntries(path).Any()) archive.CreateEntry(entry.Replace('\\', '/') + "/"); foreach (string child in Directory.EnumerateFileSystemEntries(path)) Add(archive, child, entry + "/" + Path.GetFileName(child)); }
                else archive.CreateEntryFromFile(path, entry.Replace('\\', '/'), CompressionLevel.Optimal);
            }
        }
        catch { if (File.Exists(staging)) File.Delete(staging); throw; }
    }
    internal static void Extract(string archive, string destination, Func<bool>? cancelled = null)
    {
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Choose a new extraction folder.");
        string root = Path.GetFullPath(destination).TrimEnd('\\') + "\\";
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries) ValidateEntry(root, entry.FullName, entry.ExternalAttributes);
        Directory.CreateDirectory(destination);
        foreach (var entry in zip.Entries)
        {
            if (cancelled?.Invoke() == true) throw new OperationCanceledException("Extraction stopped. Already extracted files were kept.");
            string path = ValidateEntry(root, entry.FullName, entry.ExternalAttributes);
            if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\')) { Directory.CreateDirectory(path); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(path)!); using (var input = entry.Open()) using (var output = new FileStream(path, FileMode.CreateNew)) input.CopyTo(output); File.SetLastWriteTime(path, entry.LastWriteTime.LocalDateTime);
        }
    }
    internal static string ValidateEntry(string root, string name, int attributes)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains(':') || Path.IsPathRooted(name) || ((attributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("This archive contains an unsupported path or link.");
        string path = Path.GetFullPath(Path.Combine(root, name.Replace('/', '\\')));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("An archive entry would leave the extraction folder."); return path;
    }
}
