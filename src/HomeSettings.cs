using System;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Harbor;

internal sealed class HomeSettings : Window
{
    readonly Shell shell;
    Settings draft;
    readonly StackPanel body = new() { Margin = new Thickness(24), MaxWidth = 810 };
    readonly StackPanel pages = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 8, 24, 0) };
    string section = "Layout";
    Panel inputs;
    internal bool Applied { get; private set; }
    internal HomeSettings(Shell shell)
    {
        this.shell = shell; inputs = body; draft = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(shell.Config))!;
        Title = "Harbor — Home settings"; Theme.AppWindow(this, 900, 780); MinWidth = 700; MinHeight = 560;
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, "Customize Harbor Home"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        DockPanel.SetDock(pages, System.Windows.Controls.Dock.Top); root.Children.Add(pages);
        var footer = new DockPanel { Margin = new Thickness(24, 14, 24, 20) }; var buttons = new StackPanel { Orientation = Orientation.Horizontal }; buttons.Children.Add(Theme.Button("Cancel", "Keep the current Home", Close));
        buttons.Children.Add(Theme.Button("Apply and open Home", "Save this layout", () => { HomeLayout.CopyPreferences(draft, shell.Config); shell.Config.Save(); Applied = true; Close(); })); DockPanel.SetDock(buttons, System.Windows.Controls.Dock.Right); footer.Children.Add(buttons); footer.Children.Add(Theme.Button("Reset Home layout", "Reset Home customization only", () => { HomeLayout.CopyPreferences(new Settings { DesktopCustomCategories = draft.DesktopCustomCategories.ToList() }, draft); Render(); })); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Motion.SmoothScroll(scroll); root.Children.Add(scroll); Content = root; Closed += (_, _) => { if (Applied) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => shell.OpenHome(shell.CurrentMonitor()))); }; Render();
    }
    internal void PreparePreview(string page) { section = page; Render(); }
    void Render()
    {
        using var viewport = ScrollPosition.Keep(body);
        pages.Children.Clear(); foreach (string page in new[] { "Layout", "Tabs", "Details" }) { var b = Theme.Button(page, "Customize " + page.ToLowerInvariant(), () => { section = page; Render(); }); b.Background = section == page ? Theme.Raised : Brushes.Transparent; b.Margin = new Thickness(0, 0, 8, 0); pages.Children.Add(b); }
        body.Children.Clear(); body.Children.Add(Theme.Label(section, 27));
        if (section == "Layout")
        {
            Note("Adjust the library to your screen. Everything saves when you apply.");
            var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition()); var left = new StackPanel { Margin = new Thickness(0, 0, 20, 0) }; var right = new StackPanel { Margin = new Thickness(20, 0, 0, 0) }; Grid.SetColumn(right, 1); grid.Children.Add(left); grid.Children.Add(right); body.Children.Add(grid);
            inputs = left; Slider("Columns", 2, 6, draft.HomeColumns, v => draft.HomeColumns = v); Slider("Rows per page", 1, 5, draft.HomeRows, v => draft.HomeRows = v); Slider("App icon size", 24, 80, draft.HomeIconSize, v => draft.HomeIconSize = v); Slider("App text size", 12, 22, draft.HomeTextSize, v => draft.HomeTextSize = v);
            inputs = right; Slider("Window width", 880, 1600, draft.HomeWidth, v => draft.HomeWidth = v, 20); Slider("Window height", 640, 1100, draft.HomeHeight, v => draft.HomeHeight = v, 20); Slider("Sidebar width", 160, 300, draft.HomeSidebarWidth, v => draft.HomeSidebarWidth = v, 5); Slider("Corner roundness", 0, 36, draft.HomeCornerRadius, v => draft.HomeCornerRadius = v); inputs = body;

        }
        else if (section == "Tabs")
        {
            Note("Rename any tab. Home and Frequently used stay at the top, with a permanent divider below them. Other tabs can be hidden and moved.");
            TabRow("Desktop", true); TabRow("Frequently used", true); body.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 16, 0, 12) });
            foreach (string tab in HomeLayout.OrderedTabs(draft).ToArray()) TabRow(tab, false);
            body.Children.Add(Theme.Button("Add a category", "Create a custom Home tab", () => { var name = NameDialog.Ask(this, "New category", ""); if (string.IsNullOrWhiteSpace(name) || DesktopLibraryModel.Groups(draft).Contains(name, StringComparer.OrdinalIgnoreCase)) return; draft.DesktopCustomCategories.Add(name); Render(); }));
        }
        else
        {
            Note("Choose what appears in the library and where it opens.");
            var start = new ComboBox { ItemsSource = new[] { "Frequently used", "Desktop", "All apps" }, SelectedItem = draft.HomeStartTab, Margin = new Thickness(0, 12, 0, 20), MinHeight = 40 }; body.Children.Add(Theme.Label("Start tab", 15)); start.SelectionChanged += (_, _) => draft.HomeStartTab = (string)start.SelectedItem; body.Children.Add(start);
            Toggle("Show app names", draft.HomeShowLabels, v => draft.HomeShowLabels = v); Toggle("Show file and app types", draft.HomeShowKinds, v => draft.HomeShowKinds = v); Toggle("Show counts beside tabs", draft.HomeShowCounts, v => draft.HomeShowCounts = v); Toggle("Show the desktop Recycle Bin", draft.DesktopRecycleBin, v => draft.DesktopRecycleBin = v);
            Note("Frequently used learns from apps opened through Harbor. With no history yet, it starts with your pinned shortcuts.");
        }
        Motion.Enter(body, 0);
    }
    void TabRow(string tab, bool fixedTab)
    {
        var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        var shown = new CheckBox { IsChecked = !draft.HomeHiddenTabs.Contains(tab), IsEnabled = !fixedTab, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) }; shown.Checked += (_, _) => draft.HomeHiddenTabs.Remove(tab); shown.Unchecked += (_, _) => { if (!fixedTab && !draft.HomeHiddenTabs.Contains(tab)) draft.HomeHiddenTabs.Add(tab); }; row.Children.Add(shown);
        var move = new StackPanel { Orientation = Orientation.Horizontal }; foreach (int direction in new[] { -1, 1 }) { var b = Theme.Button(direction < 0 ? "↑" : "↓", direction < 0 ? "Move tab up" : "Move tab down", () => { var order = HomeLayout.OrderedTabs(draft).ToList(); int at = order.IndexOf(tab), to = Math.Clamp(at + direction, 0, order.Count - 1); order.RemoveAt(at); order.Insert(to, tab); draft.HomeTabOrder = order; Render(); }); b.IsEnabled = !fixedTab; b.FontSize = 18; b.MinWidth = 36; move.Children.Add(b); } if (fixedTab) { move.Children.Clear(); move.Children.Add(Theme.Label("Fixed", 12, Theme.Muted)); } DockPanel.SetDock(move, System.Windows.Controls.Dock.Right); row.Children.Add(move);
        var name = new TextBox { Text = HomeLayout.Label(draft, tab), MinHeight = 40 }; name.TextChanged += (_, _) => { if (!string.IsNullOrWhiteSpace(name.Text)) draft.HomeTabNames[tab] = name.Text.Trim(); }; row.Children.Add(name); body.Children.Add(row);
    }
    void Note(string text) => body.Children.Add(new TextBlock { Text = text, FontSize = 14, Foreground = Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
    void Toggle(string title, bool initial, Action<bool> set) { var b = new CheckBox { Content = title, IsChecked = initial, Margin = new Thickness(0, 12, 0, 12), Style = (Style)FindResource("Harbor.Switch") }; b.Checked += (_, _) => set(true); b.Unchecked += (_, _) => set(false); body.Children.Add(b); }
    void Slider(string title, int min, int max, int initial, Action<int> set, int step = 1) { var value = Theme.Label(title + " · " + initial, 14); value.Margin = new Thickness(0, 14, 0, 4); inputs.Children.Add(value); var slider = new Slider { Minimum = min, Maximum = max, Value = initial, TickFrequency = step, IsSnapToTickEnabled = true, Margin = new Thickness(0, 4, 0, 10) }; slider.ValueChanged += (_, _) => { int n = (int)slider.Value; set(n); value.Text = title + " · " + n; }; System.Windows.Automation.AutomationProperties.SetName(slider, title); inputs.Children.Add(slider); }
}
