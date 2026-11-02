using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Harbor;

public sealed class WidgetNode
{
    public string Type { get; set; } = "column";
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public string Source { get; set; } = "cpu";
    public string Action { get; set; } = "apps";
    public string Argument { get; set; } = "";
    public string Component { get; set; } = "clock";
    public double FontSize { get; set; } = 14;
    public double Width { get; set; }
    public double Height { get; set; }
    public double Gap { get; set; } = 8;
    public double Maximum { get; set; } = 100;
    public string Color { get; set; } = "";
    public string Background { get; set; } = "";
    public Dictionary<string, string> Values { get; set; } = new();
    public List<WidgetNode> Children { get; set; } = new();
}

internal sealed class WidgetLayout
{
    internal static readonly string[] Types = { "column", "row", "wrap", "text", "progress", "button", "input", "check", "divider", "component" };
    internal static readonly string[] Actions = { "apps", "files", "mods", "notifications", "quick", "appearance", "profiles", "desktop", "columns", "grid", "stack", "undo", "launch", "copy", "mute", "volume-up", "volume-down", "previous", "play-pause", "next" };
    readonly Shell shell;
    readonly ModDefinition mod;
    readonly bool preview;
    readonly WidgetDemoData? demo;
    readonly List<Action> updates = new();
    readonly Dictionary<string, double> numbers = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, (DateTime Time, double Value)> drives = new();
    static readonly Regex Tokens = new(@"\{(?<key>[a-z][\w.\-]*)(?::(?<format>[^{}]{0,80}))?\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    internal WidgetLayout(Shell shell, ModDefinition mod, bool preview, WidgetDemoData? demo = null) { this.shell = shell; this.mod = mod; this.preview = preview; this.demo = demo; }
    internal void Tick() { numbers.Clear(); foreach (var update in updates) update(); }
    internal FrameworkElement Build(WidgetNode node, double available)
    {
        FrameworkElement element;
        switch (node.Type)
        {
            case "column":
            case "wrap":
            {
                Panel panel = node.Type == "column" ? new StackPanel() : new WrapPanel();
                foreach (var child in node.Children) { var item = Build(child, available); item.Margin = new Thickness(0, 0, node.Type == "wrap" ? node.Gap : 0, node.Gap); panel.Children.Add(item); }
                element = panel; break;
            }
            case "row":
            {
                var grid = new Grid(); int index = 0; double each = Math.Max(80, (available - Math.Max(0, node.Children.Count - 1) * node.Gap) / Math.Max(1, node.Children.Count));
                foreach (var child in node.Children) { grid.ColumnDefinitions.Add(new ColumnDefinition { Width = child.Width > 0 ? new GridLength(child.Width) : new GridLength(1, GridUnitType.Star) }); var item = Build(child, each); item.Margin = new Thickness(0, 0, index < node.Children.Count - 1 ? node.Gap : 0, 0); Grid.SetColumn(item, index++); grid.Children.Add(item); }
                element = grid; break;
            }
            case "text":
            {
                var text = new TextBlock { FontSize = node.FontSize, TextWrapping = TextWrapping.Wrap, Foreground = Brush(node.Color, Theme.Text) }; updates.Add(() => text.Text = Expand(node.Text)); element = text; break;
            }
            case "progress":
            {
                var track = new Grid { Height = node.Height > 0 ? node.Height : 7, Background = Theme.Raised, ClipToBounds = true }; var fill = new Border { Background = Brush(node.Color, Theme.Accent), HorizontalAlignment = HorizontalAlignment.Left }; track.Children.Add(fill); updates.Add(() => fill.Width = Math.Max(0, (track.ActualWidth > 0 ? track.ActualWidth : available) * Math.Clamp(Number(node.Source) / node.Maximum, 0, 1))); element = track; break;
            }
            case "button":
            {
                var button = Theme.Button(node.Text, node.Text, () => Run(node.Action, Expand(node.Argument))); button.Background = Theme.Raised; updates.Add(() => { string text = Expand(node.Text); button.Content = text; AutomationProperties.SetName(button, text); }); element = button; break;
            }
            case "input":
            {
                var input = new TextBox { Text = shell.Mods.Value(mod, "field." + node.Key, node.Text), AcceptsReturn = node.Height >= 70, TextWrapping = TextWrapping.Wrap, MaxLength = 10000, MinHeight = 36 };
                input.TextChanged += (_, _) => { if (!preview) shell.Mods.Set(mod, "field." + node.Key, input.Text, false); }; AutomationProperties.SetName(input, node.Key); element = input; break;
            }
            case "check":
            {
                var check = new CheckBox { Content = node.Text, IsChecked = shell.Mods.Value(mod, "field." + node.Key) == "true", FontSize = node.FontSize };
                check.Checked += (_, _) => { if (!preview) shell.Mods.Set(mod, "field." + node.Key, "true", false); }; check.Unchecked += (_, _) => { if (!preview) shell.Mods.Set(mod, "field." + node.Key, "false", false); }; element = check; break;
            }
            case "divider": element = new Border { Height = 1, Background = Brush(node.Color, Theme.Line) }; break;
            case "component":
            {
                var values = new Dictionary<string, string>(node.Values) { ["width"] = Math.Clamp(node.Width > 0 ? node.Width : available, 180, 650).ToString(CultureInfo.InvariantCulture) };
                var child = new ModDefinition { Id = mod.Id + ".node." + node.Key, Name = node.Text.Length > 0 ? node.Text : node.Component, Component = node.Component, Values = values };
                var widget = new Widget(shell, child, preview, demo, true); updates.Add(widget.Tick); element = widget.View; break;
            }
            default: element = new Border(); break;
        }
        if (node.Width > 0 && node.Type != "component") element.Width = node.Width;
        if (node.Height > 0 && node.Type is not "progress") element.Height = node.Height;
        if (node.Background.Length > 0 && node.Type != "component") element = new Border { Background = Brush(node.Background, Theme.Panel), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = element };
        return element;
    }
    internal string Expand(string template) => Tokens.Replace(template, token =>
    {
        string key = token.Groups["key"].Value, format = token.Groups["format"].Value;
        try
        {
            if (key == "time") return (demo?.Now.AddHours(-4).DateTime ?? DateTime.Now).ToString(format.Length > 0 ? format : "HH:mm");
            if (key == "date") return (demo?.Now.AddHours(-4).DateTime ?? DateTime.Today).ToString(format.Length > 0 ? format : "ddd, MMM d");
            if (key == "utc") return (demo?.Now.UtcDateTime ?? DateTime.UtcNow).ToString(format.Length > 0 ? format : "HH:mm");
            if (key == "uptime") return (demo == null ? TimeSpan.FromMilliseconds(Environment.TickCount64) : TimeSpan.FromHours(52)).ToString(format.Length > 0 ? format : @"d\d\ hh\:mm");
            if (key.StartsWith("option.")) return shell.Mods.Value(mod, key[7..]);
            if (key.StartsWith("field.")) return shell.Mods.Value(mod, key);
            if (key == "window") return shell.Tracker.Windows.FirstOrDefault(w => w.Handle == Native.GetForegroundWindow())?.Title ?? "Desktop";
            if (key == "mode") return shell.Games.Active ? "Game" : "Desktop";
            return Number(key.ToLowerInvariant()).ToString(format.Length > 0 ? format : "0", CultureInfo.CurrentCulture);
        }
        catch (FormatException) { return "?"; }
    });
    double Number(string source)
    {
        if (demo != null) return demo.Number(source);
        if (numbers.TryGetValue(source, out double cached)) return cached;
        double value = source switch { "cpu" => shell.CpuUsage, "ram" => shell.MemoryUsage, "volume" => Audio.Read().Volume * 100, "windows" => shell.Tracker.Windows.Count, "notifications" => shell.Notifications.Items.Count, _ => 0 };
        if (source.StartsWith("disk.", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = source.Split('.');
            if (parts.Length == 3 && parts[1].Length == 1)
            {
                if (!drives.TryGetValue(source, out var disk) || DateTime.UtcNow - disk.Time > TimeSpan.FromSeconds(30))
                {
                    try { var drive = new DriveInfo(parts[1] + ":\\"); value = !drive.IsReady ? 0 : parts[2] == "free" ? drive.AvailableFreeSpace / 1073741824.0 : parts[2] == "total" ? drive.TotalSize / 1073741824.0 : 100.0 * (drive.TotalSize - drive.AvailableFreeSpace) / Math.Max(1, drive.TotalSize); } catch { value = 0; }
                    disk = (DateTime.UtcNow, value); drives[source] = disk;
                }
                value = disk.Value;
            }
        }
        numbers[source] = value; return value;
    }
    void Run(string action, string argument)
    {
        if (preview) return;
        switch (action)
        {
            case "apps": shell.ToggleLauncher(shell.CurrentMonitor()); break;
            case "files": shell.OpenFiles(argument.Length > 0 ? Environment.ExpandEnvironmentVariables(argument) : null); break;
            case "mods": shell.OpenMods(); break; case "appearance": shell.OpenAppearance(); break;
            case "profiles": shell.OpenProfiles(); break;
            case "notifications": shell.OpenNotifications(shell.CurrentMonitor()); break; case "quick": shell.OpenQuick(shell.CurrentMonitor()); break;
            case "desktop": shell.ToggleDesktop(); break;
            case "columns": case "grid": case "stack": shell.Arrange(shell.CurrentMonitor(), action); break;
            case "undo": shell.UndoLayout(shell.CurrentMonitor()); break;
            case "launch": if (argument.Length > 0) shell.Launch(Environment.ExpandEnvironmentVariables(argument)); break;
            case "copy": try { Clipboard.SetText(argument); } catch { } break;
            case "mute": Audio.SetMute(!Audio.Read().Muted); break;
            case "volume-up": Audio.SetVolume(Math.Clamp(Audio.Read().Volume + .05f, 0, 1)); break;
            case "volume-down": Audio.SetVolume(Math.Clamp(Audio.Read().Volume - .05f, 0, 1)); break;
            case "previous": Native.Media(0xB1); break; case "play-pause": Native.Media(0xB3); break; case "next": Native.Media(0xB0); break;
        }
    }
    static Brush Brush(string value, Brush fallback) { try { return value.Length > 0 ? Theme.Brush(value) : fallback; } catch { return fallback; } }
    internal static void Validate(WidgetNode root)
    {
        int count = 0; var keys = new HashSet<string>(); Visit(root, 0);
        void Visit(WidgetNode node, int depth)
        {
            if (node == null || ++count > 128 || depth > 10 || !Types.Contains(node.Type) || node.Children == null || node.Values == null) throw new InvalidDataException("A widget layout can contain up to 128 supported nodes and 10 nesting levels.");
            if (node.Text == null || node.Text.Length > 10000 || node.Key == null || node.Color == null || node.Background == null || node.Source == null || node.Source.Length > 100 || node.Argument == null || node.Argument.Length > 10000 || node.Values.Count > 100 || node.Values.Any(p => p.Value == null || p.Value.Length > 10000)) throw new InvalidDataException("Invalid widget text or settings.");
            if (!double.IsFinite(node.FontSize) || node.FontSize < 8 || node.FontSize > 120 || !double.IsFinite(node.Width) || node.Width < 0 || node.Width > 650 || !double.IsFinite(node.Height) || node.Height < 0 || node.Height > 800 || !double.IsFinite(node.Gap) || node.Gap < 0 || node.Gap > 80 || !double.IsFinite(node.Maximum) || node.Maximum <= 0) throw new InvalidDataException("Widget sizes or ranges are outside supported limits.");
            if (node.Type is "input" or "check" or "component") { if (!Regex.IsMatch(node.Key, @"^[a-zA-Z][a-zA-Z0-9_-]{0,39}$") || !keys.Add(node.Key)) throw new InvalidDataException("Interactive widgets need unique keys."); }
            if (node.Type == "button" && !Actions.Contains(node.Action)) throw new InvalidDataException("Unsupported widget action.");
            if (node.Type == "component" && (!Mods.Components.Contains(node.Component) || node.Component == "dashboard")) throw new InvalidDataException("Unsupported nested component.");
            if (node.Children.Count > 0 && node.Type is not "column" and not "row" and not "wrap") throw new InvalidDataException("Only rows, columns, and wraps can contain child nodes.");
            foreach (var child in node.Children) Visit(child, depth + 1);
        }
    }
}
