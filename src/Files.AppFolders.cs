using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Harbor;

internal sealed partial class Files
{
    const string TabDragFormat = "Harbor.FilesTab";
    readonly HashSet<string> closedFolderTabs = new(StringComparer.OrdinalIgnoreCase);
    readonly DockPanel appFolderView = new() { Background = Brushes.Transparent, AllowDrop = true, Visibility = Visibility.Collapsed };
    Pin? selectedFolderApp;
    bool folderDragging, pendingFolderSync;
    internal AppFolder? CurrentAppFolder => TaskbarFolders.Find(shell.Config, CurrentFolder);
    internal int AppFolderTabCount => tabs.Count(t => TaskbarFolders.IsFolder(t.Folder));
    void InitializeAppFolders()
    {
        ((Grid)list.Parent).Children.Add(appFolderView);
        appFolderView.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(TaskbarFolders.DragFormat) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
        appFolderView.Drop += (_, e) => { if (CurrentAppFolder is { } folder) { foreach (var pin in TaskbarFolders.Shortcuts(e.Data)) TaskbarFolders.Add(shell.Config, folder, pin); shell.SaveShortcuts(); } e.Handled = true; };
        shell.ShortcutsChanged += SyncAppFolderTabs; SyncAppFolderTabs();
    }
    internal void SyncAppFolderTabs()
    {
        if (closing) return; if (folderDragging) { pendingFolderSync = true; return; }
        foreach (var tab in tabs.Where(t => TaskbarFolders.IsFolder(t.Folder)).ToArray()) {
            var folder = TaskbarFolders.Find(shell.Config, tab.Folder); if (folder == null) tabs.Remove(tab); else tab.Label = folder.Name;
        }
        foreach (var folder in shell.Config.AppFolders.OrderBy(f => { int n = shell.Config.AppFolderTabOrder.IndexOf(f.Id); return n < 0 ? int.MaxValue : n; })) {
            string path = TaskbarFolders.Path(folder);
            if (tabs.Count < 84 && !closedFolderTabs.Contains(path) && tabs.All(t => !t.Folder.Equals(path, StringComparison.OrdinalIgnoreCase))) tabs.Add(new FileTab(path) { Label = folder.Name });
        }
        if (!tabs.Contains(active)) { active = tabs.FirstOrDefault() ?? new FileTab(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); if (tabs.Count == 0) tabs.Add(active); Load(active.Folder, false); }
        else if (IsHome) RenderHome();
        else if (CurrentAppFolder != null) { RenderAppFolderPage(); heading.Text = CurrentAppFolder.Name; Title = CurrentAppFolder.Name + " — Harbor Files"; RenderLocation(); }
        RenderTabs(); RenderSidebar();
    }
    internal void OpenAppFolderTab(string path)
    {
        if (TaskbarFolders.Find(shell.Config, path) is not { } folder) return;
        closedFolderTabs.Remove(path); var tab = tabs.FirstOrDefault(t => t.Folder.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (tab == null) { tab = new FileTab(path) { Label = folder.Name }; tabs.Add(tab); }
        SelectTab(tab);
    }
    bool NavigateAppFolder(string target, bool history)
    {
        if (!TaskbarFolders.IsFolder(target)) { appFolderView.Visibility = Visibility.Collapsed; sortSelector.Visibility = sortDirection.Visibility = fileActions.Visibility = list.Visibility = Visibility.Visible; active.Label = null; return false; }
        if (TaskbarFolders.Find(shell.Config, target) is not { } folder) { status.Text = "This app folder has been removed."; return true; }
        generation++; watcher?.Dispose(); watcher = null; watcherDelay.Stop(); selectedFolderApp = null;
        var existing = tabs.FirstOrDefault(t => t != active && t.Folder.Equals(target, StringComparison.OrdinalIgnoreCase)); if (existing != null) { if (active.Folder.Length == 0) tabs.Remove(active); active = existing; }
        active.Visit(target, history); active.Label = folder.Name; syncing = true; search.Text = active.Search; syncing = false;
        heading.Text = folder.Name; Title = folder.Name + " — Harbor Files"; backButton.IsEnabled = active.Back.Count > 0; forwardButton.IsEnabled = active.Forward.Count > 0;
        RenderTabs(); RenderLocation(); SetAddressMode(false); RenderAppFolderPage(); return true;
    }
    void RenderAppFolderSidebar()
    {
        var section = Theme.Label("APP COLLECTIONS", 11, Theme.Muted); section.Margin = new Thickness(10, 20, 0, 9); sidebar.Children.Add(section);
        foreach (var folder in shell.Config.AppFolders) {
            var b = ActionButton("folder", folder.Name, () => OpenAppFolderTab(TaskbarFolders.Path(folder))); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(10, 9, 4, 9); b.ContextMenu = FolderUi.Menu(shell, folder, this); b.AllowDrop = true;
            b.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(TaskbarFolders.DragFormat) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None; e.Handled = true; };
            b.Drop += (_, e) => { foreach (var pin in TaskbarFolders.Shortcuts(e.Data)) TaskbarFolders.Add(shell.Config, folder, pin); shell.SaveShortcuts(); e.Handled = true; }; sidebar.Children.Add(b);
        }
        var add = ActionButton("plus", "New app folder", NewAppFolder); add.HorizontalContentAlignment = HorizontalAlignment.Left; sidebar.Children.Add(add);
    }
    void NewAppFolder()
    {
        string? name = NameDialog.Ask(this, "New app folder", "Folder"); if (name == null) return;
        var folder = new AppFolder { Name = TaskbarFolders.CleanName(name) }; shell.Config.AppFolders.Add(folder); shell.SaveShortcuts(); OpenAppFolderTab(TaskbarFolders.Path(folder));
    }
    void RenderAppFolderPage()
    {
        if (CurrentAppFolder is not { } folder) return;
        sortSelector.Visibility = sortDirection.Visibility = fileActions.Visibility = list.Visibility = empty.Visibility = Visibility.Collapsed; appFolderView.Visibility = Visibility.Visible; appFolderView.Children.Clear();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        actions.Children.Add(ActionButton("plus", "Add apps", () => new AppFolderPicker(shell, folder).Show())); actions.Children.Add(ActionButton("typing", "Rename folder", () => FolderUi.Rename(shell, folder, this))); DockPanel.SetDock(actions, System.Windows.Controls.Dock.Top); appFolderView.Children.Add(actions);
        var cards = new WrapPanel(); bool details = shell.Config.FilesView == "details";
        foreach (var pin in folder.Apps.Where(p => p.Name.Contains(active.Search, StringComparison.CurrentCultureIgnoreCase))) {
            var content = new StackPanel { Width = details ? 300 : shell.Config.FilesTileSize - 30, Orientation = details ? Orientation.Horizontal : Orientation.Vertical, Margin = new Thickness(8) }; content.Children.Add(Theme.AppIcon(pin.Path, pin.Name, details ? 36 : 60));
            var label = Theme.Label(pin.Name, shell.Config.FilesTextSize); label.TextTrimming = TextTrimming.CharacterEllipsis; label.MaxWidth = details ? 240 : shell.Config.FilesTileSize - 30; label.TextAlignment = details ? TextAlignment.Left : TextAlignment.Center; label.Margin = details ? new Thickness(12, 0, 0, 0) : new Thickness(0, 12, 0, 0); content.Children.Add(label);
            var button = Theme.Button(content, pin.Name + " · double-click to open · drag to move", () => { if (AppDrag.Suppressed) return; selectedFolderApp = pin; RenderFolderAppPreview(); }); button.Background = Theme.Panel; button.Margin = new Thickness(5); button.Padding = new Thickness(14); button.AllowDrop = true; button.ContextMenu = FolderUi.AppMenu(shell, folder, pin); AutomationProperties.SetAutomationId(button, "file-folder-app:" + pin.Path);
            button.MouseDoubleClick += (_, e) => { if (!AppDrag.Suppressed) shell.ActivateApp(pin); e.Handled = true; };
            AppDrag.Bind(button, () => pin, value => { folderDragging = value; if (!value) Dispatcher.BeginInvoke(new Action(() => { if (pendingFolderSync) { pendingFolderSync = false; SyncAppFolderTabs(); } else RenderAppFolderPage(); })); });
            button.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(TaskbarFolders.DragFormat) ? DragDropEffects.Move : DragDropEffects.Link; e.Handled = true; button.Background = Theme.Raised; };
            button.DragLeave += (_, _) => button.ClearValue(Control.BackgroundProperty);
            button.Drop += (_, e) => { bool after = e.GetPosition(button).X > button.ActualWidth / 2; foreach (var source in TaskbarFolders.Shortcuts(e.Data)) TaskbarFolders.Add(shell.Config, folder, source, pin.Path, after); shell.SaveShortcuts(); e.Handled = true; }; cards.Children.Add(button);
        }
        appFolderView.Children.Add(new ScrollViewer { Content = cards, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        summary.Text = folder.Apps.Count + " apps · shared with the taskbar"; status.Text = "Drag to rearrange, move to another folder, or move onto the taskbar."; previousPage.Visibility = nextPage.Visibility = Visibility.Collapsed; RenderFolderAppPreview();
    }
    void RenderFolderAppPreview()
    {
        previewGeneration++; preview.Children.Clear(); if (!shell.Config.FilesPreview) return;
        var folder = CurrentAppFolder; if (folder == null) return;
        if (selectedFolderApp != null && !folder.Apps.Any(p => p.Path == selectedFolderApp.Path)) selectedFolderApp = null;
        var icon = selectedFolderApp == null ? FolderUi.Icon(folder, 64) : Theme.AppIcon(selectedFolderApp.Path, selectedFolderApp.Name, 64); icon.Margin = new Thickness(0, 18, 0, 16); preview.Children.Add(icon); preview.Children.Add(Theme.Label(selectedFolderApp?.Name ?? folder.Name, 22));
        preview.Children.Add(new TextBlock { Text = selectedFolderApp == null ? "This folder stays in sync with your taskbar. Drag app shortcuts here, between folders, or back onto the taskbar." : "App shortcut. Moving it here does not move or uninstall the app.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(0, 14, 0, 16) });
        if (selectedFolderApp is { } app) { preview.Children.Add(ActionButton("open", "Open app", () => shell.ActivateApp(app))); preview.Children.Add(Theme.Button("Move to taskbar", "Move shortcut out of folder", () => { TaskbarFolders.ToTaskbar(shell.Config, app); shell.SaveShortcuts(); })); }
    }
    void BindFolderTab(DockPanel row, Button button, FileTab tab)
    {
        row.AllowDrop = true; Point start = default; bool armed = false;
        button.PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(row); armed = true; };
        button.PreviewMouseLeftButtonUp += (_, _) => armed = false;
        button.PreviewMouseMove += (_, e) => { if (!armed || e.LeftButton != MouseButtonState.Pressed || (e.GetPosition(row) - start).Length < 6) return; armed = false; button.ReleaseMouseCapture(); folderDragging = true; try { DragDrop.DoDragDrop(button, new DataObject(TabDragFormat, tab.Id), DragDropEffects.Move); } finally { folderDragging = false; Dispatcher.BeginInvoke(new Action(() => { if (pendingFolderSync) { pendingFolderSync = false; SyncAppFolderTabs(); } else RenderTabs(); })); } };
        row.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(TabDragFormat) || TaskbarFolders.IsFolder(tab.Folder) && (e.Data.GetDataPresent(TaskbarFolders.DragFormat) || e.Data.GetDataPresent(DataFormats.FileDrop)) ? DragDropEffects.Move : DragDropEffects.None; e.Handled = true; };
        row.Drop += (_, e) => {
            if (e.Data.GetData(TabDragFormat) is string id) { var moving = tabs.FirstOrDefault(t => t.Id == id); if (moving != null && moving != tab) { tabs.Remove(moving); int index = tabs.IndexOf(tab); if (e.GetPosition(row).X > row.ActualWidth / 2) index++; tabs.Insert(index, moving); shell.Config.AppFolderTabOrder = tabs.Where(t => TaskbarFolders.IsFolder(t.Folder)).Select(t => t.Folder[TaskbarFolders.Prefix.Length..]).ToList(); shell.Config.Save(); if (!folderDragging) RenderTabs(); } }
            else if (TaskbarFolders.Find(shell.Config, tab.Folder) is { } folder) { foreach (var pin in TaskbarFolders.Shortcuts(e.Data)) TaskbarFolders.Add(shell.Config, folder, pin); shell.SaveShortcuts(); }
            e.Handled = true;
        };
    }
}

internal sealed class AppFolderPicker : Window
{
    internal AppFolderPicker(Shell shell, AppFolder folder)
    {
        Title = "Harbor — Add apps to " + folder.Name; Theme.AppWindow(this, 520, 620);
        var root = new DockPanel { Margin = new Thickness(20) }; var search = new TextBox { MinHeight = 35, Margin = new Thickness(0, 12, 0, 12) }; AutomationProperties.SetName(search, "Search installed apps");
        var header = Theme.Label("Add apps to " + folder.Name, 23); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header); DockPanel.SetDock(search, System.Windows.Controls.Dock.Top); root.Children.Add(search);
        var apps = new ListBox { SelectionMode = SelectionMode.Extended, DisplayMemberPath = "Name" }; void Filter() => apps.ItemsSource = shell.Catalog.Apps.Where(a => a.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).OrderBy(a => a.Name).ToArray(); search.TextChanged += (_, _) => Filter(); Filter();
        var add = Theme.Button("Add selected apps", "Add selected app shortcuts to folder", () => { foreach (var app in apps.SelectedItems.OfType<AppEntry>().ToArray()) TaskbarFolders.Add(shell.Config, folder, app.ToPin()); shell.SaveShortcuts(); Close(); }); add.Margin = new Thickness(0, 12, 0, 0); DockPanel.SetDock(add, System.Windows.Controls.Dock.Bottom); root.Children.Add(add); root.Children.Add(apps); Content = root;
    }
}
