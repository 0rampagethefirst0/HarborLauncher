using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;

namespace Harbor;

public sealed class Pin
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Target { get; set; } = "";
}

public sealed class Settings
{
    public bool ReplaceTaskbar { get; set; } = true;
    public bool AllMonitors { get; set; } = true;
    public bool HideInFullscreen { get; set; } = true;
    public bool HideInGames { get; set; } = true;
    public List<string> GameApps { get; set; } = new();
    public List<string> GameExclusions { get; set; } = new();
    public bool ShowStats { get; set; } = true;
    public bool AutoTile { get; set; }
    public bool Use24HourClock { get; set; }
    public int IconSize { get; set; } = 32;
    public int TopBarHeight { get; set; } = 38;
    public bool TopDockEnabled { get; set; } = true;
    public bool TopDockPrimaryOnly { get; set; }
    public bool TopDockReserveSpace { get; set; } = true;
    public bool TopDockOnTop { get; set; } = true;
    public bool TopDockShowBorder { get; set; } = true;
    public bool TopDockBrandText { get; set; } = true;
    public bool TopDockSeconds { get; set; }
    public int TopDockFontSize { get; set; } = 12;
    public int TopDockIconSize { get; set; } = 16;
    public int TopDockPadding { get; set; } = 10;
    public int TopDockGap { get; set; } = 2;
    public int TopDockRadius { get; set; } = 0;
    public int TopDockTitleWidth { get; set; } = 260;
    public double TopDockOpacity { get; set; } = 1;
    public List<BarItem> TopDockItems { get; set; } = new();
    public int TopDockLayoutVersion { get; set; }
    public List<AppFolder> AppFolders { get; set; } = new();
    public List<string> AppFolderTabOrder { get; set; } = new();
    public List<TaskbarDivider> TaskbarDividers { get; set; } = new();
    public List<TaskbarDivider> SystemTaskbarDividers { get; set; } = new() { new() { Id = "system.pinned" }, new() { Id = "system.running" }, new() { Id = "system.end" } };
    public int PreviewFramesPerSecond { get; set; } = 90;
    public int AnimationFramesPerSecond { get; set; } = 120;
    public int AnimationRateVersion { get; set; }
    public int ReadabilityVersion { get; set; }
    public int TaskbarFolderSize { get; set; } = 52;
    public int BulkAppSize { get; set; } = 48;
    public double TaskbarBaseTransparency { get; set; } = 20;
    public double TaskbarHoverTransparency { get; set; } = 65;
    public double TaskbarActiveTransparency { get; set; } = 5;
    public int TransparencyVersion { get; set; }
    public int FeatureVersion { get; set; }
    public bool FeatureSetupPending { get; set; }
    internal HashSet<string> FeatureComponents { get; set; } = new();
    internal Dictionary<string, List<string>> FeatureIds { get; set; } = new();
    public int FilesTextSize { get; set; } = 16;
    public int FilesTileSize { get; set; } = 184;
    public string FilesView { get; set; } = "details";
    public bool FilesPreview { get; set; } = true;
    public bool FilesShowHidden { get; set; }
    public bool FilesShowExtensions { get; set; } = true;
    public bool FilesSearchSubfolders { get; set; }
    public string FilesGroupBy { get; set; } = "None";
    public List<string> FilesDetailColumns { get; set; } = new() { "Name", "Modified", "Type", "Size" };
    public List<string> FileFavorites { get; set; } = new();
    public List<string> RecentFolders { get; set; } = new();
    public int DockHeight { get; set; } = 80;
    public int DockRadius { get; set; } = 15;
    public string DockAlignment { get; set; } = "center";
    public bool DockHoverThrough { get; set; } = true;
    public double DockFadeOpacity { get; set; } = .45;
    public int DockInteractionDelay { get; set; } = 3000;
    public bool DockQuietWhileTyping { get; set; } = true;
    public bool TaskbarShowRunningApps { get; set; }
    public int DockMotionMilliseconds { get; set; } = 170;
    public bool ShowActiveTitle { get; set; } = true;
    public bool ShowMedia { get; set; } = true;
    public bool ShowClock { get; set; } = true;
    public bool ShowNetwork { get; set; } = true;
    public bool ShowFilesButton { get; set; } = true;
    public bool ShowModsButton { get; set; } = true;
    public string BackgroundColor { get; set; } = "#141820";
    public string PanelColor { get; set; } = "#1C212D";
    public string TextColor { get; set; } = "#EDF1FA";
    public string MutedColor { get; set; } = "#9AA8BC";
    public string RaisedColor { get; set; } = "#262D3B";
    public string BorderColor { get; set; } = "#343D4C";
    public string Accent { get; set; } = "#8AB4FF";
    public bool ReplaceDesktop { get; set; } = true;
    public bool DesktopLibraryEnabled { get; set; } = true;
    public bool HarborWallpaper { get; set; } = true;
    public bool DesktopLogoSpin { get; set; } = true;
    public int DesktopLogoSize { get; set; } = 240;
    public bool DesktopRecycleBin { get; set; } = true;
    public string HomeStartTab { get; set; } = "Frequently used";
    public int HomeColumns { get; set; } = 4;
    public int HomeRows { get; set; } = 3;
    public int HomeIconSize { get; set; } = 48;
    public int HomeTextSize { get; set; } = 14;
    public int HomeSidebarWidth { get; set; } = 195;
    public int HomeWidth { get; set; } = 1120;
    public int HomeHeight { get; set; } = 850;
    public int HomeCornerRadius { get; set; } = 22;
    public bool HomeShowLabels { get; set; } = true;
    public bool HomeShowKinds { get; set; } = true;
    public bool HomeShowCounts { get; set; } = true;
    public List<string> HomeTabOrder { get; set; } = new();
    public List<string> HomeHiddenTabs { get; set; } = new();
    public Dictionary<string, string> HomeTabNames { get; set; } = new();
    public Dictionary<string, AppUse> AppUsage { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public bool StartupIntroEnabled { get; set; } = true;
    public Dictionary<string, string> DesktopCategories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> DesktopCustomCategories { get; set; } = new();
    public List<string> DesktopFavorites { get; set; } = new();
    public bool UseHarborFiles { get; set; } = true;
    public bool ModsInitialized { get; set; }
    public List<string> EnabledMods { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> ModOptions { get; set; } = new();
    public List<string> DismissedNotifications { get; set; } = new();
    public List<Pin> Pins { get; set; } = new();
    public bool PinsInitialized { get; set; }
    public Dictionary<string, RememberedWindow> WindowMemory { get; set; } = new();
    public List<DesktopProfile> Profiles { get; set; } = new();
    public DesktopProfile? PreviousProfile { get; set; }
    public List<string> FavoriteMods { get; set; } = new();
    internal static string? TestDataDir;
    public static string DataDir => TestDataDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Harbor");
    public static string FilePath => Path.Combine(DataDir, "settings.json");

    public static Settings Load()
    {
        Directory.CreateDirectory(DataDir);
        try
        {
            var s = JsonSerializer.Deserialize<Settings>(AtomicFile.ReadText(FilePath)) ?? new();
            s.Normalize();
            s.Pins ??= new();
            s.GameApps ??= new(); s.GameExclusions ??= new();
            s.EnabledMods ??= new(); s.ModOptions ??= new(); s.DismissedNotifications ??= new();
            s.WindowMemory ??= new(); s.Profiles ??= new();
            s.FavoriteMods ??= new();
            return s;
        }
        catch (FileNotFoundException) { return new(); }
        catch (Exception ex)
        {
            Log.Write("Settings recovery: " + ex.Message);
            try { File.Copy(FilePath, FilePath + ".invalid-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); } catch { }
            return new();
        }
    }

    public void Save()
    {
        Normalize();
        Directory.CreateDirectory(DataDir);
        AtomicFile.WriteText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
    internal void Normalize()
    {
        IconSize = Math.Clamp(IconSize, 24, 44); TopBarHeight = Math.Clamp(TopBarHeight, 28, 64);
        DockHeight = Math.Clamp(Math.Max(DockHeight, IconSize + 32), 64, 128); DockRadius = Math.Clamp(DockRadius, 0, 36);
        if (DockAlignment is not "left" and not "center" and not "right") DockAlignment = "center";
        DockFadeOpacity = double.IsFinite(DockFadeOpacity) ? Math.Clamp(DockFadeOpacity, .005, .995) : .45;
        DockInteractionDelay = Math.Clamp(DockInteractionDelay, 500, 10000);
        DockMotionMilliseconds = Math.Clamp(DockMotionMilliseconds, 0, 350);
        TopDockFontSize = Math.Clamp(TopDockFontSize, 10, 18); TopDockIconSize = Math.Clamp(TopDockIconSize, 12, 26);
        TopDockPadding = Math.Clamp(TopDockPadding, 0, 20); TopDockGap = Math.Clamp(TopDockGap, 0, 18); TopDockRadius = Math.Clamp(TopDockRadius, 0, 28); TopDockTitleWidth = Math.Clamp(TopDockTitleWidth, 40, 520);
        TopDockOpacity = double.IsFinite(TopDockOpacity) ? Math.Clamp(TopDockOpacity, .005, 1) : 1;
        if (ReadabilityVersion < 1) { PreviewFramesPerSecond = Math.Max(60, PreviewFramesPerSecond); ReadabilityVersion = 1; }
        if (AnimationRateVersion < 1) { PreviewFramesPerSecond = Math.Max(90, PreviewFramesPerSecond); AnimationFramesPerSecond = Math.Max(120, AnimationFramesPerSecond); AnimationRateVersion = 1; }
        PreviewFramesPerSecond = Math.Clamp(PreviewFramesPerSecond, 80, 144); AnimationFramesPerSecond = Math.Clamp(AnimationFramesPerSecond, 80, 144);
        TaskbarFolderSize = Math.Clamp(TaskbarFolderSize, 40, 72); FilesTextSize = Math.Clamp(FilesTextSize, 12, 20); FilesTileSize = Math.Clamp(FilesTileSize, 140, 240);
        BulkAppSize = Math.Clamp(BulkAppSize, 24, 96);
        DesktopLogoSize = Math.Clamp(DesktopLogoSize, 120, 280);
        HomeLayout.Normalize(this);
        DesktopCategories = (DesktopCategories ?? new()).Where(p => !string.IsNullOrWhiteSpace(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).Take(10000).GroupBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
        DesktopCustomCategories = (DesktopCustomCategories ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(100).ToList();
        DesktopFavorites = (DesktopFavorites ?? new()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).Take(10000).ToList();
        if (TransparencyVersion < 1) { TaskbarHoverTransparency = (1 - DockFadeOpacity) * 100; TransparencyVersion = 1; }
        TaskbarBaseTransparency = ClampTransparency(TaskbarBaseTransparency); TaskbarHoverTransparency = ClampTransparency(TaskbarHoverTransparency); TaskbarActiveTransparency = ClampTransparency(TaskbarActiveTransparency);
        if (FilesView is not "details" and not "grid") FilesView = "details";
        if (FilesGroupBy is not "None" and not "Type" and not "Date") FilesGroupBy = "None";
        FilesDetailColumns = new[] { "Name" }.Concat(FilesDetailColumns ?? new()).Where(n => new[] { "Name", "Modified", "Type", "Size", "Created", "Extension", "Attributes" }.Contains(n)).Distinct().ToList();
        FileFavorites ??= new(); FileFavorites = FileFavorites.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList();
        RecentFolders ??= new(); RecentFolders = RecentFolders.Where(p => !string.IsNullOrWhiteSpace(p) && !TaskbarFolders.IsFolder(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
        BarLayout.Normalize(this);
        TaskbarFolders.Normalize(this);
        if (AppFolders.Count > 0) DockHeight = Math.Clamp(Math.Max(DockHeight, TaskbarFolderSize + 48), 64, 128);
    }

    public static bool StartupEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return ShellSession.Enabled || key?.GetValue("Harbor") is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (value) ShellSession.Install();
            else { ShellSession.RestoreRegistration(); key.DeleteValue("Harbor", false); }
        }
    }
    static double ClampTransparency(double value) => double.IsFinite(value) ? Math.Clamp(value, .5, 99.5) : .5;
}

public static class Log
{
    private static readonly object Gate = new();
    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.DataDir);
                var path = Path.Combine(Settings.DataDir, "Harbor.log");
                if (File.Exists(path) && new FileInfo(path).Length > 2_000_000) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
            }
        }
        catch { }
    }
}
