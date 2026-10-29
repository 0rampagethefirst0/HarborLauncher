using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Harbor;

internal sealed class Appearance : Window
{
    readonly Shell shell;
    readonly Dictionary<string, TextBox> colors = new();
    readonly Dictionary<string, CheckBox> toggles = new();
    readonly Slider top = new() { Minimum = 28, Maximum = 64, TickFrequency = 1, IsSnapToTickEnabled = true };
    readonly Slider dock = new() { Minimum = 64, Maximum = 128, TickFrequency = 1, IsSnapToTickEnabled = true };
    readonly Slider size = new() { Minimum = 24, Maximum = 44, TickFrequency = 1, IsSnapToTickEnabled = true };
    readonly Slider radius = new() { Minimum = 0, Maximum = 36, TickFrequency = 1, IsSnapToTickEnabled = true };
    readonly Slider faded = new() { Minimum = 20, Maximum = 80, TickFrequency = 1, IsSnapToTickEnabled = true };
    readonly Slider hold = new() { Minimum = 1, Maximum = 10, TickFrequency = 1, IsSnapToTickEnabled = true };
    readonly ComboBox alignment = new() { ItemsSource = new[] { "left", "center", "right" }, MinWidth = 150 };
    readonly TextBlock status = Theme.Label("", 12, Theme.Muted);
    readonly Border previewBar = new(), previewDock = new();
    readonly Grid preview = new();
    internal Appearance(Shell shell)
    {
        this.shell = shell; Title = "Harbor — appearance"; Theme.AppWindow(this, 1000, 760);
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, "Appearance"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var footer = new DockPanel { Margin = new Thickness(22, 12, 22, 18) }; var apply = Theme.Button("Apply appearance", "Apply appearance settings", Apply); apply.Background = Theme.Raised; DockPanel.SetDock(apply, System.Windows.Controls.Dock.Right); footer.Children.Add(apply); footer.Children.Add(status); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var grid = new Grid { Margin = new Thickness(22, 18, 22, 0) }; grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var body = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
        Heading("Size and position");
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 12) };
        foreach (var preset in new[] { ("Compact", 30, 64, 24), ("Balanced", 38, 80, 32), ("Large", 46, 96, 40) }) { var button = Theme.Button(preset.Item1, preset.Item1 + " appearance preset", () => { top.Value = preset.Item2; dock.Value = preset.Item3; size.Value = preset.Item4; Preview(); }); button.Background = Theme.Raised; button.Margin = new Thickness(0, 0, 6, 0); presets.Children.Add(button); } body.Children.Add(presets);
        Slider("Dock height (top)", top, shell.Config.TopBarHeight); Slider("Taskbar height", dock, shell.Config.DockHeight); Slider("Dock icon size", size, shell.Config.IconSize); Slider("Taskbar corner radius", radius, shell.Config.DockRadius);
        var row = new DockPanel { Margin = new Thickness(0, 12, 0, 16) }; row.Children.Add(Theme.Label("Taskbar alignment", 13)); alignment.SelectedItem = shell.Config.DockAlignment; alignment.SelectionChanged += (_, _) => Preview(); DockPanel.SetDock(alignment, System.Windows.Controls.Dock.Right); row.Children.Insert(0, alignment); body.Children.Add(row);
        Heading("Taskbar interaction");
        Toggle("hover", "Let clicks through the taskbar when it covers an app", shell.Config.DockHoverThrough);
        body.Children.Add(new TextBlock { Text = "Hover to reach the app below. The first right-click activates the taskbar; another right-click opens app menus. Typing in an app keeps its taskbar faded and click-through.", FontSize = 12, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 10), LineHeight = 19 });
        Slider("Faded taskbar opacity", faded, shell.Config.DockFadeOpacity * 100, "%"); Slider("Stay usable after leaving", hold, shell.Config.DockInteractionDelay / 1000.0, "s");
        Heading("Dock buttons (top)");
        Toggle("title", "Active window title", shell.Config.ShowActiveTitle); Toggle("clock", "Clock and calendar", shell.Config.ShowClock); Toggle("stats", "CPU and memory", shell.Config.ShowStats); Toggle("media", "Media controls", shell.Config.ShowMedia); Toggle("network", "Network status", shell.Config.ShowNetwork); Toggle("files", "Files button", shell.Config.ShowFilesButton); Toggle("mods", "Mods button", shell.Config.ShowModsButton);
        Heading("Base colors");
        Color("background", "Bars", shell.Config.BackgroundColor); Color("panel", "Panels", shell.Config.PanelColor); Color("accent", "Accent", shell.Config.Accent); Color("text", "Text", shell.Config.TextColor); Color("muted", "Secondary text", shell.Config.MutedColor); Color("raised", "Controls", shell.Config.RaisedColor); Color("border", "Borders", shell.Config.BorderColor);
        var theme = shell.Mods.Enabled.LastOrDefault(m => m.Kind == "theme");
        if (theme != null) { var note = new TextBlock { Text = theme.Name + " is enabled and can override base colors. Its colors can also be edited in Mods.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(0, 12, 0, 8) }; body.Children.Add(note); body.Children.Add(Theme.Button("Open theme settings", "Open theme settings in Mods", shell.OpenMods)); }
        grid.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var previewBody = new StackPanel(); previewBody.Children.Add(Theme.Label("PREVIEW", 10, Theme.Muted));
        preview.Height = 260; preview.Background = Theme.Brush("#29374B"); preview.Margin = new Thickness(0, 14, 0, 14); preview.ClipToBounds = true;
        previewBar.VerticalAlignment = VerticalAlignment.Top; previewBar.Child = new TextBlock { Text = "Harbor                         12:34", FontSize = 11, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center }; preview.Children.Add(previewBar);
        previewDock.VerticalAlignment = VerticalAlignment.Bottom; previewDock.Margin = new Thickness(12); previewDock.Width = 205;
        var icons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center }; for (int i = 0; i < 5; i++) icons.Children.Add(new Border { Width = 22, Height = 22, Margin = new Thickness(6), CornerRadius = new CornerRadius(5), Background = Theme.Brush(new[] { "#8AB4FF", "#84D4AC", "#FFB08A", "#D0A3EE", "#C9D2E3" }[i]) }); previewDock.Child = icons; preview.Children.Add(previewDock); previewBody.Children.Add(preview);
        previewBody.Children.Add(new TextBlock { Text = "Game hiding stays monitor-specific. Screen space follows the sizes you apply here.", FontSize = 13, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, LineHeight = 21 });
        Grid.SetColumn(previewBody, 1); grid.Children.Add(previewBody); root.Children.Add(grid); Content = root; Preview();
        void Heading(string text) { var label = Theme.Label(text.ToUpperInvariant(), 10, Theme.Muted); label.Margin = new Thickness(0, 18, 0, 5); body.Children.Add(label); }
        void Slider(string label, Slider slider, double initial, string unit = "px") { var number = Theme.Label("", 12, Theme.Muted); var line = new DockPanel { Margin = new Thickness(0, 8, 0, 5) }; DockPanel.SetDock(number, System.Windows.Controls.Dock.Right); line.Children.Add(number); line.Children.Add(Theme.Label(label, 13)); body.Children.Add(line); slider.Value = initial; number.Text = initial.ToString("0") + " " + unit; slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString("0") + " " + unit; Preview(); }; AutomationProperties.SetName(slider, label); body.Children.Add(slider); }
        void Toggle(string key, string label, bool value) { var box = new CheckBox { Content = label, IsChecked = value, FontSize = 13, Margin = new Thickness(0, 5, 0, 5) }; toggles[key] = box; box.Checked += (_, _) => Preview(); box.Unchecked += (_, _) => Preview(); body.Children.Add(box); }
        void Color(string key, string label, string value) { var line = new DockPanel { Margin = new Thickness(0, 8, 0, 0) }; var box = new TextBox { Text = value, Width = 126, MaxLength = 7, FontSize = 13, Padding = new Thickness(8, 5, 8, 5) }; AutomationProperties.SetName(box, label + " color"); colors[key] = box; box.TextChanged += (_, _) => Preview(); DockPanel.SetDock(box, System.Windows.Controls.Dock.Right); line.Children.Add(box); line.Children.Add(Theme.Label(label, 13)); body.Children.Add(line); }
    }
    void Preview()
    {
        if (colors.Count == 0) return;
        try { var background = Theme.Brush(colors["background"].Text); previewBar.Background = previewDock.Background = background; ((TextBlock)previewBar.Child).Foreground = Theme.Brush(colors["text"].Text); } catch { }
        previewBar.Height = top.Value * .6; previewDock.Height = dock.Value * .6; previewDock.CornerRadius = new CornerRadius(radius.Value * .7); previewDock.HorizontalAlignment = alignment.SelectedItem as string == "left" ? HorizontalAlignment.Left : alignment.SelectedItem as string == "right" ? HorizontalAlignment.Right : HorizontalAlignment.Center;
        previewDock.Opacity = toggles.GetValueOrDefault("hover")?.IsChecked == true ? faded.Value / 100 : 1;
    }
    void Apply()
    {
        foreach (var color in colors) if (!System.Text.RegularExpressions.Regex.IsMatch(color.Value.Text, "^#[0-9a-fA-F]{6}$")) { status.Text = "Use six-digit colors such as #8AB4FF."; color.Value.Focus(); return; }
        var c = shell.Config; var oldVisibility = c.TopDockItems.ToDictionary(i => i.Id, i => BarLayout.Legacy(c, i.Id)); c.TopBarHeight = (int)top.Value; c.DockHeight = (int)dock.Value; c.IconSize = (int)size.Value; c.DockRadius = (int)radius.Value; c.DockAlignment = alignment.SelectedItem as string ?? "center";
        c.DockHoverThrough = toggles["hover"].IsChecked == true; c.DockFadeOpacity = faded.Value / 100; c.DockInteractionDelay = (int)hold.Value * 1000;
        c.ShowActiveTitle = toggles["title"].IsChecked == true; c.ShowClock = toggles["clock"].IsChecked == true; c.ShowStats = toggles["stats"].IsChecked == true; c.ShowMedia = toggles["media"].IsChecked == true; c.ShowNetwork = toggles["network"].IsChecked == true; c.ShowFilesButton = toggles["files"].IsChecked == true; c.ShowModsButton = toggles["mods"].IsChecked == true;
        c.BackgroundColor = colors["background"].Text; c.PanelColor = colors["panel"].Text; c.Accent = colors["accent"].Text; c.TextColor = colors["text"].Text; c.MutedColor = colors["muted"].Text; c.RaisedColor = colors["raised"].Text; c.BorderColor = colors["border"].Text;
        foreach (var item in c.TopDockItems.Where(i => !i.Id.StartsWith("divider.") && oldVisibility.GetValueOrDefault(i.Id) != BarLayout.Legacy(c, i.Id))) item.Enabled = BarLayout.Legacy(c, item.Id);
        shell.ApplySettings(true); dock.Value = c.DockHeight; status.Text = "Appearance applied and saved.";
    }
}
