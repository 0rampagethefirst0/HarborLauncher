using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Harbor;

internal sealed record DesktopLibraryItem(string Name, string Path, string Target, bool Folder = false, bool Desktop = true)
{
    internal bool App => !Folder && System.IO.Path.GetExtension(Path).ToLowerInvariant() is ".lnk" or ".url" or ".exe" or ".appref-ms" or ".cmd" or ".bat" || !Desktop;
    internal string Kind => Folder ? "Folder" : App ? "App" : System.IO.Path.GetExtension(Path) is { Length: > 1 } ext ? ext.TrimStart('.').ToUpperInvariant() + " file" : "File";
    internal Pin Pin => new() { Name = Name, Path = Path, Target = string.IsNullOrWhiteSpace(Target) ? Path : Target };
}

internal static class DesktopLibraryModel
{
    internal static readonly string[] Categories = { "Games", "Social", "Music & video", "Creative", "Tools", "Folders", "Files", "Other" };
    internal static IEnumerable<string> DesktopRoots => new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    internal static List<DesktopLibraryItem> Scan(IEnumerable<string>? roots = null)
    {
        var items = new List<DesktopLibraryItem>();
        foreach (string root in roots ?? DesktopRoots)
            try { foreach (string path in Directory.EnumerateFileSystemEntries(root)) { try { var attributes = File.GetAttributes(path); if ((attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue; bool folder = attributes.HasFlag(FileAttributes.Directory); items.Add(new DesktopLibraryItem(folder ? System.IO.Path.GetFileName(path) : System.IO.Path.GetFileNameWithoutExtension(path), path, path, folder)); } catch (IOException) { } catch (UnauthorizedAccessException) { } } } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return items.DistinctBy(i => i.Path, StringComparer.OrdinalIgnoreCase).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    internal static List<DesktopLibraryItem> AllApps(IEnumerable<AppEntry> apps) => apps.Select(a => new DesktopLibraryItem(a.Name, a.Path, a.Key, Desktop: false)).DistinctBy(a => a.Target, StringComparer.OrdinalIgnoreCase).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    internal static string Category(Settings settings, DesktopLibraryItem item)
    {
        if (settings.DesktopCategories.TryGetValue(item.Path, out string? category) && !string.IsNullOrWhiteSpace(category)) return category;
        if (item.Folder) return "Folders"; if (!item.App) return "Files";
        string text = (item.Name + " " + item.Target).ToLowerInvariant();
        if (Has("steam", "epic games", "rocket league", "minecraft", "roblox", "fortnite", "valorant", "battle.net", "ubisoft", "xbox", "game", "playnite", "geode", "geometry dash", "curseforge", "modrinth", "prism launcher")) return "Games";
        if (Has("discord", "whatsapp", "telegram", "signal", "messenger", "teams", "zoom", "slack")) return "Social";
        if (Has("spotify", "medal", "vlc", "music", "audio", "voicemeeter", "wave link", "obs", "video", "mpv", "youtube")) return "Music & video";
        if (Has("visual studio", "code.exe", "vscode")) return "Tools";
        if (Has("blender", "unity", "unreal", "photoshop", "krita", "gimp", "figma", "resolve", "premiere", "aseprite", "studio", "paint")) return "Creative";
        if (Has("code", "notepad", "browser", "chrome", "opera", "firefox", "edge", "terminal", "settings", "restore", "backup", "accel", "ds4", "calculator", "7-zip", "7z", "task manager", "explorer", "harbor")) return "Tools";
        return "Other";
        bool Has(params string[] words) => words.Any(text.Contains);
    }
    internal static IEnumerable<string> Groups(Settings settings) => Categories.Concat(settings.DesktopCustomCategories).Distinct(StringComparer.OrdinalIgnoreCase);
    internal static List<DesktopLibraryItem> Query(IEnumerable<DesktopLibraryItem> items, Settings settings, string category, string search) => items.Where(i => category == "All" || category == "Favorites" ? category == "All" || settings.DesktopFavorites.Contains(i.Path, StringComparer.OrdinalIgnoreCase) : Category(settings, i).Equals(category, StringComparison.OrdinalIgnoreCase)).Where(i => (i.Name + " " + Category(settings, i) + " " + i.Kind).Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    internal static void Move(Settings settings, string path, string category) { if (!Groups(settings).Contains(category, StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Choose an existing category."); settings.DesktopCategories[path] = category; settings.Save(); }
}
