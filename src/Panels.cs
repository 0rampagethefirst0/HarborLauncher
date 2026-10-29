using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Harbor;

internal sealed class QuickPanel : Window
{
    internal QuickPanel(Shell shell, IntPtr monitor)
    {
        Title = "Harbor — quick settings";
        Theme.PopupWindow(this, 410, 365, monitor, false);
        var layout = new StackPanel { Margin = new Thickness(22) };
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 20) };
        var close = Theme.IconButton("\uE8BB", "Close quick settings", Close); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close);
        header.Children.Add(new TextBlock { Text = "Quick settings", FontSize = 22, FontWeight = FontWeights.SemiBold }); layout.Children.Add(header);
        var tiles = new UniformGrid { Columns = 3, Margin = new Thickness(-3, 0, -3, 22) };
        AddTile("\uE701", "Network", "ms-settings:network");
        AddTile("\uE702", "Bluetooth", "ms-settings:bluetooth");
        AddTile("\uE7F4", "Display", "ms-settings:display");
        layout.Children.Add(tiles);
        var audio = Audio.Read();
        var volumeHeader = new DockPanel();
        var mute = Theme.IconButton(audio.Muted ? "\uE74F" : "\uE767", "Mute / unmute", () => { });
        bool muted = audio.Muted;
        mute.Click += (_, _) => { muted = !muted; Audio.SetMute(muted); mute.Content = Theme.Glyph(muted ? "\uE74F" : "\uE767"); };
        volumeHeader.Children.Add(mute); volumeHeader.Children.Add(Theme.Label("System volume", 13)); layout.Children.Add(volumeHeader);
        var volumeRow = new Grid { Margin = new Thickness(5, 8, 5, 20) }; volumeRow.ColumnDefinitions.Add(new ColumnDefinition()); volumeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = audio.Volume * 100, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        AutomationProperties.SetName(slider, "System volume");
        var number = Theme.Label(((int)slider.Value).ToString(), 12, Theme.Muted); number.HorizontalAlignment = HorizontalAlignment.Right;
        slider.ValueChanged += (_, _) => { Audio.SetVolume((float)slider.Value / 100); number.Text = ((int)slider.Value).ToString(); };
        volumeRow.Children.Add(slider); Grid.SetColumn(number, 1); volumeRow.Children.Add(number); layout.Children.Add(volumeRow);
        layout.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 0, 0, 14) });
        var lower = new UniformGrid { Columns = 2 };
        lower.Children.Add(Theme.Button("Audio devices", "Open audio device settings", () => { Close(); Native.Launch("ms-settings:sound"); }));
        lower.Children.Add(Theme.Button("System tray", "Show the Windows notification tray for 12 seconds", () => { Close(); shell.Recovery.RevealTaskbar(); }));
        lower.Children.Add(Theme.Button("Windows settings", "Open Windows settings", () => { Close(); Native.Launch("ms-settings:"); }));
        lower.Children.Add(Theme.Button("Harbor settings", "Open Harbor settings", () => { Close(); shell.OpenSettings(); }));
        layout.Children.Add(lower);
        Content = Theme.Card(layout, 18);
        Theme.CloseOnDeactivate(this);
        void AddTile(string glyph, string text, string target)
        {
            var content = new StackPanel { Margin = new Thickness(0, 9, 0, 9) }; content.Children.Add(Theme.Glyph(glyph, 23));
            content.Children.Add(new TextBlock { Text = text, FontSize = 12, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
            var b = Theme.Button(content, text + " settings", () => { Close(); Native.Launch(target); }); b.Background = Theme.Raised; b.Margin = new Thickness(3); tiles.Children.Add(b);
        }
    }
}

internal sealed class CalendarPanel : Window
{
    DateTime month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    readonly TextBlock monthLabel;
    readonly UniformGrid days;
    internal CalendarPanel(IntPtr monitor)
    {
        Title = "Harbor — calendar";
        Theme.PopupWindow(this, 380, 438, monitor, false);
        var layout = new StackPanel { Margin = new Thickness(22) };
        layout.Children.Add(new TextBlock { Text = DateTime.Now.ToString("h:mm tt"), FontSize = 32, FontWeight = FontWeights.Light });
        layout.Children.Add(Theme.Label(DateTime.Now.ToString("dddd, MMMM d, yyyy"), 13, Theme.Muted));
        var nav = new Grid { Margin = new Thickness(0, 24, 0, 12) }; nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); nav.ColumnDefinitions.Add(new ColumnDefinition()); nav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        nav.Children.Add(Theme.IconButton("\uE76B", "Previous month", () => { month = month.AddMonths(-1); Populate(); }));
        monthLabel = Theme.Label("", 15); monthLabel.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetColumn(monthLabel, 1); nav.Children.Add(monthLabel);
        var next = Theme.IconButton("\uE76C", "Next month", () => { month = month.AddMonths(1); Populate(); }); Grid.SetColumn(next, 2); nav.Children.Add(next); layout.Children.Add(nav);
        var week = new UniformGrid { Columns = 7 };
        foreach (string day in new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }) { var l = Theme.Label(day, 11, Theme.Muted); l.HorizontalAlignment = HorizontalAlignment.Center; l.Margin = new Thickness(0, 0, 0, 8); week.Children.Add(l); }
        layout.Children.Add(week); days = new UniformGrid { Columns = 7, Rows = 6 }; layout.Children.Add(days);
        var today = Theme.Button("Today", "Go to today", () => { month = new(DateTime.Today.Year, DateTime.Today.Month, 1); Populate(); }); today.Margin = new Thickness(0, 10, 0, 0); layout.Children.Add(today);
        Content = Theme.Card(layout, 18); Populate();
        Theme.CloseOnDeactivate(this);
    }
    void Populate()
    {
        monthLabel.Text = month.ToString("MMMM yyyy"); days.Children.Clear();
        var first = month.AddDays(-(int)month.DayOfWeek);
        for (int i = 0; i < 42; i++)
        {
            var date = first.AddDays(i);
            var label = Theme.Label(date.Day.ToString(), 12, date.Month == month.Month ? Theme.Text : Theme.Muted); label.HorizontalAlignment = HorizontalAlignment.Center;
            var border = new Border { Height = 29, CornerRadius = new CornerRadius(7), Margin = new Thickness(2), Child = label, Background = date == DateTime.Today ? Theme.Accent : Brushes.Transparent };
            if (date == DateTime.Today) label.Foreground = Theme.Bg;
            days.Children.Add(border);
        }
    }
}

internal sealed class ArrangePanel : Window
{
    internal ArrangePanel(Shell shell, IntPtr monitor)
    {
        Title = "Harbor — arrange windows";
        Theme.PopupWindow(this, 410, 270, monitor, false);
        var layout = new StackPanel { Margin = new Thickness(22) };
        layout.Children.Add(new TextBlock { Text = "Arrange windows", FontSize = 22, FontWeight = FontWeights.SemiBold });
        var text = Theme.Label("This screen · visible app windows", 12, Theme.Muted); text.Margin = new Thickness(0, 5, 0, 20); layout.Children.Add(text);
        var choices = new UniformGrid { Columns = 3 };
        Add("\uE745", "Columns", "columns"); Add("\uE80A", "Grid", "grid"); Add("\uE8A1", "Stack", "stack");
        layout.Children.Add(choices);
        var undo = Theme.Button("Undo layout", "Restore the previous window positions", () => { Close(); shell.UndoLayout(monitor); }); undo.Margin = new Thickness(0, 15, 0, 0); layout.Children.Add(undo);
        Content = Theme.Card(layout, 18); Theme.CloseOnDeactivate(this);
        void Add(string glyph, string label, string layoutName)
        {
            var body = new StackPanel { Margin = new Thickness(0, 6, 0, 6) }; body.Children.Add(Theme.Glyph(glyph, 27));
            var l = Theme.Label(label, 12); l.HorizontalAlignment = HorizontalAlignment.Center; l.Margin = new Thickness(0, 12, 0, 0); body.Children.Add(l);
            var b = Theme.Button(body, label + " layout", () => { Close(); shell.Arrange(monitor, layoutName); }); b.Margin = new Thickness(3); b.Background = Theme.Raised; choices.Children.Add(b);
        }
    }
}

internal sealed class SettingsWindow : Window
{
    readonly Shell shell;
    internal SettingsWindow(Shell shell)
    {
        this.shell = shell;
        Title = "Harbor settings";
        var monitor = shell.CurrentMonitor();
        Theme.PopupWindow(this, 760, 724, monitor, false);
        Topmost = false; ShowInTaskbar = true;
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 22) };
        var close = Theme.IconButton("\uE8BB", "Close Harbor settings", Close); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close);
        var brand = new StackPanel { Orientation = Orientation.Horizontal }; brand.Children.Add(Theme.Logo(36));
        var title = new StackPanel { Margin = new Thickness(14, 0, 0, 0) }; title.Children.Add(new TextBlock { Text = "Harbor", FontSize = 26, FontWeight = FontWeights.SemiBold }); title.Children.Add(Theme.Label("Settings", 12, Theme.Muted)); brand.Children.Add(title); header.Children.Add(brand); root.Children.Add(header);
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock or Border or Grid) try { DragMove(); } catch { } };
        var body = new StackPanel();
        Heading("Desktop");
        Check("Replace the Windows taskbar and Start menu", shell.Config.ReplaceTaskbar, v => { shell.Config.ReplaceTaskbar = v; shell.ApplySettings(); });
        Note("Windows returns when you exit Harbor. Press Ctrl + Alt + F12 to restore it immediately.");
        Check("Use the Harbor desktop", shell.Config.ReplaceDesktop, v => { shell.Config.ReplaceDesktop = v; shell.ApplySettings(); });
        Check("Open files and folders in Harbor Files", shell.Config.UseHarborFiles, v => { shell.Config.UseHarborFiles = v; shell.ApplySettings(); });
        Check("Show Harbor on every monitor", shell.Config.AllMonitors, v => { shell.Config.AllMonitors = v; shell.ApplySettings(true); });
        Check("Hide bars during fullscreen games and videos", shell.Config.HideInFullscreen, v => { shell.Config.HideInFullscreen = v; shell.ApplySettings(); });
        Check("Hide Harbor while playing games, including windowed games", shell.Config.HideInGames, v => { shell.Config.HideInGames = v; shell.ApplySettings(); });
        Note("Hidden bars give their screen space back. Right-click a running dock app and choose Treat as game to adjust detection.");
        Check("Start Harbor when I sign in", Settings.StartupEnabled, v => Settings.StartupEnabled = v);
        Heading("Appearance");
        var appearance = Theme.Button("Customize colors, bars, and layout", "Open appearance settings", shell.OpenAppearance); appearance.Background = Theme.Raised; appearance.HorizontalAlignment = HorizontalAlignment.Left; body.Children.Add(appearance);
        Check("Show CPU and memory usage", shell.Config.ShowStats, v => { shell.Config.ShowStats = v; shell.ApplySettings(); });
        Check("Use a 24-hour clock", shell.Config.Use24HourClock, v => { shell.Config.Use24HourClock = v; shell.ApplySettings(); });
        var sizeRow = new DockPanel { Margin = new Thickness(0, 10, 0, 10) };
        var sizeLabel = Theme.Label("Dock icon size", 14); sizeLabel.Width = 150; sizeRow.Children.Add(sizeLabel);
        var sizes = new StackPanel { Orientation = Orientation.Horizontal };
        var sizeButtons = new List<Button>();
        int selectedSize = shell.Config.IconSize <= 26 ? 0 : shell.Config.IconSize <= 34 ? 1 : 2;
        for (int i = 0; i < 3; i++)
        {
            int choice = i;
            var b = Theme.Button(new[] { "Small", "Medium", "Large" }[i], "Dock icon size " + new[] { "small", "medium", "large" }[i], () =>
            {
                shell.Config.IconSize = new[] { 24, 32, 40 }[choice]; shell.ApplySettings();
                for (int n = 0; n < sizeButtons.Count; n++) sizeButtons[n].Background = n == choice ? Theme.Brush("#354762") : Theme.Raised;
            });
            b.Background = i == selectedSize ? Theme.Brush("#354762") : Theme.Raised; b.Margin = new Thickness(0, 0, 5, 0); b.MinWidth = 70; sizeButtons.Add(b); sizes.Children.Add(b);
        }
        sizeRow.Children.Add(sizes); body.Children.Add(sizeRow);
        var mods = Theme.Button("Browse mods and customize widgets", "Open Harbor mods", shell.OpenMods); mods.Background = Theme.Raised; mods.HorizontalAlignment = HorizontalAlignment.Left; mods.Margin = new Thickness(0, 10, 0, 5); body.Children.Add(mods);
        var profiles = Theme.Button("Save or switch desktop profiles", "Open desktop profiles", shell.OpenProfiles); profiles.Background = Theme.Raised; profiles.HorizontalAlignment = HorizontalAlignment.Left; profiles.Margin = new Thickness(0, 5, 0, 5); body.Children.Add(profiles);
        Heading("Windows");
        Check("Automatically arrange new windows in a grid", shell.Config.AutoTile, v => { shell.Config.AutoTile = v; shell.ApplySettings(); if (v) shell.Arrange(shell.CurrentMonitor(), "grid"); });
        Note("You can also arrange windows from the dock. Undo restores their previous positions.");
        Heading("Shortcuts");
        Note("Windows key, Ctrl + Esc, or Ctrl + Alt + Space    Open apps\nCtrl + Alt + F12    Exit Harbor and restore Windows\nRight-click an app    Pin, move, switch, or close\nDrop an app shortcut onto the dock    Pin it");
        var scroller = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroller, 1); root.Children.Add(scroller);
        var footer = new Grid { Margin = new Thickness(0, 20, 0, 0) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var status = new StackPanel(); status.Children.Add(Theme.Label("Harbor " + Program.Version + " · Software rendering", 12, Theme.Muted)); status.Children.Add(Theme.Label("Changes save automatically", 11, Theme.Muted)); footer.Children.Add(status);
        var exit = Theme.Button("Exit and restore Windows", "Exit Harbor and restore Windows", shell.Exit); exit.Background = Theme.Raised; Grid.SetColumn(exit, 1); footer.Children.Add(exit); Grid.SetRow(footer, 2); root.Children.Add(footer);
        Content = Theme.Card(root, 18);
        void Heading(string text) { var h = Theme.Label(text.ToUpperInvariant(), 10, Theme.Muted); h.FontWeight = FontWeights.SemiBold; h.Margin = new Thickness(0, body.Children.Count == 0 ? 0 : 20, 0, 4); body.Children.Add(h); }
        void Note(string text) { var l = Theme.Label(text, 12, Theme.Muted); l.TextWrapping = TextWrapping.Wrap; l.Margin = new Thickness(22, 0, 0, 2); l.LineHeight = 20; body.Children.Add(l); }
        void Check(string text, bool value, Action<bool> update)
        {
            var c = new CheckBox { Content = text, IsChecked = value };
            AutomationProperties.SetName(c, text);
            c.Checked += (_, _) => update(true); c.Unchecked += (_, _) => update(false); body.Children.Add(c);
        }
    }
}
