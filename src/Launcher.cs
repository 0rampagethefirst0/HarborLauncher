using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Harbor;

internal sealed class Launcher : Window
{
    readonly Shell shell;
    readonly TextBox search;
    readonly ListBox results;
    readonly StackPanel pinArea;
    readonly TextBlock subtitle, count;
    readonly System.Windows.Threading.DispatcherTimer debounce;
    System.Windows.Threading.DispatcherTimer? launchTimeout;
    bool closing, launching;
    internal Launcher(Shell shell, IntPtr monitor)
    {
        this.shell = shell;
        Title = "Harbor — apps";
        Theme.PopupWindow(this, 800, 690, monitor);
        var layout = new Grid { Margin = new Thickness(24) };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        controls.Children.Add(Theme.IconButton("\uE713", "Harbor settings", () => { Close(); shell.OpenSettings(); }));
        controls.Children.Add(Theme.IconButton("\uE8BB", "Close apps menu", Close)); DockPanel.SetDock(controls, System.Windows.Controls.Dock.Right); header.Children.Add(controls);
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(Theme.Logo(32));
        var words = new StackPanel { Margin = new Thickness(12, 0, 0, 0) };
        words.Children.Add(new TextBlock { Text = "Apps", FontSize = 23, FontWeight = FontWeights.SemiBold });
        subtitle = Theme.Label("", 12, Theme.Muted); words.Children.Add(subtitle); brand.Children.Add(words); header.Children.Add(brand);
        layout.Children.Add(header);
        var searchGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        search = new TextBox { Height = 46, VerticalContentAlignment = VerticalAlignment.Center, FontSize = 15 };
        AutomationProperties.SetName(search, "Search apps");
        var placeholder = Theme.Label("Search your apps…", 15, Theme.Muted); placeholder.Margin = new Thickness(13, 0, 0, 0); placeholder.IsHitTestVisible = false;
        searchGrid.Children.Add(search); searchGrid.Children.Add(placeholder); Grid.SetRow(searchGrid, 1); layout.Children.Add(searchGrid);
        pinArea = new StackPanel();
        var pinHeader = Theme.Label("PINNED", 10, Theme.Muted); pinHeader.FontWeight = FontWeights.SemiBold; pinHeader.Margin = new Thickness(0, 0, 0, 8); pinArea.Children.Add(pinHeader);
        var pins = new UniformGrid { Columns = 7, Margin = new Thickness(0, 0, 0, 16) };
        foreach (var pin in shell.Config.Pins.Take(14))
        {
            var content = new StackPanel { Width = 90, Margin = new Thickness(0, 4, 0, 4) };
            var icon = Theme.AppIcon(pin.Path, pin.Name, 30); content.Children.Add(icon);
            content.Children.Add(new TextBlock { Text = pin.Name, FontSize = 11, Foreground = Theme.Text, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 7, 0, 0) });
            var b = Theme.Button(content, "Open " + pin.Name, () => Open(pin.Path)); b.Padding = new Thickness(2); pins.Children.Add(b);
        }
        pinArea.Children.Add(pins); Grid.SetRow(pinArea, 2); layout.Children.Add(pinArea);
        var listArea = new Grid();
        listArea.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); listArea.RowDefinitions.Add(new RowDefinition());
        count = Theme.Label("ALL APPS", 10, Theme.Muted); count.Margin = new Thickness(0, 0, 0, 6); listArea.Children.Add(count);
        results = new ListBox { SelectionMode = SelectionMode.Single };
        AutomationProperties.SetName(results, "App results");
        VirtualizingPanel.SetIsVirtualizing(results, true);
        VirtualizingPanel.SetVirtualizationMode(results, VirtualizationMode.Recycling);
        Grid.SetRow(results, 1); listArea.Children.Add(results); Grid.SetRow(listArea, 3); layout.Children.Add(listArea);
        var footer = new DockPanel { Margin = new Thickness(0, 16, 0, 0) };
        footer.Children.Add(Theme.Label("Enter to open   ·   Right-click to pin   ·   Esc to close", 11, Theme.Muted));
        var user = Theme.Label(Environment.UserName, 11, Theme.Muted); user.HorizontalAlignment = HorizontalAlignment.Right; footer.Children.Add(user);
        Grid.SetRow(footer, 4); layout.Children.Add(footer);
        Content = Theme.Card(layout, 18);
        debounce = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        debounce.Tick += (_, _) => { debounce.Stop(); Populate(); };
        search.TextChanged += (_, _) => { placeholder.Visibility = search.Text.Length > 0 ? Visibility.Collapsed : Visibility.Visible; debounce.Stop(); debounce.Start(); };
        search.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Down && results.Items.Count > 0) { results.SelectedIndex = Math.Min(results.SelectedIndex + 1, results.Items.Count - 1); results.ScrollIntoView(results.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Up) { results.SelectedIndex = Math.Max(0, results.SelectedIndex - 1); results.ScrollIntoView(results.SelectedItem); e.Handled = true; }
            else if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; }
        };
        results.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { OpenSelected(); e.Handled = true; } };
        Loaded += (_, _) => { Populate(); Activate(); search.Focus(); KeyboardFocus(); };
        Closing += (_, _) => closing = true;
        // Shell's foreground watcher closes Apps for external apps. Harbor tool
        // controls and their settings may take focus without closing Apps.
        Closed += (_, _) => { closing = true; debounce.Stop(); launchTimeout?.Stop(); shell.Catalog.Changed -= CatalogChanged; };
        shell.Catalog.Changed += CatalogChanged;
    }
    void KeyboardFocus() => System.Windows.Input.Keyboard.Focus(search);
    internal void PreparePreview(string text = "") { search.Text = text; Populate(); }
    bool AnyMenuOpen() => results.Items.OfType<ListBoxItem>().Any(i => i.ContextMenu?.IsOpen == true);
    void CatalogChanged() { if (!closing) Populate(); }
    void Populate()
    {
        string query = search.Text.Trim();
        pinArea.Visibility = query.Length > 0 || shell.Config.Pins.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        subtitle.Text = shell.Catalog.Ready ? $"{shell.Catalog.Apps.Count} installed apps" : "Finding your apps…";
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<AppEntry> apps = shell.Catalog.Apps;
        if (terms.Length > 0) apps = apps.Where(a => terms.All(t => a.Name.Contains(t, StringComparison.CurrentCultureIgnoreCase))).OrderBy(a => !a.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)).ThenBy(a => a.Name);
        var matches = apps.ToList();
        count.Text = query.Length == 0 ? "ALL APPS" : matches.Count == 0 ? "NO APPS FOUND" : $"{matches.Count} RESULT{(matches.Count == 1 ? "" : "S")}";
        results.Items.Clear();
        foreach (var app in matches)
        {
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(43) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(Theme.AppIcon(app.Path, app.Name, 28));
            var label = Theme.Label(app.Name); label.TextTrimming = TextTrimming.CharacterEllipsis; Grid.SetColumn(label, 1); row.Children.Add(label);
            bool pinned = shell.Config.Pins.Any(p => p.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase));
            if (pinned) { var pinLabel = Theme.Label("Pinned", 10, Theme.Muted); Grid.SetColumn(pinLabel, 2); row.Children.Add(pinLabel); }
            var openButton = Theme.Button(row, "Open " + app.Name, () => Open(app.Path));
            openButton.Padding = new Thickness(0); openButton.HorizontalContentAlignment = HorizontalAlignment.Stretch; openButton.Focusable = false;
            var item = new ListBoxItem { Content = openButton, Tag = app };
            AutomationProperties.SetName(item, app.Name);
            var menu = new ContextMenu();
            menu.Items.Add(Theme.Item("Open", () => Open(app.Path)));
            menu.Items.Add(Theme.Item(pinned ? "Unpin from dock" : "Pin to dock", () => { if (pinned) shell.Unpin(shell.Config.Pins.First(p => p.Path.Equals(app.Path, StringComparison.OrdinalIgnoreCase))); else shell.Pin(app.ToPin()); Populate(); }));
            item.ContextMenu = menu;
            results.Items.Add(item);
        }
        if (results.Items.Count > 0) results.SelectedIndex = 0;
    }
    void OpenSelected() { if (results.SelectedItem is ListBoxItem { Tag: AppEntry app }) Open(app.Path); }
    void Open(string path)
    {
        if (launching) return;
        launching = true;
        shell.Launch(path);
        if (closing) return;
        // Keep focus out of the game until the launched app activates. Deactivated
        // closes the menu as soon as that happens; commands without a window time out.
        launchTimeout = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        launchTimeout.Tick += (_, _) => { launchTimeout.Stop(); if (!closing) Close(); };
        launchTimeout.Start();
    }
}
