using System.Collections.Generic;

namespace Harbor;

internal static class TweakPackages
{
    internal static List<ModDefinition> All()
    {
        var mods = new List<ModDefinition>();
        Add("window-topmost", "Always on Top", "Windows", "Keep chosen apps above ordinary windows. Match executable names, full paths, app IDs, or window titles. Restores the original state when disabled or Harbor exits.", true);
        Add("window-opacity", "Window Opacity", "Windows", "Set the opacity of selected ordinary desktop windows. Their original opacity is restored when disabled or Harbor exits. WPF apps and windows that manage their own transparency may decline the effect and are left unchanged.", true, Number("opacity", "Opacity percent, 30 to 100", "90"));
        Add("window-placement", "App Placement", "Windows", "Open matching windows on your chosen monitor and in a chosen screen region. Applied once per window when enabled; Undo layout restores its previous position.", true, Choice("monitor", "Monitor", "current", "current", "primary", "secondary"), Choice("region", "Screen region", "center", "center", "left", "right", "top", "bottom", "top-left", "top-right", "bottom-left", "bottom-right"), Number("width", "Center width percent", "75"), Number("height", "Center height percent", "80"), Number("gap", "Edge gap in pixels", "12"));
        Add("window-memory", "Remember Window Positions", "Windows", "Remember normal window positions for selected apps and restore them when those apps open again. Positions stay within the current screen layout. Games are skipped.", true);
        Add("dock-scroll-windows", "Scroll to Switch Windows", "Dock", "Scroll over a running dock app to cycle its windows. Choose whether minimized windows join the cycle.", false, Choice("minimized", "Include minimized windows", "yes", "yes", "no"), Choice("direction", "Scroll direction", "normal", "normal", "reversed"));
        Add("dock-middle-close", "Middle Click to Close", "Dock", "Middle-click an app icon to close its focused window, or its most recent window. App-owned save prompts still apply. Overrides middle-click to open a new instance.", false);
        Add("dock-monitor-only", "Monitor-specific Dock", "Dock", "Pinned app indicators and window menus show only windows on that dock's monitor. An app with windows on another monitor opens a new instance when clicked here.", false);
        Add("dock-labels", "Dock App Labels", "Dock", "Add app names or active window titles beside dock icons. Choose label width and when to show labels.", false, Choice("label", "Label text", "app", "app", "window"), Number("width", "Maximum label width", "130"), Choice("when", "Show labels", "always", "always", "running"));
        Add("dock-wheel-volume", "App Volume on the Dock", "Audio", "Hold Alt and scroll over an app icon to adjust that app's audio sessions on active playback devices. Other apps keep their own volume. Uses Windows audio controls.", false, Number("step", "Volume step percent", "3"));
        Add("toolbar-wheel-volume", "Scroll the Bar for Volume", "Audio", "Scroll anywhere on the top bar to change system volume. Optionally require Alt to prevent accidental changes.", false, Number("step", "Volume step percent", "2"), Choice("modifier", "Require Alt", "no", "no", "yes"));
        Add("clock-seconds", "Toolbar Clock Format", "Toolbar", "Use your own date and time format in the toolbar clock, including seconds. No continuous animation is used.", false, new ModOption { Key = "format", Label = "Date and time format", Default = "ddd, MMM d    HH:mm:ss" });
        Add("title-actions", "Active Window Controls", "Toolbar", "Double-click the active app title on the top bar to maximize or restore it. Right-click the title for minimize, close, always-on-top, and app-specific mod shortcuts.", false);
        return mods;
        void Add(string component, string name, string category, string description, bool window, params ModOption[] options)
        {
            var list = new List<ModOption>();
            if (window) { list.Add(new ModOption { Key = "apps", Label = "Apps, one pattern per line (* matches any text)", Type = "multiline", Default = "notepad.exe" }); list.Add(new ModOption { Key = "titles", Label = "Window title patterns (blank = any)", Type = "multiline" }); list.Add(new ModOption { Key = "exclude", Label = "Excluded apps or titles, one pattern per line", Type = "multiline" }); }
            list.AddRange(options);
            mods.Add(new ModDefinition { Format = 3, Id = "harbor." + component, Name = name, Category = category, Description = description, Kind = "tweak", Component = component, MinimumHarborVersion = "0.4.0", Tags = new() { "behavior", category.ToLowerInvariant(), window ? "per-app" : "shell" }, Options = list });
        }
    }
    static ModOption Number(string key, string label, string value) => new() { Key = key, Label = label, Default = value, Type = "number" };
    static ModOption Choice(string key, string label, string value, params string[] choices) => new() { Key = key, Label = label, Default = value, Type = "choice", Choices = choices };
}
