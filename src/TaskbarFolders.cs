using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace Harbor;

public sealed class AppFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Folder";
    public List<Pin> Apps { get; set; } = new();
}

internal static class TaskbarFolders
{
    internal const string Prefix = "harbor-folder://", DragFormat = "Harbor.TaskbarApp";
    internal static bool IsFolder(string path) => path.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    internal static string Path(AppFolder folder) => Prefix + folder.Id;
    internal static AppFolder? Find(Settings c, string path) => IsFolder(path) ? c.AppFolders.FirstOrDefault(f => path[Prefix.Length..].Equals(f.Id, StringComparison.OrdinalIgnoreCase)) : null;
    internal static IEnumerable<Pin> AllApps(Settings c) => c.Pins.Where(p => !IsFolder(p.Path)).Concat(c.AppFolders.SelectMany(f => f.Apps));
    internal static string CleanName(string? name) { string value = new((name ?? "").Where(ch => !char.IsControl(ch)).Take(50).ToArray()); return string.IsNullOrWhiteSpace(value) ? "Folder" : value.Trim(); }
    internal static void Normalize(Settings c)
    {
        c.Pins ??= new(); c.AppFolders ??= new(); c.AppFolderTabOrder ??= new();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        c.AppFolders = c.AppFolders.Where(f => f != null && f.Id != null && f.Id.Length is > 0 and < 65 && f.Id.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_') && ids.Add(f.Id)).Take(60).ToList();
        foreach (var folder in c.AppFolders) {
            folder.Name = CleanName(folder.Name); folder.Apps ??= new();
            folder.Apps = folder.Apps.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Path) && !IsFolder(p.Path) && paths.Add(p.Path)).Take(300).ToList();
            foreach (var pin in folder.Apps) { pin.Name ??= "App"; pin.Target ??= pin.Path; }
        }
        c.AppFolderTabOrder = c.AppFolderTabOrder.Where(id => ids.Contains(id)).Distinct(StringComparer.OrdinalIgnoreCase).Take(60).ToList();
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pins = c.Pins.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Path) && roots.Add(p.Path) && (IsFolder(p.Path) ? Find(c, p.Path) != null : !paths.Contains(p.Path))).ToList();
        c.Pins.Clear(); c.Pins.AddRange(pins);
        foreach (var pin in c.Pins) { pin.Name ??= "App"; pin.Target ??= pin.Path; }
        foreach (var folder in c.AppFolders) {
            var pin = c.Pins.FirstOrDefault(p => p.Path.Equals(Path(folder), StringComparison.OrdinalIgnoreCase));
            if (pin == null) c.Pins.Add(new Pin { Name = folder.Name, Path = Path(folder), Target = Path(folder) }); else { pin.Name = folder.Name; pin.Target = pin.Path; }
        }
    }
    internal static AppFolder Create(Settings c, Pin source, Pin target)
    {
        if (source.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Choose two different apps.");
        if (Find(c, target.Path) is { } existing) { Add(c, existing, source); return existing; }
        if (IsFolder(source.Path)) throw new InvalidOperationException("Drop an app on a folder, or drag the folder by its edge to move it.");
        int index = c.Pins.FindIndex(p => p.Path.Equals(target.Path, StringComparison.OrdinalIgnoreCase));
        int sourceIndex = c.Pins.FindIndex(p => p.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase)); if (sourceIndex >= 0 && sourceIndex < index) index--;
        var folder = new AppFolder { Apps = new() { target, source } }; RemoveApp(c, source.Path); RemoveApp(c, target.Path);
        c.AppFolders.Add(folder); c.Pins.Insert(Math.Clamp(index, 0, c.Pins.Count), new Pin { Name = folder.Name, Path = Path(folder), Target = Path(folder) }); return folder;
    }
    internal static void Add(Settings c, AppFolder folder, Pin source, string? before = null, bool after = false)
    {
        var origin = Find(c, source.Path);
        if (origin == folder) return;
        if (origin != null) { var apps = origin.Apps.ToArray(); c.AppFolders.Remove(origin); c.Pins.RemoveAll(p => p.Path.Equals(Path(origin), StringComparison.OrdinalIgnoreCase)); foreach (var app in apps) Add(c, folder, app, before, after); return; }
        if (source.Path.Equals(before, StringComparison.OrdinalIgnoreCase)) return;
        RemoveApp(c, source.Path); int index = before == null ? -1 : folder.Apps.FindIndex(p => p.Path.Equals(before, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = folder.Apps.Count; else if (after) index++;
        folder.Apps.Insert(index, source);
    }
    internal static void RemoveApp(Settings c, string path) { c.Pins.RemoveAll(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)); foreach (var folder in c.AppFolders) folder.Apps.RemoveAll(p => p.Path.Equals(path, StringComparison.OrdinalIgnoreCase)); }
    internal static void ToTaskbar(Settings c, Pin source, string? target = null, bool after = false)
    {
        if (source.Path.Equals(target, StringComparison.OrdinalIgnoreCase)) return;
        if (Find(c, source.Path) != null) c.Pins.RemoveAll(p => p.Path.Equals(source.Path, StringComparison.OrdinalIgnoreCase)); else RemoveApp(c, source.Path);
        int index = target == null ? -1 : c.Pins.FindIndex(p => p.Path.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (index < 0) index = c.Pins.Count; else if (after) index++;
        c.Pins.Insert(index, source);
    }
    internal static void Rename(Settings c, AppFolder folder, string name) { folder.Name = CleanName(name); var pin = c.Pins.FirstOrDefault(p => p.Path.Equals(Path(folder), StringComparison.OrdinalIgnoreCase)); if (pin != null) pin.Name = folder.Name; }
    internal static void Unpack(Settings c, AppFolder folder)
    {
        int index = c.Pins.FindIndex(p => p.Path.Equals(Path(folder), StringComparison.OrdinalIgnoreCase)); c.Pins.RemoveAll(p => p.Path.Equals(Path(folder), StringComparison.OrdinalIgnoreCase)); c.AppFolders.Remove(folder);
        c.Pins.InsertRange(Math.Clamp(index, 0, c.Pins.Count), folder.Apps);
    }
    internal static DataObject Data(Pin pin) => new(DragFormat, JsonSerializer.Serialize(pin));
    internal static Pin? Read(IDataObject data)
    {
        try { if (data.GetData(DragFormat) is string json && json.Length < 20000) { var pin = JsonSerializer.Deserialize<Pin>(json); if (pin == null || string.IsNullOrWhiteSpace(pin.Path) || pin.Path.Length >= 4000) return null; pin.Name ??= "App"; pin.Target ??= pin.Path; return pin; } } catch (JsonException) { }
        return null;
    }
    internal static List<Pin> Shortcuts(IDataObject data)
    {
        if (Read(data) is { } own) return new() { own };
        if (data.GetData(DataFormats.FileDrop) is not string[] paths) return new();
        return paths.Where(File.Exists).Where(p => System.IO.Path.GetExtension(p).ToLowerInvariant() is ".exe" or ".lnk" or ".url").Select(p => new Pin { Name = System.IO.Path.GetFileNameWithoutExtension(p), Path = p, Target = p.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? Catalog.Resolve(p) : p }).ToList();
    }
}
