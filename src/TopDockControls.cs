using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Harbor;

internal sealed class TopDockControls : Window
{
    readonly Shell shell;
    readonly StackPanel body = new();
    readonly TextBlock status = Theme.Label("Changes save automatically", 11, Theme.Muted);
    readonly DispatcherTimer save = new() { Interval = TimeSpan.FromMilliseconds(250) };
    string page = "Buttons";
    bool dirty, dragging;
    internal TopDockControls(Shell shell, IntPtr monitor)
    {
        this.shell = shell; Title = "Harbor — Dock controls"; Theme.PopupWindow(this, 550, 700, monitor, false);
        var root = new DockPanel { Margin = new Thickness(19) }; var header = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var close = Theme.Button(UiIcons.Icon("close"), "Close Dock controls", Close); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); header.Children.Add(close); header.Children.Add(Theme.Label("Dock controls", 24)); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        foreach (string tab in new[] { "Buttons", "Style", "Behavior" }) tabs.Children.Add(Theme.Button(tab, tab + " settings", () => { page = tab; Render(); })); DockPanel.SetDock(tabs, System.Windows.Controls.Dock.Top); root.Children.Add(tabs);
        DockPanel.SetDock(status, System.Windows.Controls.Dock.Bottom); status.Margin = new Thickness(0, 10, 0, 0); root.Children.Add(status); root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var card = Theme.Card(root, 17); var offset = new TranslateTransform(); card.RenderTransform = offset; Content = card;
        SourceInitialized += (_, _) => { double scale = Native.Scale(monitor); var bounds = Native.Monitor(monitor).Monitor; int width = (int)(Width * scale), height = (int)(Height * scale); Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.Left + (bounds.Width - width) / 2, bounds.Top + (int)((shell.Config.TopBarHeight + 8) * scale), width, height, 0x50); };
        Loaded += (_, _) => { int ms = shell.Config.DockMotionMilliseconds; if (ms > 0 && Settings.TestDataDir == null) offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-22, 0, TimeSpan.FromMilliseconds(ms)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }); };
        save.Tick += (_, _) => Commit(); Deactivated += (_, _) => { if (!dragging) Close(); }; Closed += (_, _) => { save.Stop(); Commit(); };
        Render();
    }
    void Render()
    {
        body.Children.Clear(); var c = shell.Config;
        if (page == "Buttons")
        {
            Note("Drag buttons anywhere across the Dock, including empty space. The outline shows where they will land. Drag a grip here to reorder, or choose a group to snap back to it.");
            body.Children.Add(Theme.Button("+ Piece", "Add an independent Dock piece", () => { var editor = new DockPieceEditor(shell); editor.Closed += (_, _) => { if (IsLoaded) Render(); }; editor.Show(); }));
            var add = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) }; add.Children.Add(Theme.Button("+ Divider", "Add Dock divider", () => { BarLayout.AddDivider(c, "right"); Changed(); Render(); })); add.Children.Add(Theme.Button("+ Space", "Add flexible space", () => { BarLayout.AddDivider(c, "right", space: true); Changed(); Render(); })); add.Children.Add(Theme.Button("Reset layout", "Restore the default Dock layout", () => { c.TopDockItems = BarLayout.Defaults(c); Changed(); Render(); })); body.Children.Add(add);
            foreach (var item in c.TopDockItems.ToArray())
            {
                var row = new DockPanel { Background = Theme.Panel, Margin = new Thickness(0, 0, 0, 5), MinHeight = 38, AllowDrop = true };
                var grip = Theme.Button(UiIcons.Icon("grip", 17), "Drag " + BarLayout.Label(item), () => { }); grip.Width = 24; grip.Padding = new Thickness(2); grip.Cursor = Cursors.SizeAll; row.Children.Add(grip);
                var remove = Theme.Button(UiIcons.Icon("close", 12), "Remove divider", () => { c.TopDockItems.Remove(item); Changed(); Render(); }); remove.Padding = new Thickness(6); remove.Visibility = item.Id.StartsWith("divider.") ? Visibility.Visible : Visibility.Hidden; DockPanel.SetDock(remove, System.Windows.Controls.Dock.Right); row.Children.Add(remove);
                var group = new ComboBox { ItemsSource = new[] { "left", "center", "right" }, SelectedItem = item.Group, Width = 90, Margin = new Thickness(6, 1, 3, 1) }; group.SelectionChanged += (_, _) => { if (group.SelectedItem is string value) { BarLayout.Move(c, item.Id, null, value); Changed(); } }; AutomationProperties.SetName(group, BarLayout.Label(item) + " position"); DockPanel.SetDock(group, System.Windows.Controls.Dock.Right); row.Children.Add(group);
                var edit = Theme.Button(UiIcons.Icon("controls", 13), "Edit " + BarLayout.Label(item), () => { var editor = new DockPieceEditor(shell, item); editor.Show(); }); edit.Padding = new Thickness(6); DockPanel.SetDock(edit, System.Windows.Controls.Dock.Right); row.Children.Add(edit);
                var toggle = new CheckBox { Style = (Style)FindResource("Harbor.Switch"), Content = BarLayout.Label(item), IsChecked = item.Enabled && BarLayout.Legacy(c, BarLayout.Component(item)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 0, 0) }; toggle.Checked += (_, _) => { BarLayout.Enable(c, item, true); Changed(); }; toggle.Unchecked += (_, _) => { BarLayout.Enable(c, item, false); Changed(); }; AutomationProperties.SetName(toggle, "Show " + BarLayout.Label(item)); row.Children.Add(toggle);
                Point start = default; bool armed = false; grip.PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(grip); armed = true; };
                grip.PreviewMouseLeftButtonUp += (_, _) => armed = false;
                grip.PreviewMouseMove += (_, e) => { if (!armed || e.LeftButton != MouseButtonState.Pressed || dragging || (e.GetPosition(grip) - start).Length < 5) return; armed = false; dragging = true; grip.ReleaseMouseCapture(); try { DragDrop.DoDragDrop(grip, new DataObject(BarLayout.DragFormat, item.Id), DragDropEffects.Move); } finally { dragging = false; Dispatcher.BeginInvoke(new Action(Render)); } };
                row.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(BarLayout.DragFormat) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
                row.Drop += (_, e) => { if (e.Data.GetData(BarLayout.DragFormat) is string id) { BarLayout.Move(c, id, item.Id, item.Group); Changed(); e.Handled = true; } }; body.Children.Add(row);
            }
        }
        else if (page == "Style")
        {
            Note("Change how the Dock looks. Buttons keep their saved order.");
            Slider("Height", 28, 64, c.TopBarHeight, "px", v => c.TopBarHeight = (int)v);
            Slider("Text size", 10, 18, c.TopDockFontSize, "px", v => c.TopDockFontSize = (int)v);
            Slider("Icon size", 12, 26, c.TopDockIconSize, "px", v => c.TopDockIconSize = (int)v);
            Slider("Button padding", 3, 20, c.TopDockPadding, "px", v => c.TopDockPadding = (int)v);
            Slider("Space between buttons", 0, 18, c.TopDockGap, "px", v => c.TopDockGap = (int)v);
            Slider("Rounded corners", 0, 28, c.TopDockRadius, "px", v => c.TopDockRadius = (int)v);
            Slider("Window title width", 80, 520, c.TopDockTitleWidth, "px", v => c.TopDockTitleWidth = (int)v);
            Slider("Opacity", 50, 100, c.TopDockOpacity * 100, "%", v => c.TopDockOpacity = v / 100);
            Toggle("Show bottom border", c.TopDockShowBorder, v => c.TopDockShowBorder = v);
            Toggle("Show Harbor name", c.TopDockBrandText, v => c.TopDockBrandText = v);
            body.Children.Add(Theme.Button("Colors and themes", "Open appearance settings", shell.OpenAppearance));
        }
        else
        {
            Note("The Dock can be restored from the taskbar’s right-click menu if you hide it.");
            Toggle("Show Dock", c.TopDockEnabled, v => c.TopDockEnabled = v);
            Toggle("Only on the main display", c.TopDockPrimaryOnly, v => c.TopDockPrimaryOnly = v);
            Toggle("Keep apps below the Dock", c.TopDockReserveSpace, v => c.TopDockReserveSpace = v);
            Toggle("Stay above ordinary windows", c.TopDockOnTop, v => c.TopDockOnTop = v);
            Toggle("Hide bars while gaming", c.HideInGames, v => c.HideInGames = v);
            Toggle("Hide bars in full screen", c.HideInFullscreen, v => c.HideInFullscreen = v);
            Toggle("24-hour clock", c.Use24HourClock, v => c.Use24HourClock = v);
            Toggle("Show seconds", c.TopDockSeconds, v => c.TopDockSeconds = v);
            Slider("Preview frame rate", 15, 60, c.PreviewFramesPerSecond, "fps", v => c.PreviewFramesPerSecond = (int)v);
            Slider("Animation frame target", 30, 120, c.AnimationFramesPerSecond, "fps", v => c.AnimationFramesPerSecond = (int)v);
            Slider("Taskbar folder size", 40, 72, c.TaskbarFolderSize, "px", v => c.TaskbarFolderSize = (int)v);
            Slider("UI animation time", 0, 350, c.DockMotionMilliseconds, "ms", v => c.DockMotionMilliseconds = (int)v);
        }
    }
    void Note(string text) => body.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12, Margin = new Thickness(0, 0, 0, 12) });
    void Toggle(string label, bool initial, Action<bool> change) { var box = new CheckBox { Style = (Style)FindResource("Harbor.Switch"), Content = label, IsChecked = initial, Margin = new Thickness(0, 7, 0, 8) }; box.Checked += (_, _) => { change(true); Changed(); }; box.Unchecked += (_, _) => { change(false); Changed(); }; AutomationProperties.SetName(box, label); body.Children.Add(box); }
    void Slider(string label, double min, double max, double initial, string unit, Action<double> change) { var line = new DockPanel { Margin = new Thickness(0, 7, 0, 0) }; var value = Theme.Label(initial.ToString("0") + " " + unit, 11, Theme.Muted); DockPanel.SetDock(value, System.Windows.Controls.Dock.Right); line.Children.Add(value); line.Children.Add(Theme.Label(label, 12)); body.Children.Add(line); var slider = new Slider { Minimum = min, Maximum = max, Value = initial, TickFrequency = 1, IsSnapToTickEnabled = true }; slider.ValueChanged += (_, _) => { change(slider.Value); value.Text = slider.Value.ToString("0") + " " + unit; Changed(); }; AutomationProperties.SetName(slider, label); body.Children.Add(slider); }
    void Changed() { dirty = true; shell.ApplyTopDockPreferences(); save.Stop(); save.Start(); status.Text = "Saving…"; }
    void Commit() { save.Stop(); if (!dirty) return; try { shell.Config.Save(); dirty = false; status.Text = "Saved"; } catch (Exception ex) { status.Text = ex.Message; } }
    internal void PreparePreview(string name) { page = name; Render(); }
}
