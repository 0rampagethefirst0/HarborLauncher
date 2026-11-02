using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Harbor;

internal static class DockPieceText
{
    internal static string Render(BarItem item, Settings c, string title, double cpu, double memory, bool seconds = false, string? clockFormat = null)
    {
        DateTime now = DateTime.Now;
        if (item.TimeZone.Length > 0) try { now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, TimeZoneInfo.FindSystemTimeZoneById(item.TimeZone)).DateTime; } catch (TimeZoneNotFoundException) { } catch (InvalidTimeZoneException) { }
        string time = c.Use24HourClock ? c.TopDockSeconds || seconds ? "HH:mm:ss" : "HH:mm" : c.TopDockSeconds || seconds ? "h:mm:ss tt" : "h:mm tt";
        string kind = BarLayout.Component(item);
        if (kind is "clock" or "date" or "time") {
            string format = item.Format.Length > 0 ? item.Format : kind == "date" ? "ddd, MMM d" : kind == "time" ? time : clockFormat ?? "ddd, MMM d   " + time;
            try { return now.ToString(format); } catch (FormatException) { return now.ToString(kind == "date" ? "ddd, MMM d" : time); }
        }
        return kind switch {
            "title" => title, "cpu" => $"CPU {cpu:0}%", "memory" => $"RAM {memory:0}%", "stats" => $"CPU {cpu:0}%  RAM {memory:0}%",
            "text" => item.Text.Replace("{date}", now.ToString("ddd, MMM d")).Replace("{time}", now.ToString(time)).Replace("{cpu}", cpu.ToString("0")).Replace("{ram}", memory.ToString("0")).Replace("{title}", title), _ => ""
        };
    }
}

internal sealed class DockPieceEditor : Window
{
    internal DockPieceEditor(Shell shell, BarItem? original = null)
    {
        Title = original == null ? "Harbor — Add Dock piece" : "Harbor — Edit Dock piece"; Theme.AppWindow(this, 530, 680); MinHeight = 500; MinWidth = 430;
        var piece = original == null ? new BarItem { Id = "part." + Guid.NewGuid().ToString("N"), Kind = "text", Text = "{time}", Group = "center" } : JsonSerializer.Deserialize<BarItem>(JsonSerializer.Serialize(original))!;
        var root = new DockPanel { Margin = new Thickness(22), Background = Theme.Bg }; var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) }; DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var body = new StackPanel(); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        var fieldLabels = new Dictionary<FrameworkElement, TextBlock>();
        body.Children.Add(Theme.Label(original == null ? "Add a Dock piece" : "Edit " + BarLayout.Label(original), 24));
        body.Children.Add(new TextBlock { Text = "Each piece moves on its own. Add more clocks, app shortcuts, text, media controls, or system readings.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(0, 8, 0, 12) });
        var kind = new ComboBox { ItemsSource = BarLayout.Labels.Values.ToArray(), SelectedItem = BarLayout.Labels.GetValueOrDefault(BarLayout.Component(piece), "Custom text"), MinHeight = 34 }; Field("Piece", kind);
        if (piece.Id.StartsWith("divider.")) { kind.IsEnabled = false; kind.SelectedItem = BarLayout.Labels["text"]; }
        var name = Box("Name", piece.Name); var text = Box("Text", piece.Text); var format = Box("Date / time format", piece.Format); format.ToolTip = "For example: ddd, MMM d or h:mm tt. Leave empty to use Dock settings.";
        var zone = new ComboBox { ItemsSource = new[] { "" }.Concat(TimeZoneInfo.GetSystemTimeZones().Select(z => z.Id)).ToArray(), IsEditable = true, Text = piece.TimeZone, MinHeight = 34 }; Field("Time zone (empty means local)", zone);
        var target = Box("App or shortcut path", piece.Target);
        var plugin = new ComboBox { ItemsSource = shell.Mods.Enabled.Where(m => m.Kind == "widget").Select(m => new ComboBoxItem { Content = m.Name, Tag = m.Id }).ToArray(), MinHeight = 34 };
        plugin.SelectedItem = plugin.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == piece.Target); Field("Plugin to open", plugin);
        var choose = new ComboBox { ItemsSource = shell.Catalog.Apps.OrderBy(a => a.Name).Select(a => new ComboBoxItem { Content = a.Name, Tag = a }).ToArray(), IsTextSearchEnabled = true, MinHeight = 34 }; choose.SelectionChanged += (_, _) => { if (choose.SelectedItem is ComboBoxItem { Tag: AppEntry app }) { target.Text = app.Path; if (name.Text.Length == 0) name.Text = app.Name; kind.SelectedItem = BarLayout.Labels["app"]; } }; Field("Or choose an installed app", choose);
        var width = Box("Width in pixels (0 = automatic)", piece.Width.ToString()); var font = Box("Text size (0 = Dock setting)", piece.FontSize.ToString());
        body.Children.Add(new TextBlock { Text = "Text variables: {date}, {time}, {cpu}, {ram}, {title}. Date and Time are separate pieces, so Play can sit between them.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(0, 12, 0, 0) });
        var error = Theme.Label("", 11, Theme.Muted); body.Children.Add(error);
        kind.SelectionChanged += (_, _) => UpdateFields(); UpdateFields();
        footer.Children.Add(Theme.Button("Cancel", "Cancel editing", Close));
        footer.Children.Add(Theme.Button("Save piece", "Save Dock piece", () => {
            if (!int.TryParse(width.Text, out int w) || !int.TryParse(font.Text, out int f)) { error.Text = "Use a number for width and text size."; return; }
            string selected = SelectedKind();
            if (!piece.Id.StartsWith("divider.")) piece.Kind = selected;
            piece.Name = name.Text.Trim(); piece.Text = text.Text; piece.Format = format.Text; piece.TimeZone = zone.Text.Trim(); piece.Target = target.Text.Trim().Trim('"'); piece.Width = w; piece.FontSize = f;
            if (selected == "plugin") { if (plugin.SelectedItem is not ComboBoxItem choice) { error.Text = "Choose an enabled plugin."; return; } piece.Target = (string)choice.Tag; if (piece.Name.Length == 0) piece.Name = (string)choice.Content; }
            if (selected == "app" && piece.Target.Length == 0) { error.Text = "Choose an app or enter its path."; return; }
            if (piece.TimeZone.Length > 0) try { TimeZoneInfo.FindSystemTimeZoneById(piece.TimeZone); } catch { error.Text = "Choose a time zone from the list."; return; }
            if (piece.Format.Length > 0 && selected is "date" or "time" or "clock") try { DateTime.Now.ToString(piece.Format); } catch { error.Text = "That date / time format is invalid."; return; }
            int index = original == null ? -1 : shell.Config.TopDockItems.FindIndex(i => i.Id == original.Id);
            if (index < 0) { piece.Enabled = true; shell.Config.TopDockItems.Add(piece); } else shell.Config.TopDockItems[index] = piece;
            BarLayout.Enable(shell.Config, piece, piece.Enabled); shell.Config.Save(); shell.ApplyTopDockPreferences(); Close();
        }));
        string SelectedKind() => BarLayout.Labels.FirstOrDefault(p => p.Value == kind.SelectedItem as string).Key ?? "text";
        void Show(FrameworkElement control, bool visible) { control.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; fieldLabels[control].Visibility = control.Visibility; }
        void UpdateFields() { string selected = SelectedKind(); bool divider = piece.Id.StartsWith("divider."); Show(text, !divider && selected == "text"); Show(format, !divider && selected is "date" or "time" or "clock"); Show(zone, !divider && selected is "date" or "time" or "clock" or "text"); Show(target, !divider && selected == "app"); Show(choose, !divider && selected == "app"); Show(plugin, !divider && selected == "plugin"); Show(font, !divider); }
        void Field(string label, FrameworkElement control) { var caption = new TextBlock { Text = label, Foreground = Theme.Muted, FontSize = 11, Margin = new Thickness(0, 10, 0, 4) }; fieldLabels[control] = caption; body.Children.Add(caption); AutomationProperties.SetName(control, label); body.Children.Add(control); }
        TextBox Box(string label, string value) { var box = new TextBox { Text = value, MinHeight = 32 }; Field(label, box); return box; }
    }
}
