using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

internal sealed class PluginQuickSettings : Window
{
    readonly Shell shell;
    readonly ModDefinition mod;
    readonly Dictionary<string, string> draft = new();
    StackPanel page = new();
    readonly Grid pages = new() { ClipToBounds = true };
    readonly Grid navigation = new() { Height = 38 };
    readonly Border tabHighlight = new() { Background = Theme.Raised, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    readonly TranslateTransform tabOffset = new();
    readonly Dictionary<string, Button> tabs = new();
    readonly QuickSettingsMotion motion;
    readonly TextBlock liveReading;
    readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(1) };
    string section = "Visibility";
    bool closing, closeReady, closed;
    internal string ModId => mod.Id;
    internal PluginQuickSettings(Shell shell, ModDefinition mod, IntPtr monitor, Rect? anchor = null)
    {
        this.shell = shell; this.mod = mod; Title = mod.Name + " controls";
        Theme.PopupWindow(this, 470, 670, monitor, false);
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new DockPanel(); var close = Theme.Button(UiIcons.Icon("close", 16), "Cancel changes", Close); close.Padding = new Thickness(6); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close);
        var icon = UiIcons.Icon(PluginPresentation.Icon(mod), 25); icon.Margin = new Thickness(0, 0, 12, 0); DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left); header.Children.Add(icon);
        var words = new StackPanel(); var title = Theme.Label(mod.Name, 21); title.TextTrimming = TextTrimming.CharacterEllipsis; words.Children.Add(title);
        liveReading = Theme.Label(PluginPresentation.Reading(shell, mod, Settings.TestDataDir != null), 13, Theme.Accent); liveReading.Margin = new Thickness(0, 5, 0, 0); words.Children.Add(liveReading); header.Children.Add(words);
        DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        navigation.Margin = new Thickness(0, 18, 0, 16); tabHighlight.RenderTransform = tabOffset; navigation.Children.Add(tabHighlight);
        var tabRow = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 }; navigation.Children.Add(tabRow);
        foreach (string name in new[] { "Visibility", "Position", "Appearance" }) { var button = Theme.Button(name, "Edit " + name.ToLowerInvariant(), () => { int previous = Array.IndexOf(new[] { "Visibility", "Position", "Appearance" }, section); section = name; Render(Math.Sign(Array.IndexOf(new[] { "Visibility", "Position", "Appearance" }, section) - previous)); }); button.FontSize = 13; button.Background = Brushes.Transparent; tabs[name] = button; tabRow.Children.Add(button); }
        navigation.SizeChanged += (_, _) => MoveHighlight(); DockPanel.SetDock(navigation, System.Windows.Controls.Dock.Top); root.Children.Add(navigation);
        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        var done = Theme.Button("Save", "Save control settings", () => { Apply(); Close(); }); done.Background = Theme.Accent; done.Foreground = Theme.Bg; done.Padding = new Thickness(22, 9, 22, 9); DockPanel.SetDock(done, System.Windows.Controls.Dock.Right); footer.Children.Add(done);
        var cancel = Theme.Button("Cancel", "Discard control changes", Close); DockPanel.SetDock(cancel, System.Windows.Controls.Dock.Right); footer.Children.Add(cancel);
        var details = Theme.Button(UiIcons.Label("Plugin settings", 12, "controls"), "All settings and usage instructions", () => { Close(); shell.OpenPluginSettings(mod); }); details.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(details);
        DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(pages); var surface = Theme.Card(root, 16); Content = surface; motion = new QuickSettingsMotion(surface);
        SourceInitialized += (_, _) => { if (anchor is not Rect a) return; var area = PluginSnapping.WorkArea(shell, monitor); double scale = Native.Scale(monitor), w = Width * scale, h = Height * scale; double left = a.Left < area.Left + area.Width / 2 ? a.Right + 10 : a.Left - w - 10; Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), (int)Math.Clamp(left, area.Left + 8, Math.Max(area.Left + 8, area.Right - w - 8)), (int)Math.Clamp(a.Top - 24, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - h - 8)), (int)w, (int)h, 0x50); };
        Loaded += (_, _) => { refresh.Start(); MoveHighlight(); motion.Enter(); };
        refresh.Tick += (_, _) => liveReading.Text = PluginPresentation.Reading(shell, mod);
        Closing += (_, e) => { if (closeReady || !motion.Enabled) return; e.Cancel = true; if (closing) return; closing = true; refresh.Stop(); surface.IsHitTestVisible = false; motion.Exit(() => { if (closed) return; closeReady = true; Close(); }); };
        Closed += (_, _) => { closed = true; refresh.Stop(); motion.Dispose(); };
        Deactivated += (_, _) => Dispatcher.BeginInvoke(new Action(() => { if (!IsVisible) return; var foreground = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(foreground, out uint pid); if (foreground != IntPtr.Zero && pid != Environment.ProcessId) Close(); }));
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        Render();
    }
    string Value(string key, string fallback = "") => draft.TryGetValue(key, out string? value) ? value : shell.Mods.Value(mod, key, fallback);
    internal IReadOnlyDictionary<string, string> Pending => draft;
    internal void PreparePreview(string name) { section = name; Render(); }
    void Apply()
    {
        foreach (var pair in draft) shell.Mods.Set(mod, pair.Key, pair.Value, false);
        if (draft.Count > 0) shell.RebuildMods();
    }
    void Render(int direction = 0)
    {
        page = new StackPanel(); foreach (var tab in tabs) tab.Value.Foreground = tab.Key == section ? Theme.Accent : Theme.Muted;
        if (section == "Visibility") VisibilityPage(); else if (section == "Position") PositionPage(); else AppearancePage();
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Motion.SmoothScroll(scroll); motion.Swap(pages, scroll, direction); MoveHighlight();
    }
    void MoveHighlight() { if (motion == null) return; double width = navigation.ActualWidth / 3; tabHighlight.Width = Math.Max(0, width - 4); motion.Indicator(tabOffset, Array.IndexOf(new[] { "Visibility", "Position", "Appearance" }, section) * width); }
    void Heading(string title, string description)
    {
        page.Children.Add(Theme.Label(title, 16)); Hint(description, new Thickness(0, 6, 4, 12));
    }
    void Hint(string text, Thickness? margin = null) => page.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, Foreground = Theme.Muted, Margin = margin ?? new Thickness(0, 6, 4, 10), LineHeight = 18 });
    void VisibilityPage()
    {
        Heading("When should it appear?", "Each tool has its own rule. Your position and readings stay the same.");
        string mode = Value("visibility", PluginVisibility.Always);
        var choices = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 4, 0) };
        foreach (var choice in new[] { (PluginVisibility.Apps, "Apps screen", "apps"), (PluginVisibility.Selected, "Chosen apps", "profiles"), (PluginVisibility.Always, "Always", "pin") })
        {
            var body = new StackPanel(); var icon = UiIcons.Icon(choice.Item3, 18); icon.HorizontalAlignment = HorizontalAlignment.Center; body.Children.Add(icon); var label = Theme.Label(choice.Item2, 12); label.HorizontalAlignment = HorizontalAlignment.Center; label.Margin = new Thickness(0, 8, 0, 0); body.Children.Add(label);
            var button = Theme.Button(body, choice.Item1, () => { draft["visibility"] = choice.Item1; if (choice.Item1 == PluginVisibility.Selected && mode != choice.Item1) { draft["always-on"] = "Yes"; if (Value("access") == PluginPresentation.Desktop) draft["access"] = PluginPresentation.Floating; } Render(); }); button.Background = mode == choice.Item1 ? Theme.Raised : Theme.Panel; button.BorderBrush = mode == choice.Item1 ? Theme.Accent : Theme.Line; button.BorderThickness = new Thickness(1); button.Padding = new Thickness(6, 11, 6, 11); button.Margin = new Thickness(0, 0, 6, 0); choices.Children.Add(button);
        }
        page.Children.Add(choices);
        Hint(mode == PluginVisibility.Selected ? "Show while any chosen app has a window open, including minimized windows." : mode == PluginVisibility.Apps ? "Show with Harbor's Apps screen. Hide as soon as it closes." : "Keep the compact control available across your apps.", new Thickness(0, 10, 4, 14));
        if (mode == PluginVisibility.Selected) AppPicker();
        var pinned = new CheckBox { Content = "Keep the full tool expanded", IsChecked = Value("always-on", "No") == "Yes", Style = (Style)Application.Current.FindResource("Harbor.Switch"), Margin = new Thickness(0, 14, 5, 0), FontSize = 13 };
        AutomationProperties.SetName(pinned, "Keep tool expanded while its visibility rule matches");
        pinned.Checked += (_, _) => { draft["always-on"] = "Yes"; if (Value("access") == PluginPresentation.Desktop) draft["access"] = PluginPresentation.Floating; }; pinned.Unchecked += (_, _) => draft["always-on"] = "No"; page.Children.Add(pinned);
        Hint("Off: click to expand. On: stay expanded while the rule matches. Games still hide tools on their monitor.");
    }
    void AppPicker()
    {
        page.Children.Add(Theme.Label("Choose apps", 14));
        var selected = Value("visible-apps").Split(new[] { '\n', '\r' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var count = Theme.Label("", 12, Theme.Accent); count.Margin = new Thickness(0, 6, 0, 8); page.Children.Add(count);
        var search = new TextBox { FontSize = 13, MinHeight = 34, Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 4, 6) }; AutomationProperties.SetName(search, "Search installed and open apps"); page.Children.Add(Theme.Placeholder(search, "Search installed or open apps"));
        var all = shell.Catalog.Apps.Where(a => a.Key.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || a.Key.Contains('!')).Select(a => (Name: PluginVisibility.AppName(a.Name, PluginVisibility.AppKey(a)), Key: PluginVisibility.AppKey(a), Icon: a.Path))
            .Concat(shell.Tracker.Windows.Select(w => (Name: shell.Catalog.Apps.FirstOrDefault(a => Shell.KeysMatch(a.Key, w.Key))?.Name ?? w.ProcessName, Key: w.AppId.Length > 0 ? w.AppId : w.ProcessName + ".exe", Icon: w.LaunchPath)))
            .Concat(selected.Select(key => (Name: PluginVisibility.RuleName(key), Key: key, Icon: key))).GroupBy(a => a.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        var entries = new StackPanel(); var list = new ScrollViewer { Content = entries, Height = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Theme.Bg, Margin = new Thickness(0, 0, 4, 0) }; page.Children.Add(list);
        search.TextChanged += (_, _) => Populate(); Populate();
        var input = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, MinHeight = 68, MaxHeight = 120, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Text = string.Join("\n", selected) }; AutomationProperties.SetName(input, "Chosen app rules, one per line");
        input.TextChanged += (_, _) => { selected.Clear(); foreach (string key in input.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) selected.Add(key); Save(); Populate(); };
        bool manualOpen = false; var inputArea = new Border { Child = input, ClipToBounds = true, Visibility = Visibility.Collapsed }; var manual = Theme.Button(UiIcons.Label("Add executable names or app IDs", 12, "plus"), "Edit executable names or app IDs, one per line", () => { manualOpen = !manualOpen; if (manualOpen) input.Text = string.Join("\n", selected); motion.Expand(inputArea, manualOpen); }); manual.Padding = new Thickness(0, 8, 0, 4); manual.HorizontalAlignment = HorizontalAlignment.Left; page.Children.Add(manual); page.Children.Add(inputArea);
        void Save() { draft["visible-apps"] = string.Join("\n", selected); count.Text = selected.Count + " selected · any one can show this tool"; }
        void Populate()
        {
            entries.Children.Clear(); count.Text = selected.Count + " selected · any one can show this tool";
            foreach (var app in all.OrderByDescending(a => selected.Contains(a.Key)).ThenBy(a => a.Name).Where(a => a.Name.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase) || a.Key.Contains(search.Text.Trim(), StringComparison.OrdinalIgnoreCase)).Take(100))
            {
                var row = new DockPanel(); var icon = Theme.AppIcon(app.Icon, app.Name, 22); icon.Margin = new Thickness(0, 0, 10, 0); DockPanel.SetDock(icon, System.Windows.Controls.Dock.Left); row.Children.Add(icon); var words = new StackPanel(); words.Children.Add(Theme.Label(app.Name, 13)); var detail = Theme.Label(PluginVisibility.RuleName(app.Key), 11, Theme.Muted); detail.TextTrimming = TextTrimming.CharacterEllipsis; detail.MaxWidth = 305; words.Children.Add(detail); row.Children.Add(words);
                var choice = new CheckBox { Content = row, IsChecked = selected.Contains(app.Key), Margin = new Thickness(10, 5, 8, 5), HorizontalContentAlignment = HorizontalAlignment.Stretch }; AutomationProperties.SetName(choice, "Show with " + app.Name);
                choice.Checked += (_, _) => { selected.Add(app.Key); Save(); }; choice.Unchecked += (_, _) => { selected.Remove(app.Key); Save(); }; entries.Children.Add(choice);
            }
            if (entries.Children.Count == 0) entries.Children.Add(new TextBlock { Text = "No matching apps. Add an executable name below, such as Discord.exe.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(10), FontSize = 12 });
        }
    }
    void PositionPage()
    {
        Heading("How you reach it", "Change the control's home without moving its saved content.");
        Select("access", "Control style", PluginPresentation.Modes, PluginPresentation.Edge);
        Hint(Value("access") switch { PluginPresentation.Dock => "A button inside Minimalists Dock at the top.", PluginPresentation.Desktop => "A full panel behind your apps on Harbor's desktop.", PluginPresentation.Floating => "A compact control you can place freely.", _ => "A compact tab that snaps to edges or the center box." });
        Select("screen", "Monitor", new[] { "primary", "secondary", "all" }, "primary", new[] { "Main monitor", "Second monitor", "Every monitor" });
        page.Children.Add(Theme.Label("Snap position", 14)); Hint("Center box points leave a wide open space in the middle. Screen positions stay below the Dock and above the taskbar.");
        var positions = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        foreach (var pair in new[] { ("Auto", "Auto"), ("Free", "Free"), ("Box ↖", "Center box: upper left"), ("Box ↗", "Center box: upper right"), ("Box ↙", "Center box: lower left"), ("Box ↘", "Center box: lower right"), ("Left edge", "Left"), ("Right edge", "Right"), ("Top edge", "Top"), ("Bottom edge", "Bottom") })
        { var button = Theme.Button(pair.Item1, pair.Item2, () => { draft["snap"] = pair.Item2; Render(); }); button.Background = Value("snap", "Auto") == pair.Item2 ? Theme.Raised : Brushes.Transparent; button.Foreground = Value("snap", "Auto") == pair.Item2 ? Theme.Accent : Theme.Text; button.Margin = new Thickness(0, 0, 5, 5); button.MinWidth = 82; positions.Children.Add(button); }
        page.Children.Add(positions);
        Select("snap", "All snap positions", PluginSnapping.Positions, "Auto");
        var reset = Theme.Button(UiIcons.Label("Reset position", 12, "refresh"), "Reset only this control's position", () => { draft["snap"] = "Auto"; draft["control-x"] = ""; draft["control-y"] = ""; Render(); }); reset.HorizontalAlignment = HorizontalAlignment.Left; reset.Margin = new Thickness(0, 12, 0, 0); page.Children.Add(reset);
    }
    void AppearancePage()
    {
        Heading("Compact control", "Set what you see before expanding the tool.");
        Select("compact-reading", "Content", new[] { "Live reading", "Name", "Icon only" }, "Live reading");
        string key = PluginPresentation.Key(mod);
        if (key is "system" or "resource-desk") Select("meter-mode", "Readings", new[] { "CPU + RAM", "CPU", "RAM" }, "CPU + RAM");
        if (key == "storage") { Text("drives", "Drive letters", "Separate drives with commas, for example C:,D:,E:"); Select("drive-mode", "Drive display", new[] { "All drives", "First drive", "Cycle drives", "Used percent" }, "All drives"); }
        if (key is "clock" or "world-clock") Text("format", "Time format", "HH:mm is 24-hour time. h:mm tt is 12-hour time. Add :ss for seconds.");
        Slider("control-size", "Height", 30, 60, 36);
        Slider("control-width", "Minimum width", 44, 240, 112);
        Hint("Live readings can grow a little wider to fit. This changes the compact control, not the expanded panel.");
        Text("compact-format", "Custom reading (optional)", "Examples: CPU {cpu:0}% · RAM {ram:0}% or {time:HH:mm:ss}. Leave blank to use the mode above.");
    }
    void Select(string key, string label, string[] choices, string fallback, string[]? names = null)
    {
        page.Children.Add(Theme.Label(label, 13));
        var box = new ComboBox { ItemsSource = names ?? choices, SelectedIndex = Math.Max(0, Array.IndexOf(choices, Value(key, fallback))), Margin = new Thickness(0, 6, 4, 12), FontSize = 13, MinHeight = 36 };
        AutomationProperties.SetName(box, label); box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) draft[key] = choices[box.SelectedIndex]; }; page.Children.Add(box);
    }
    void Text(string key, string label, string hint)
    {
        page.Children.Add(Theme.Label(label, 13)); var box = new TextBox { Text = Value(key), FontSize = 13, Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 6, 4, 0) }; AutomationProperties.SetName(box, label); box.TextChanged += (_, _) => draft[key] = box.Text; page.Children.Add(box); Hint(hint);
    }
    void Slider(string key, string label, double min, double max, double fallback)
    {
        double value = double.TryParse(Value(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && double.IsFinite(parsed) ? Math.Clamp(parsed, min, max) : fallback;
        var title = Theme.Label(label + " · " + value.ToString("0") + " px", 13); page.Children.Add(title); var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 5, 4, 14) }; AutomationProperties.SetName(slider, label);
        slider.ValueChanged += (_, _) => { draft[key] = slider.Value.ToString("0", CultureInfo.InvariantCulture); title.Text = label + " · " + slider.Value.ToString("0") + " px"; }; page.Children.Add(slider);
    }
}
