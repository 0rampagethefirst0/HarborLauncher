using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Harbor;

public sealed class DesktopProfile
{
    public int Format { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "My desktop";
    public List<string> EnabledMods { get; set; } = new();
    public Dictionary<string, Dictionary<string, string>> Options { get; set; } = new();
    public Dictionary<string, JsonElement> Appearance { get; set; } = new();
    public override string ToString() => Name;
}

internal static class Profiles
{
    internal static readonly string[] AppearanceKeys = new string[] { "HarborWallpaper", "DesktopLibraryEnabled", "DesktopLogoSpin", "DesktopLogoSize", "StartupIntroEnabled", "AnimationFramesPerSecond", "TopDockEnabled", "TopDockPrimaryOnly", "TopDockReserveSpace", "TopDockOnTop", "TopDockShowBorder", "TopDockBrandText", "TopDockSeconds", "TopDockFontSize", "TopDockIconSize", "TopDockPadding", "TopDockGap", "TopDockRadius", "TopDockTitleWidth", "TopDockOpacity", "TopDockItems", "TopDockLayoutVersion", "TaskbarDividers", "PreviewFramesPerSecond", "IconSize", "TopBarHeight", "DockHeight", "DockRadius", "DockAlignment", "DockHoverThrough", "DockFadeOpacity", "DockInteractionDelay", "DockQuietWhileTyping", "TaskbarShowRunningApps", "DockMotionMilliseconds", "ShowActiveTitle", "ShowMedia", "ShowClock", "ShowNetwork", "ShowFilesButton", "ShowModsButton", "ShowStats", "Use24HourClock", "BackgroundColor", "PanelColor", "TextColor", "MutedColor", "RaisedColor", "BorderColor", "Accent" }.Concat(HomeLayout.PreferenceKeys).Distinct().ToArray();
    static bool Allowed(ModDefinition mod, string key) => mod.Options.Any(o => o.Key == key) || (key.Length < 16 && (key.StartsWith("x-") || key.StartsWith("y-")) && key[2..].All(char.IsDigit));
    internal static DesktopProfile Capture(Settings config, Mods mods, string name)
    {
        var profile = new DesktopProfile { Name = name, EnabledMods = mods.Enabled.Select(m => m.Id).ToList() };
        foreach (var mod in mods.Catalog.Where(mods.Installed))
        {
            var values = mod.Options.ToDictionary(o => o.Key, o => mods.Value(mod, o.Key));
            if (config.ModOptions.TryGetValue(mod.Id, out var saved)) foreach (var pair in saved.Where(p => Allowed(mod, p.Key))) values[pair.Key] = pair.Value;
            profile.Options[mod.Id] = values;
        }
        foreach (string key in AppearanceKeys) profile.Appearance[key] = JsonSerializer.SerializeToElement(typeof(Settings).GetProperty(key)!.GetValue(config));
        return profile;
    }
    internal static DesktopProfile Read(string json)
    {
        if (json.Length > 2_000_000) throw new InvalidDataException("A profile can contain up to 2 MB.");
        var p = JsonSerializer.Deserialize<DesktopProfile>(json, Mods.Json) ?? throw new InvalidDataException("Empty profile."); Validate(p); return p;
    }
    internal static void Validate(DesktopProfile p)
    {
        if (p.Format != 1 || string.IsNullOrWhiteSpace(p.Id) || p.Id.Length > 80 || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 120 || p.EnabledMods == null || p.EnabledMods.Count > 500 || p.EnabledMods.Any(id => string.IsNullOrWhiteSpace(id) || id.Length > 80) || p.EnabledMods.Distinct(StringComparer.OrdinalIgnoreCase).Count() != p.EnabledMods.Count || p.Options == null || p.Options.Count > 1000 || p.Options.Any(m => m.Key.Length > 80 || m.Value == null || m.Value.Count > 150 || m.Value.Any(v => v.Key.Length > 80 || v.Value == null || v.Value.Length > 10000)) || p.Appearance == null || p.Appearance.Keys.Any(k => !AppearanceKeys.Contains(k))) throw new InvalidDataException("Invalid desktop profile.");
        // Validate property types without mutating live settings.
        foreach (var pair in p.Appearance)
        {
            var property = typeof(Settings).GetProperty(pair.Key)!;
            try
            {
                if (pair.Value.ValueKind == JsonValueKind.Null || pair.Value.Deserialize(property.PropertyType) == null) throw new JsonException();
                if (pair.Key.EndsWith("Color") || pair.Key == "Accent") { string? color = pair.Value.GetString(); if (color == null || color.Length > 30 || System.Windows.Media.ColorConverter.ConvertFromString(color) is not System.Windows.Media.Color) throw new FormatException(); }
                if (pair.Key == "DockAlignment" && pair.Value.GetString() is not "left" and not "center" and not "right") throw new FormatException();
            }
            catch (Exception ex) when (ex is JsonException or FormatException or NotSupportedException) { throw new InvalidDataException("Invalid appearance value: " + pair.Key); }
        }
    }
    internal static List<string> Missing(DesktopProfile profile, Mods mods) => profile.EnabledMods.Where(id => mods.Catalog.All(m => !m.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).ToList();
    internal static void Apply(Settings config, Mods mods, DesktopProfile profile)
    {
        Validate(profile); var missing = Missing(profile, mods);
        if (missing.Count > 0) throw new InvalidDataException("Import these mods before applying the profile: " + string.Join(", ", missing));
        var requested = profile.EnabledMods.Select(id => mods.Catalog.First(m => m.Id.Equals(id, StringComparison.OrdinalIgnoreCase))).ToList();
        var plan = ModPlan.Build(mods, requested, Array.Empty<ModDefinition>());
        if (plan.Errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, plan.Errors));
        var previous = Capture(config, mods, "Previous setup"); var oldOptions = config.ModOptions; var oldEnabled = config.EnabledMods.ToList(); var oldPrevious = config.PreviousProfile;
        try
        {
            var merged = oldOptions.ToDictionary(m => m.Key, m => new Dictionary<string, string>(m.Value));
            foreach (var item in profile.Options)
            {
                var mod = mods.Catalog.FirstOrDefault(m => m.Id == item.Key); if (mod == null) continue;
                if (!merged.TryGetValue(item.Key, out var values)) merged[item.Key] = values = new();
                foreach (var pair in item.Value.Where(p => Allowed(mod, p.Key))) values[pair.Key] = pair.Value;
            }
            config.ModOptions = merged; mods.SetEnabledSet(requested);
            foreach (var pair in profile.Appearance) { var property = typeof(Settings).GetProperty(pair.Key)!; property.SetValue(config, pair.Value.Deserialize(property.PropertyType)); }
            if (!profile.Appearance.ContainsKey("TopDockLayoutVersion")) config.TopDockLayoutVersion = 0;
            if (!profile.Appearance.ContainsKey("TopDockItems")) config.TopDockItems = BarLayout.Defaults(config);
            config.Normalize(); config.PreviousProfile = previous; config.Save();
        }
        catch
        {
            config.ModOptions = oldOptions; config.EnabledMods = oldEnabled; config.PreviousProfile = oldPrevious;
            foreach (var pair in previous.Appearance) { var property = typeof(Settings).GetProperty(pair.Key)!; property.SetValue(config, pair.Value.Deserialize(property.PropertyType)); }
            throw;
        }
        mods.NotifyChanged();
    }
}

internal sealed class ProfileWindow : Window
{
    readonly Shell shell;
    readonly ListBox list = new();
    readonly StackPanel detail = new() { Margin = new Thickness(24) };
    readonly TextBlock status = Theme.Label("", 12, Theme.Muted);
    readonly TextBlock empty = new() { Text = "No saved profiles yet.\n\nSave your current setup to add one here.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(14, 18, 28, 0), FontSize = 13, LineHeight = 21, IsHitTestVisible = false };
    internal ProfileWindow(Shell shell)
    {
        this.shell = shell; Title = "Harbor — profiles"; Theme.AppWindow(this, 940, 670);
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, "Desktop Profiles"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var footer = new StackPanel { Margin = new Thickness(20, 12, 20, 18) }; footer.Children.Add(status); var buttons = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
        Add("Save current setup", Save); Add("Import profile", Import); Add("Undo last switch", () => { if (shell.Config.PreviousProfile != null) Try(() => { Profiles.Apply(shell.Config, shell.Mods, shell.Config.PreviousProfile); status.Text = "Previous setup restored."; Reload(); }); });
        footer.Children.Add(buttons); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var grid = new Grid { Margin = new Thickness(20, 18, 20, 0) }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(270) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        list.Margin = new Thickness(0, 0, 16, 0); list.SelectionChanged += (_, _) => ShowDetails(); var navigation = new Grid(); navigation.Children.Add(list); navigation.Children.Add(empty); grid.Children.Add(navigation); var scroll = new ScrollViewer { Content = detail, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(scroll, 1); grid.Children.Add(Theme.Card(scroll)); Grid.SetColumn(grid.Children[1], 1); root.Children.Add(grid); Content = root; Reload();
        void Add(string label, Action action) { var button = Theme.Button(label, label, action); button.Background = Theme.Raised; button.Margin = new Thickness(0, 0, 8, 4); buttons.Children.Add(button); }
    }
    void Reload(string? id = null) { list.ItemsSource = null; list.ItemsSource = shell.Config.Profiles; empty.Visibility = shell.Config.Profiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed; list.SelectedItem = shell.Config.Profiles.FirstOrDefault(p => p.Id == id) ?? shell.Config.Profiles.FirstOrDefault(); ShowDetails(); }
    void ShowDetails()
    {
        detail.Children.Clear();
        if (list.SelectedItem is not DesktopProfile profile) { Text("Make this desktop yours", 27); Text("Save the current appearance, enabled mods, their declared options, and widget positions as a profile. Switch between work, gaming, and personal setups."); Text("Pins, startup, notes, clipboard, and remembered app positions stay separate from profiles."); return; }
        Text(profile.Name, 28); Text(profile.EnabledMods.Count + " enabled mods", 12);
        var missing = Profiles.Missing(profile, shell.Mods); if (missing.Count > 0) Text("Missing packages: " + string.Join(", ", missing));
        var apply = Theme.Button("Apply profile", "Apply " + profile.Name, () => Try(() => { Profiles.Apply(shell.Config, shell.Mods, profile); status.Text = "Applied " + profile.Name + ". Undo last switch restores the prior setup."; })); apply.Background = Theme.Raised; apply.Margin = new Thickness(0, 18, 0, 18); detail.Children.Add(apply);
        foreach (string id in profile.EnabledMods) Text("• " + (shell.Mods.Catalog.FirstOrDefault(m => m.Id == id)?.Name ?? id), 13);
        var actions = new WrapPanel { Margin = new Thickness(0, 20, 0, 0) };
        actions.Children.Add(Theme.Button("Update from current", "Replace this profile with the current setup", () => Try(() => { var next = Profiles.Capture(shell.Config, shell.Mods, profile.Name); next.Id = profile.Id; shell.Config.Profiles[shell.Config.Profiles.IndexOf(profile)] = next; shell.Config.Save(); Reload(next.Id); status.Text = "Profile updated."; })));
        actions.Children.Add(Theme.Button("Export", "Export this profile", () => { var dialog = new SaveFileDialog { Filter = "Harbor profile|*.harborprofile", FileName = "desktop.harborprofile" }; if (dialog.ShowDialog(this) == true) Try(() => { AtomicFile.WriteText(dialog.FileName, JsonSerializer.Serialize(profile, Mods.Json)); status.Text = "Profile exported."; }); }));
        actions.Children.Add(Theme.Button("Delete", "Delete this saved profile", () => Try(() => { shell.Config.Profiles.Remove(profile); shell.Config.Save(); Reload(); }))); detail.Children.Add(actions);
    }
    void Text(string value, double size = 14) { detail.Children.Add(new TextBlock { Text = value, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 14), LineHeight = size * 1.5 }); }
    void Save() { string? name = NameDialog.Ask(this, "Save desktop profile", "My desktop"); if (string.IsNullOrWhiteSpace(name)) return; Try(() => { if (shell.Config.Profiles.Count >= 50) throw new InvalidDataException("Up to 50 profiles can be saved."); var profile = Profiles.Capture(shell.Config, shell.Mods, name); shell.Config.Profiles.Add(profile); shell.Config.Save(); Reload(profile.Id); status.Text = "Current setup saved."; }); }
    void Import() { var dialog = new OpenFileDialog { Filter = "Harbor profile|*.harborprofile;*.json" }; if (dialog.ShowDialog(this) == true) Try(() => { if (shell.Config.Profiles.Count >= 50) throw new InvalidDataException("Up to 50 profiles can be saved."); var file = new FileInfo(dialog.FileName); if (file.Length > 2_000_000) throw new InvalidDataException("Profile exceeds 2 MB."); var p = Profiles.Read(File.ReadAllText(file.FullName)); p.Id = Guid.NewGuid().ToString("N"); shell.Config.Profiles.Add(p); shell.Config.Save(); Reload(p.Id); status.Text = "Profile imported. Apply it when ready."; }); }
    void Try(Action action) { try { action(); } catch (Exception ex) { status.Text = ex.Message; } }
}
