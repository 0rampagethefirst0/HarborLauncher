using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Harbor;

internal sealed class FileTab
{
    internal readonly string Id = Guid.NewGuid().ToString("N");
    internal string Folder, Search = "";
    internal string? Label;
    internal readonly Stack<string> Back = new(), Forward = new();
    internal List<FileEntry> Entries = new();
    internal FileTab(string path) { Folder = path; }
    internal string Name => Label ?? (Path.GetFileName(Folder.TrimEnd(Path.DirectorySeparatorChar)) is string name && name.Length > 0 ? name : Folder);
    internal void Visit(string path, bool history)
    {
        if (!TaskbarFolders.IsFolder(path) && path != Files.HomePath && path != Files.FrequentPath && path != RecycleBin.Location) path = Path.GetFullPath(path);
        if (history && Folder.Length > 0 && !Folder.Equals(path, StringComparison.OrdinalIgnoreCase)) { Back.Push(Folder); Forward.Clear(); }
        Folder = path;
    }
    internal string? Previous() { if (Back.Count == 0) return null; Forward.Push(Folder); return Back.Pop(); }
    internal string? Next() { if (Forward.Count == 0) return null; Back.Push(Folder); return Forward.Pop(); }
}
internal static class FileBrowserState
{
    internal static bool Matches(string name, string term) => term.IndexOfAny(new[] { '*', '?' }) >= 0 ? System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(term.Trim(), name, true) : name.Contains(term.Trim(), StringComparison.CurrentCultureIgnoreCase);
    internal static List<FileEntry> Query(IEnumerable<FileEntry> entries, string term, string sort, bool descending)
    {
        var filtered = entries.Where(e => Matches(e.Name, term));
        IOrderedEnumerable<FileEntry> sorted = filtered.OrderByDescending(e => e.Folder);
        return (sort switch {
            "Modified" => descending ? sorted.ThenByDescending(e => e.Modified) : sorted.ThenBy(e => e.Modified),
            "Size" => descending ? sorted.ThenByDescending(e => e.Bytes) : sorted.ThenBy(e => e.Bytes),
            "Created" => descending ? sorted.ThenByDescending(e => e.CreatedAt) : sorted.ThenBy(e => e.CreatedAt),
            "Extension" => descending ? sorted.ThenByDescending(e => e.Extension) : sorted.ThenBy(e => e.Extension),
            "Attributes" => descending ? sorted.ThenByDescending(e => e.Attributes) : sorted.ThenBy(e => e.Attributes),
            "Type" => descending ? sorted.ThenByDescending(e => e.Kind) : sorted.ThenBy(e => e.Kind),
            _ => descending ? sorted.ThenByDescending(e => e.Name, StringComparer.CurrentCultureIgnoreCase) : sorted.ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
        }).ToList();
    }
}
