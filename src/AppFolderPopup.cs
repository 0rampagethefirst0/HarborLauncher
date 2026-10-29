using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Harbor;

internal static class AppDrag
{
    static long suppressUntil;
    internal static bool Suppressed => Environment.TickCount64 < suppressUntil;
    internal static void Bind(UIElement element, Func<Pin> pin, Action<bool>? state = null) => BindData(element, () => TaskbarFolders.Data(pin()), state);
    internal static void BindData(UIElement element, Func<DataObject> data, Action<bool>? state = null)
    {
        Point origin = default; bool armed = false;
        element.PreviewMouseLeftButtonDown += (_, e) => { origin = e.GetPosition(element); armed = true; };
        element.PreviewMouseLeftButtonUp += (_, _) => armed = false;
        element.PreviewMouseMove += (_, e) => {
            if (!armed || e.LeftButton != MouseButtonState.Pressed || (e.GetPosition(element) - origin).Length < 6) return;
            armed = false; suppressUntil = long.MaxValue; element.ReleaseMouseCapture(); state?.Invoke(true);
            try { DragDrop.DoDragDrop(element, data(), DragDropEffects.Move | DragDropEffects.Link); }
            finally { suppressUntil = Environment.TickCount64 + 300; state?.Invoke(false); }
        };
    }
}

internal static class FolderUi
{
    internal static FrameworkElement Icon(AppFolder folder, double size)
    {
        var root = new Grid { Width = size, Height = size }; var background = new Border { Background = Theme.Raised, CornerRadius = new CornerRadius(Math.Max(4, size / 5)), BorderBrush = Theme.Line, BorderThickness = new Thickness(1) }; root.Children.Add(background);
        var grid = new UniformGrid { Rows = 2, Columns = 2, Margin = new Thickness(size * .12) };
        foreach (var pin in folder.Apps.Take(4)) grid.Children.Add(Theme.AppIcon(pin.Path, pin.Name, (int)Math.Max(8, size * .32))); root.Children.Add(grid);
        if (folder.Apps.Count == 0) root.Children.Add(UiIcons.Icon("folder", size * .62)); return root;
    }
    internal static void Rename(Shell shell, AppFolder folder, Window owner)
    {
        string? name = NameDialog.Ask(owner, "Rename folder", folder.Name); if (name == null) return; TaskbarFolders.Rename(shell.Config, folder, name); shell.SaveShortcuts();
    }
    internal static ContextMenu Menu(Shell shell, AppFolder folder, Window owner)
    {
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Open in Files", () => shell.OpenFiles(TaskbarFolders.Path(folder)))); menu.Items.Add(Theme.Item("Rename folder", () => Rename(shell, folder, owner)));
        var pin = shell.Config.Pins.First(p => p.Path.Equals(TaskbarFolders.Path(folder), StringComparison.OrdinalIgnoreCase));
        menu.Items.Add(Theme.Item("Move left", () => shell.MovePin(pin, -1))); menu.Items.Add(Theme.Item("Move right", () => shell.MovePin(pin, 1)));
        menu.Items.Add(Theme.Item("Ungroup apps", () => { TaskbarFolders.Unpack(shell.Config, folder); shell.SaveShortcuts(); })); return menu;
    }
    internal static ContextMenu AppMenu(Shell shell, AppFolder folder, Pin pin)
    {
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Open", () => shell.ActivateApp(pin))); menu.Items.Add(Theme.Item("Move to taskbar", () => { TaskbarFolders.ToTaskbar(shell.Config, pin); shell.SaveShortcuts(); }));
        menu.Items.Add(Theme.Item("Move earlier", () => Move(-1))); menu.Items.Add(Theme.Item("Move later", () => Move(1)));
        foreach (var other in shell.Config.AppFolders.Where(f => f != folder)) menu.Items.Add(Theme.Item("Move to " + other.Name, () => { TaskbarFolders.Add(shell.Config, other, pin); shell.SaveShortcuts(); }));
        menu.Items.Add(Theme.Item("Add to top Dock", () => { var item = new BarItem { Id = "part." + Guid.NewGuid().ToString("N"), Kind = "app", Name = pin.Name, Target = pin.Path }; shell.Config.TopDockItems.Add(item); shell.Config.Save(); shell.ApplyTopDockPreferences(); }));
        menu.Items.Add(Theme.Item("Remove shortcut", () => { folder.Apps.Remove(pin); shell.SaveShortcuts(); })); return menu;
        void Move(int direction) { int index = folder.Apps.IndexOf(pin), target = index + direction; if (target < 0 || target >= folder.Apps.Count) return; folder.Apps.RemoveAt(index); folder.Apps.Insert(target, pin); shell.SaveShortcuts(); }
    }
}

internal sealed class AppFolderPopup : Window
{
    readonly Shell shell;
    readonly AppFolder folder;
    readonly WrapPanel apps = new();
    readonly TextBlock title;
    bool dragging, editing;
    internal AppFolderPopup(Shell shell, AppFolder folder, IntPtr monitor)
    {
        this.shell = shell; this.folder = folder; Title = "Harbor — " + folder.Name; Theme.PopupWindow(this, 530, 430, monitor, true);
        var root = new DockPanel { Margin = new Thickness(20), Background = Brushes.Transparent, AllowDrop = true }; var header = new DockPanel { Margin = new Thickness(0, 0, 0, 14) };
        var edit = Theme.Button(UiIcons.Icon("typing"), "Rename folder", () => { editing = true; try { FolderUi.Rename(shell, folder, this); } finally { editing = false; } }); DockPanel.SetDock(edit, System.Windows.Controls.Dock.Right); header.Children.Add(edit);
        var files = Theme.Button(UiIcons.Icon("folder"), "Open folder in Files", () => shell.OpenFiles(TaskbarFolders.Path(folder))); DockPanel.SetDock(files, System.Windows.Controls.Dock.Right); header.Children.Add(files); title = Theme.Label(folder.Name, 24); header.Children.Add(title); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var note = Theme.Label("Drag apps here. Drag between icons to rearrange them.", 11, Theme.Muted); note.Margin = new Thickness(0, 12, 0, 0); DockPanel.SetDock(note, System.Windows.Controls.Dock.Bottom); root.Children.Add(note); root.Children.Add(new ScrollViewer { Content = apps, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var card = Theme.Card(root, 17); Content = card;
        root.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(TaskbarFolders.DragFormat) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        root.Drop += (_, e) => { foreach (var pin in TaskbarFolders.Shortcuts(e.Data)) TaskbarFolders.Add(shell.Config, folder, pin); shell.SaveShortcuts(); e.Handled = true; };
        shell.ShortcutsChanged += Render; Closed += (_, _) => shell.ShortcutsChanged -= Render; Deactivated += (_, _) => { if (!dragging && !editing) Close(); };
        if (shell.Config.DockMotionMilliseconds > 0 && Settings.TestDataDir == null) { var slide = new TranslateTransform(); card.RenderTransform = slide; Loaded += (_, _) => slide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(shell.Config.DockMotionMilliseconds)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } }); }
        Render();
    }
    void Render()
    {
        if (dragging) return; if (!shell.Config.AppFolders.Contains(folder)) { if (IsLoaded) Close(); return; }
        title.Text = folder.Name; Title = "Harbor — " + folder.Name; apps.Children.Clear();
        foreach (var pin in folder.Apps.ToArray()) {
            var content = new StackPanel { Width = 88, Margin = new Thickness(2, 8, 2, 8) }; content.Children.Add(Theme.AppIcon(pin.Path, pin.Name, 36)); var label = Theme.Label(pin.Name, 11); label.TextAlignment = TextAlignment.Center; label.TextWrapping = TextWrapping.Wrap; label.MaxHeight = 32; label.Margin = new Thickness(0, 8, 0, 0); content.Children.Add(label);
            var button = Theme.Button(content, pin.Name, () => { if (AppDrag.Suppressed) return; shell.ActivateApp(pin); Close(); }); button.ContextMenu = FolderUi.AppMenu(shell, folder, pin); AutomationProperties.SetAutomationId(button, "folder-app:" + pin.Path); button.AllowDrop = true;
            AppDrag.Bind(button, () => pin, value => { dragging = value; if (!value) Dispatcher.BeginInvoke(new Action(Render)); });
            button.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(TaskbarFolders.DragFormat) ? DragDropEffects.Move : DragDropEffects.Link; e.Handled = true; button.Background = Theme.Raised; };
            button.DragLeave += (_, _) => button.ClearValue(BackgroundProperty);
            button.Drop += (_, e) => { bool after = e.GetPosition(button).X > button.ActualWidth / 2; foreach (var source in TaskbarFolders.Shortcuts(e.Data)) TaskbarFolders.Add(shell.Config, folder, source, pin.Path, after); shell.SaveShortcuts(); button.ClearValue(BackgroundProperty); e.Handled = true; }; apps.Children.Add(button);
        }
    }
}
