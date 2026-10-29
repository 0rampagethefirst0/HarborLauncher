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
    public List<TaskbarDivider> TaskbarDividers { get; set; } = new();
    public int PreviewFramesPerSecond { get; set; } = 30;
    public string FilesView { get; set; } = "details";
    public bool FilesPreview { get; set; } = true;
    public bool FilesShowHidden { get; set; }
    public List<string> FileFavorites { get; set; } = new();
    public int DockHeight { get; set; } = 80;
    public int DockRadius { get; set; } = 15;
    public string DockAlignment { get; set; } = "center";
    public bool DockHoverThrough { get; set; } = true;
    public double DockFadeOpacity { get; set; } = .45;
    public int DockInteractionDelay { get; set; } = 3000;
    public bool DockQuietWhileTyping { get; set; } = true;
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
        DockFadeOpacity = double.IsFinite(DockFadeOpacity) ? Math.Clamp(DockFadeOpacity, .2, .8) : .45;
        DockInteractionDelay = Math.Clamp(DockInteractionDelay, 1000, 10000);
        DockMotionMilliseconds = Math.Clamp(DockMotionMilliseconds, 0, 350);
        TopDockFontSize = Math.Clamp(TopDockFontSize, 10, 18); TopDockIconSize = Math.Clamp(TopDockIconSize, 12, 26);
        TopDockPadding = Math.Clamp(TopDockPadding, 3, 20); TopDockGap = Math.Clamp(TopDockGap, 0, 18); TopDockRadius = Math.Clamp(TopDockRadius, 0, 28); TopDockTitleWidth = Math.Clamp(TopDockTitleWidth, 80, 520);
        TopDockOpacity = double.IsFinite(TopDockOpacity) ? Math.Clamp(TopDockOpacity, .5, 1) : 1;
        PreviewFramesPerSecond = Math.Clamp(PreviewFramesPerSecond, 12, 30);
        if (FilesView is not "details" and not "grid") FilesView = "details";
        FileFavorites ??= new(); FileFavorites = FileFavorites.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList();
        BarLayout.Normalize(this);
    }

    public static bool StartupEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("Harbor") is string;
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (value) key.SetValue("Harbor", "\"" + Environment.ProcessPath + "\"");
            else key.DeleteValue("Harbor", false);
        }
    }
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
