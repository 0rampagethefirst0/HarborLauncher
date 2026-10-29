using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Harbor;

internal sealed class ModDesigner : Window
{
    readonly Shell shell;
    readonly ModDefinition draft;
    readonly TreeView tree = new() { Background = Theme.Bg, Foreground = Theme.Text, BorderThickness = new Thickness(0) };
    readonly StackPanel inspector = new() { Margin = new Thickness(14) };
    readonly ContentControl preview = new();
    readonly TextBox name = new(), author = new();
    readonly TextBlock status = Theme.Label("", 12, Theme.Muted);
    readonly DispatcherTimer debounce = new() { Interval = TimeSpan.FromMilliseconds(250) }, clock = new() { Interval = TimeSpan.FromSeconds(1) };
    WidgetNode selected;
    Widget? widget;
    internal ModDesigner(Shell shell, ModDefinition original)
    {
        this.shell = shell; draft = Copy(original);
        if (draft.Component != "dashboard")
        {
            draft.Format = 2; draft.Kind = "widget"; draft.Component = "dashboard";
            draft.Layout = new WidgetNode { Children = new() { new WidgetNode { Type = "component", Key = "widget1", Component = original.Component == "dashboard" || original.Kind != "widget" ? "clock" : original.Component, Text = original.Name, Values = original.Options.ToDictionary(o => o.Key, o => shell.Mods.Value(original, o.Key)) } } };
            draft.Options = Mods.CommonOptions(420); draft.Name = original.Name + " dashboard"; draft.Category = "Dashboards";
        }
        selected = draft.Layout!; Title = "Harbor — mod builder"; Theme.AppWindow(this, 1180, 800);
        if (original.Component == "dashboard") foreach (var option in draft.Options) option.Default = shell.Mods.Value(original, option.Key);
        if (!original.Id.StartsWith("local.")) draft.Id = "local.draft." + Guid.NewGuid().ToString("N")[..12];
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, "Mod Builder"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var metadata = new Grid { Margin = new Thickness(18, 12, 18, 12) }; metadata.ColumnDefinitions.Add(new ColumnDefinition()); metadata.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        name.Text = draft.Name; name.ToolTip = "Mod name"; name.Margin = new Thickness(0, 0, 12, 0); metadata.Children.Add(name); author.Text = original.Id.StartsWith("local.") ? original.Author : "Local"; author.ToolTip = "Author"; Grid.SetColumn(author, 1); metadata.Children.Add(author); DockPanel.SetDock(metadata, System.Windows.Controls.Dock.Top); root.Children.Add(metadata);
        var footer = new DockPanel { Margin = new Thickness(18, 10, 18, 16) }; var actions = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(actions, System.Windows.Controls.Dock.Right);
        actions.Children.Add(Theme.Button("Export package", "Export this widget layout", Export));
        if (original.Component == "dashboard" && original.Id.StartsWith("local.")) actions.Children.Add(Theme.Button("Save changes", "Save changes to this mod", () => Save(false)));
        var save = Theme.Button("Save as new mod", "Save this layout as a new mod", () => Save(true)); save.Background = Theme.Raised; actions.Children.Add(save); footer.Children.Add(actions); footer.Children.Add(status); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var columns = new Grid { Margin = new Thickness(18, 0, 18, 0) }; columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) }); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(310) });
        var outline = new DockPanel { Margin = new Thickness(0, 0, 12, 0) };
        var tools = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) }; var type = new ComboBox { ItemsSource = WidgetLayout.Types, SelectedItem = "text", Width = 125 }; tools.Children.Add(type); tools.Children.Add(Theme.Button("Add", "Add layout item", () => Add(type.SelectedItem as string ?? "text"))); tools.Children.Add(Theme.Button("Up", "Move item up", () => Move(-1))); tools.Children.Add(Theme.Button("Down", "Move item down", () => Move(1))); tools.Children.Add(Theme.Button("Duplicate", "Duplicate item", Duplicate)); tools.Children.Add(Theme.Button("Remove", "Remove item", Remove)); DockPanel.SetDock(tools, System.Windows.Controls.Dock.Top); outline.Children.Add(tools); outline.Children.Add(tree); columns.Children.Add(outline);
        var previewPanel = new DockPanel { Margin = new Thickness(0, 0, 12, 0) }; var caption = Theme.Label("LIVE PREVIEW · actions are disabled", 10, Theme.Muted); caption.Margin = new Thickness(10); DockPanel.SetDock(caption, System.Windows.Controls.Dock.Top); previewPanel.Children.Add(caption);
        preview.HorizontalContentAlignment = HorizontalAlignment.Center; preview.VerticalContentAlignment = VerticalAlignment.Top; preview.Margin = new Thickness(12); previewPanel.Children.Add(new ScrollViewer { Content = preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto }); Grid.SetColumn(previewPanel, 1); columns.Children.Add(Theme.Card(previewPanel)); Grid.SetColumn(columns.Children[1], 1);
        var fields = new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(fields, 2); columns.Children.Add(fields); root.Children.Add(columns); Content = root;
        tree.SelectedItemChanged += (_, _) => { if (tree.SelectedItem is TreeViewItem item && item.Tag is WidgetNode node) { selected = node; Inspect(); } };
        debounce.Tick += (_, _) => { debounce.Stop(); Preview(); }; clock.Tick += (_, _) => widget?.Tick(); clock.Start();
        name.TextChanged += (_, _) => Changed(); Closed += (_, _) => { debounce.Stop(); clock.Stop(); preview.Content = null; };
        RebuildTree(); Preview();
    }
    static ModDefinition Copy(ModDefinition original) => JsonSerializer.Deserialize<ModDefinition>(JsonSerializer.Serialize(original, Mods.Json), Mods.Json)!;
    void Changed() { debounce.Stop(); debounce.Start(); }
    void RebuildTree()
    {
        tree.Items.Clear(); tree.Items.Add(Item(draft.Layout!)); Inspect();
        TreeViewItem Item(WidgetNode node) { var item = new TreeViewItem { Header = node.Type + (node.Text.Length > 0 ? " · " + (node.Text.Length > 22 ? node.Text[..22] + "…" : node.Text) : ""), Tag = node, IsExpanded = true, Foreground = Theme.Text, Margin = new Thickness(0, 3, 0, 3) }; foreach (var child in node.Children) item.Items.Add(Item(child)); if (ReferenceEquals(node, selected)) item.IsSelected = true; return item; }
    }
    void Inspect()
    {
        inspector.Children.Clear(); inspector.Children.Add(Theme.Label(selected.Type.ToUpperInvariant(), 10, Theme.Muted));
        Text("Text or label", selected.Text, value => selected.Text = value, true);
        if (selected.Type is "row" or "column" or "wrap") Number("Spacing", selected.Gap, value => selected.Gap = value);
        if (selected.Type is "input" or "check" or "component") Text("Unique key", selected.Key, value => selected.Key = value);
        if (selected.Type == "component")
        {
            Choice("Widget", Mods.Components.Where(c => c != "dashboard").OrderBy(c => c), selected.Component, value => { selected.Component = value; Dispatcher.BeginInvoke(new Action(Inspect)); });
            var model = shell.Mods.Catalog.FirstOrDefault(m => m.Component == selected.Component && m.Id.StartsWith("harbor."));
            foreach (var option in model?.Options.Where(o => o.Key != "screen") ?? Enumerable.Empty<ModOption>())
            {
                string value = selected.Values.GetValueOrDefault(option.Key, option.Default); Text(option.Label, value, next => selected.Values[option.Key] = next, option.Type == "multiline");
            }
        }
        if (selected.Type == "button") { Choice("Action", WidgetLayout.Actions, selected.Action, value => selected.Action = value); Text("Path, text, or folder", selected.Argument, value => selected.Argument = value); }
        if (selected.Type == "progress") { Text("Value", selected.Source, value => selected.Source = value); Number("Maximum", selected.Maximum, value => selected.Maximum = value); }
        Number("Font size", selected.FontSize, value => selected.FontSize = value); Number("Width (0 = auto)", selected.Width, value => selected.Width = value); Number("Height (0 = auto)", selected.Height, value => selected.Height = value); Text("Text / progress color", selected.Color, value => selected.Color = value); Text("Background color", selected.Background, value => selected.Background = value);
        var help = new TextBlock { Text = "Live text: {time:HH:mm}, {date:ddd}, {cpu:0}%, {ram:0}%, {volume:0}%, {windows}, {notifications}, {disk.C.free:0.0} GB, {option.name}, {field.key}.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 11, Margin = new Thickness(0, 16, 0, 8) }; inspector.Children.Add(help);
        void Text(string title, string value, Action<string> set, bool multiline = false) { var label = Theme.Label(title, 12); label.Margin = new Thickness(0, 12, 0, 5); inspector.Children.Add(label); var box = new TextBox { Text = value, FontSize = 12, AcceptsReturn = multiline, TextWrapping = TextWrapping.Wrap, MinHeight = multiline ? 68 : 32, MaxLength = 10000, Padding = new Thickness(8, 6, 8, 6) }; box.TextChanged += (_, _) => { set(box.Text); Changed(); }; inspector.Children.Add(box); }
        void Number(string title, double value, Action<double> set) => Text(title, value.ToString(CultureInfo.InvariantCulture), text => { if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)) set(number); });
        void Choice(string title, IEnumerable<string> values, string initial, Action<string> set) { var label = Theme.Label(title, 12); label.Margin = new Thickness(0, 12, 0, 5); inspector.Children.Add(label); var box = new ComboBox { ItemsSource = values.ToList(), SelectedItem = initial }; box.SelectionChanged += (_, _) => { if (box.SelectedItem is string value) { set(value); Changed(); } }; inspector.Children.Add(box); }
    }
    void Preview()
    {
        try { draft.Name = string.IsNullOrWhiteSpace(name.Text) ? "Preview" : name.Text; Mods.Validate(draft); widget = new Widget(shell, draft, true); preview.Content = widget.View; status.Text = "Preview updated"; }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    WidgetNode? FindParent(WidgetNode child, WidgetNode? current = null) { current ??= draft.Layout!; foreach (var node in current.Children) { if (ReferenceEquals(node, child)) return current; var found = FindParent(child, node); if (found != null) return found; } return null; }
    void Add(string type)
    {
        var parent = selected.Type is "column" or "row" or "wrap" ? selected : FindParent(selected) ?? draft.Layout!;
        var node = new WidgetNode { Type = type, Key = "item" + Guid.NewGuid().ToString("N")[..8], Text = type == "text" ? "{time:HH:mm}" : type == "button" ? "Open apps" : type == "check" ? "New task" : type == "component" ? "Widget" : "" };
        parent.Children.Add(node); selected = node; RebuildTree(); Changed();
    }
    void Move(int direction) { var parent = FindParent(selected); if (parent == null) return; int index = parent.Children.IndexOf(selected), next = index + direction; if (next < 0 || next >= parent.Children.Count) return; parent.Children.RemoveAt(index); parent.Children.Insert(next, selected); RebuildTree(); Changed(); }
    void Remove() { var parent = FindParent(selected); if (parent == null) return; parent.Children.Remove(selected); selected = parent; RebuildTree(); Changed(); }
    void Duplicate()
    {
        var parent = FindParent(selected); if (parent == null) return;
        var node = JsonSerializer.Deserialize<WidgetNode>(JsonSerializer.Serialize(selected, Mods.Json), Mods.Json)!; Rename(node); parent.Children.Insert(parent.Children.IndexOf(selected) + 1, node); selected = node; RebuildTree(); Changed();
        static void Rename(WidgetNode item) { if (item.Key.Length > 0) item.Key = "item" + Guid.NewGuid().ToString("N")[..8]; foreach (var child in item.Children) Rename(child); }
    }
    void Save(bool create)
    {
        try { draft.Name = name.Text.Trim(); draft.Author = author.Text.Trim(); Mods.Validate(draft); if (create) { var result = shell.Mods.Create(draft, draft.Name, draft.Author); status.Text = "Saved " + result.Name + ". Enable it in Mods."; } else { shell.Mods.UpdateDefinition(Copy(draft)); status.Text = "Changes saved."; } }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    void Export()
    {
        try { draft.Name = name.Text.Trim(); draft.Author = author.Text.Trim(); Mods.Validate(draft); var dialog = new SaveFileDialog { Filter = "Harbor mod|*.harbormod", FileName = "custom-widget.harbormod" }; if (dialog.ShowDialog(this) == true) { Mods.Export(draft, dialog.FileName); status.Text = "Package exported."; } } catch (Exception ex) { status.Text = ex.Message; }
    }
}
