using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.VisualBasic.FileIO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;

namespace Harbor;

internal sealed partial class Files : Window
{
    readonly Shell shell;
    readonly List<FileTab> tabs = new();
    FileTab active;
    readonly ListView list = new() { SelectionMode = SelectionMode.Extended, BorderThickness = new Thickness(0), Background = Theme.Bg, Foreground = Theme.Text };
    readonly StackPanel tabStrip = new() { Orientation = Orientation.Horizontal }, breadcrumbs = new() { Orientation = Orientation.Horizontal }, sidebar = new(), preview = new();
    readonly TextBox address = new(), search = new() { Width = 240 };
    readonly TextBlock heading = Theme.Label("", 25), summary = Theme.Label("", 12, Theme.Muted), status = Theme.Label("", 12, Theme.Muted);
    readonly Border empty = new(), previewFrame;
    readonly Grid contentGrid = new();
    readonly StackPanel fileActions = new() { Orientation = Orientation.Horizontal };
    readonly ComboBox sortSelector;
    readonly Button sortDirection;
    readonly ColumnDefinition previewColumn = new() { Width = new GridLength(280) };
    readonly ProgressBar progress = new() { Height = 3, IsIndeterminate = true, Visibility = Visibility.Collapsed };
    readonly DispatcherTimer watcherDelay = new() { Interval = TimeSpan.FromMilliseconds(400) }, searchDelay = new() { Interval = TimeSpan.FromMilliseconds(150) };
    readonly Button backButton, forwardButton, pasteButton, previousPage, nextPage, stopButton;
    FileSystemWatcher? watcher;
    List<FileEntry> filtered = new();
    string sort = "Name";
    bool descending, busy, closing, cut, syncing, editAddress;
    volatile bool cancel;
    uint clipboardSequence;
    int generation, previewGeneration, page;
    Point dragOrigin;
    internal string CurrentFolder => active.Folder;
    internal int TabCount => tabs.Count;
    internal Files(Shell shell, string? initial = null)
    {
        this.shell = shell; active = new FileTab(""); tabs.Add(active);
        Title = "Harbor — Files"; Theme.AppWindow(this, 1240, 830); MinWidth = 840; MinHeight = 570;
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, "Harbor Files"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var tabRow = new DockPanel { Background = Theme.Panel, Margin = new Thickness(0, 0, 0, 1) }; var add = IconButton("plus", "New tab", () => NewTab(CurrentFolder)); DockPanel.SetDock(add, System.Windows.Controls.Dock.Right); tabRow.Children.Add(add); tabRow.Children.Add(new ScrollViewer { Content = tabStrip, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }); DockPanel.SetDock(tabRow, System.Windows.Controls.Dock.Top); root.Children.Add(tabRow);
        var nav = new DockPanel { Margin = new Thickness(14, 12, 14, 10) };
        var navigation = new StackPanel { Orientation = Orientation.Horizontal }; backButton = IconButton("left", "Back", Back); forwardButton = IconButton("right", "Forward", Forward); navigation.Children.Add(backButton); navigation.Children.Add(forwardButton); navigation.Children.Add(IconButton("up", "Parent folder", Up)); navigation.Children.Add(IconButton("refresh", "Refresh files", () => Load(CurrentFolder, false))); nav.Children.Add(navigation);
        search.ToolTip = "Search this folder"; search.Margin = new Thickness(12, 0, 0, 0); AutomationProperties.SetName(search, "Search this folder"); var searchBox = Theme.Placeholder(search, "Search this folder"); DockPanel.SetDock(searchBox, System.Windows.Controls.Dock.Right); nav.Children.Add(searchBox);
        var location = new Grid { Margin = new Thickness(10, 0, 0, 0) }; address.MinHeight = 35; address.Visibility = Visibility.Collapsed; AutomationProperties.SetName(address, "Folder path"); address.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Load(address.Text); e.Handled = true; } if (e.Key == Key.Escape) SetAddressMode(false); };
        var crumbScroll = new ScrollViewer { Content = breadcrumbs, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled }; location.Children.Add(crumbScroll); location.Children.Add(address); location.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) SetAddressMode(true); }; nav.Children.Add(location); DockPanel.SetDock(nav, System.Windows.Controls.Dock.Top); root.Children.Add(nav);
        var tools = new DockPanel { Margin = new Thickness(18, 0, 18, 12) }; var actions = fileActions;
        actions.Children.Add(ActionButton("folder", "New folder", NewFolder)); actions.Children.Add(IconButton("copy", "Copy", () => Copy(false))); actions.Children.Add(IconButton("cut", "Cut", () => Copy(true))); pasteButton = IconButton("copy", "Paste", Paste); actions.Children.Add(pasteButton); actions.Children.Add(IconButton("typing", "Rename", Rename)); actions.Children.Add(IconButton("trash", "Recycle selected items", Recycle)); tools.Children.Add(actions);
        var views = new StackPanel { Orientation = Orientation.Horizontal }; DockPanel.SetDock(views, System.Windows.Controls.Dock.Right);
        views.Children.Add(IconButton("apps", "Grid view", () => SetView("grid"))); views.Children.Add(IconButton("file", "Details view", () => SetView("details"))); views.Children.Add(IconButton("window", "Toggle preview pane", () => { shell.Config.FilesPreview = !shell.Config.FilesPreview; SavePreferences(); ShowPreviewPane(); }));
        var more = IconButton("controls", "File options", () => { }); var options = new ContextMenu(); more.ContextMenu = options; more.Click += (_, _) => { options.Items.Clear(); var hidden = Theme.Item("Show hidden files", () => { shell.Config.FilesShowHidden = !shell.Config.FilesShowHidden; SavePreferences(); Load(CurrentFolder, false); }); hidden.IsCheckable = true; hidden.IsChecked = shell.Config.FilesShowHidden; options.Items.Add(hidden); options.Items.Add(Theme.Item("Pin this folder", PinFolder)); options.Items.Add(Theme.Item("Copy folder path", () => Clipboard.SetText(CurrentFolder))); options.Items.Add(Theme.Item("Edit folder path", () => SetAddressMode(true))); options.PlacementTarget = more; options.IsOpen = true; }; views.Children.Add(more); tools.Children.Add(views); DockPanel.SetDock(tools, System.Windows.Controls.Dock.Top); root.Children.Add(tools);
        var footer = new DockPanel { Margin = new Thickness(18, 8, 18, 10) }; var pager = new StackPanel { Orientation = Orientation.Horizontal }; previousPage = Theme.Button("←", "Previous grid page", () => { page--; Filter(false); }); nextPage = Theme.Button("→", "Next grid page", () => { page++; Filter(false); }); pager.Children.Add(previousPage); pager.Children.Add(nextPage); stopButton = Theme.Button("Stop", "Stop after the current item", () => cancel = true); stopButton.Visibility = Visibility.Collapsed; pager.Children.Add(stopButton); DockPanel.SetDock(pager, System.Windows.Controls.Dock.Right); footer.Children.Add(pager); footer.Children.Add(status); DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer); DockPanel.SetDock(progress, System.Windows.Controls.Dock.Bottom); root.Children.Add(progress);
        contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(205) }); contentGrid.ColumnDefinitions.Add(new ColumnDefinition()); contentGrid.ColumnDefinitions.Add(previewColumn);
        var sidebarFrame = new Border { Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(10), Child = new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }; contentGrid.Children.Add(sidebarFrame);
        var center = new DockPanel { Margin = new Thickness(20, 2, 15, 0) }; Grid.SetColumn(center, 1);
        var intro = new DockPanel { Margin = new Thickness(0, 0, 0, 15) }; var sortBox = sortSelector = new ComboBox { ItemsSource = new[] { "Name", "Modified", "Type", "Size" }, SelectedItem = sort, Width = 125, Height = 34, VerticalAlignment = VerticalAlignment.Center }; AutomationProperties.SetName(sortBox, "Sort files by"); sortBox.SelectionChanged += (_, _) => { sort = sortBox.SelectedItem as string ?? "Name"; Filter(); }; var direction = sortDirection = IconButton("down", "Reverse sort order", () => { descending = !descending; Filter(); }); DockPanel.SetDock(direction, System.Windows.Controls.Dock.Right); intro.Children.Add(direction); DockPanel.SetDock(sortBox, System.Windows.Controls.Dock.Right); intro.Children.Add(sortBox); var words = new StackPanel(); words.Children.Add(heading); words.Children.Add(summary); intro.Children.Add(words); DockPanel.SetDock(intro, System.Windows.Controls.Dock.Top); center.Children.Add(intro);
        var listing = new Grid(); list.Style = Application.Current.TryFindResource(typeof(ListBox)) as Style; VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling); listing.Children.Add(list);
        var noFiles = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; noFiles.Children.Add(UiIcons.Icon("folder", 52)); noFiles.Children.Add(Theme.Label("Nothing here yet", 22)); noFiles.Children.Add(Theme.Label("Create a folder or paste some files to get started.", 12, Theme.Muted)); empty.Child = noFiles; empty.IsHitTestVisible = false; listing.Children.Add(empty); center.Children.Add(listing); contentGrid.Children.Add(center);
        previewFrame = new Border { Background = Theme.Panel, BorderBrush = Theme.Line, BorderThickness = new Thickness(1, 0, 0, 0), Padding = new Thickness(18), Child = new ScrollViewer { Content = preview, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }; Grid.SetColumn(previewFrame, 2); contentGrid.Children.Add(previewFrame); root.Children.Add(contentGrid); Content = root;
        list.SelectionChanged += (_, _) => { UpdateStatus(); _ = PreviewSelection(); };
        list.MouseDoubleClick += (_, e) => { var item = EntryAt(e.OriginalSource as DependencyObject); if (item != null) Open(item); };
        list.PreviewMouseRightButtonDown += (_, e) => { var entry = EntryAt(e.OriginalSource as DependencyObject); if (entry != null && !list.SelectedItems.Contains(entry)) list.SelectedItem = entry; };
        list.ContextMenu = new ContextMenu(); list.ContextMenuOpening += (_, _) => BuildMenu(list.ContextMenu);
        list.PreviewMouseLeftButtonDown += (_, e) => dragOrigin = e.GetPosition(list);
        list.MouseMove += (_, e) => { if (e.LeftButton != MouseButtonState.Pressed || busy || (e.GetPosition(list) - dragOrigin).Length < 6 || list.SelectedItems.Count == 0) return; var paths = SelectedPaths(); var data = new DataObject(DataFormats.FileDrop, paths); var shortcut = TaskbarFolders.Shortcuts(data).FirstOrDefault(); if (shortcut != null && paths.Length == 1) data.SetData(TaskbarFolders.DragFormat, System.Text.Json.JsonSerializer.Serialize(shortcut)); DragDrop.DoDragDrop(list, data, DragDropEffects.Copy | DragDropEffects.Link | DragDropEffects.Move); };
        list.AllowDrop = true; list.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) && !busy ? e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey) ? DragDropEffects.Move : DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        list.Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || busy) return; var target = EntryAt(e.OriginalSource as DependencyObject); Transfer(paths, target?.Folder == true ? target.Path : CurrentFolder, e.KeyStates.HasFlag(DragDropKeyStates.ShiftKey)); e.Handled = true; };
        search.TextChanged += (_, _) => { if (syncing) return; active.Search = search.Text; searchDelay.Stop(); searchDelay.Start(); }; searchDelay.Tick += (_, _) => { searchDelay.Stop(); Filter(); };
        watcherDelay.Tick += (_, _) => { watcherDelay.Stop(); if (!busy) Load(CurrentFolder, false); };
        Closed += (_, _) => { closing = true; generation++; previewGeneration++; watcher?.Dispose(); watcherDelay.Stop(); searchDelay.Stop(); shell.ShortcutsChanged -= SyncAppFolderTabs; };
        PreviewKeyDown += Keys; RenderSidebar(); ConfigureView(); ShowPreviewPane(); Load(initial ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); InitializeAppFolders();
    }
    static Button IconButton(string icon, string title, Action click) { var b = Theme.Button(UiIcons.Icon(icon), title, click); b.Padding = new Thickness(9); return b; }
    static Button ActionButton(string icon, string title, Action click) => Theme.Button(UiIcons.Label(title, 12, icon), title, click);
    void SavePreferences() { try { shell.Config.Save(); } catch (Exception ex) { status.Text = ex.Message; } }
    internal void NewTab(string? target = null) { if (target != null && TaskbarFolders.IsFolder(target)) target = null; if (tabs.Count(t => !TaskbarFolders.IsFolder(t.Folder)) >= 24) { status.Text = "Close a tab before adding another."; return; } active = new FileTab(""); tabs.Add(active); syncing = true; search.Clear(); syncing = false; Load(target is { Length: > 0 } ? target : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); }
    void SelectTab(FileTab tab) { active = tab; syncing = true; search.Text = tab.Search; syncing = false; page = 0; Load(tab.Folder, false); }
    void CloseTab(FileTab tab) { if (TaskbarFolders.IsFolder(tab.Folder)) closedFolderTabs.Add(tab.Folder); if (tabs.Count == 1) { Close(); return; } int index = tabs.IndexOf(tab); tabs.Remove(tab); if (active == tab) SelectTab(tabs[Math.Min(index, tabs.Count - 1)]); else RenderTabs(); }
    void RenderTabs()
    {
        tabStrip.Children.Clear(); foreach (var tab in tabs) { var row = new DockPanel { Background = tab == active ? Theme.Raised : Theme.Panel, Margin = new Thickness(4, 3, 0, 3) }; var close = IconButton("close", "Close " + tab.Name + " tab", () => CloseTab(tab)); close.Padding = new Thickness(5); DockPanel.SetDock(close, System.Windows.Controls.Dock.Right); row.Children.Add(close); var button = Theme.Button(UiIcons.Label(tab.Name.Length > 22 ? tab.Name[..22] + "…" : tab.Name, 12, "folder"), tab.Name, () => SelectTab(tab)); button.MinWidth = 135; row.Children.Add(button); BindFolderTab(row, button, tab); tabStrip.Children.Add(row); }
    }
    void RenderSidebar()
    {
        sidebar.Children.Clear(); Section("QUICK ACCESS"); Shortcut("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "profiles"); Shortcut("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "window"); Shortcut("Downloads", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), "down"); Shortcut("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "file"); Shortcut("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "image"); Shortcut("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "audio"); Shortcut("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "video");
        if (shell.Config.FileFavorites.Count > 0) { Section("PINNED FOLDERS"); foreach (string path in shell.Config.FileFavorites.ToArray()) { var b = Shortcut(Path.GetFileName(path.TrimEnd('\\')), path, "pin"); var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Unpin folder", () => { shell.Config.FileFavorites.Remove(path); SavePreferences(); RenderSidebar(); })); b.ContextMenu = menu; } }
        RenderAppFolderSidebar();
        Section("THIS PC"); foreach (var drive in DriveInfo.GetDrives()) { string title; try { title = drive.IsReady && drive.VolumeLabel.Length > 0 ? drive.VolumeLabel + " (" + drive.Name.TrimEnd('\\') + ")" : drive.Name; } catch { title = drive.Name; } Shortcut(title, drive.Name, "storage"); }
        void Section(string name) { var label = Theme.Label(name, 9, Theme.Muted); label.Margin = new Thickness(10, 20, 0, 9); sidebar.Children.Add(label); }
        Button Shortcut(string name, string path, string icon) { var b = ActionButton(icon, name, () => Load(path)); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(10, 9, 4, 9); sidebar.Children.Add(b); return b; }
    }
    void RenderLocation()
    {
        if (CurrentAppFolder is { } appFolder) { breadcrumbs.Children.Clear(); breadcrumbs.Children.Add(Theme.Button("App folders", "App folders", () => { })); breadcrumbs.Children.Add(UiIcons.Icon("right", 10)); breadcrumbs.Children.Add(Theme.Label(appFolder.Name, 12)); address.Text = CurrentFolder; return; }
        breadcrumbs.Children.Clear(); string root = Path.GetPathRoot(CurrentFolder) ?? ""; string path = root;
        if (root.Length > 0) Crumb(root, root);
        foreach (string part in CurrentFolder[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)) { path = Path.Combine(path, part); Crumb(part, path); }
        var edit = IconButton("typing", "Edit folder path", () => SetAddressMode(true)); breadcrumbs.Children.Add(edit); address.Text = CurrentFolder;
        void Crumb(string label, string target) { var b = Theme.Button(label, target, () => Load(target)); b.Padding = new Thickness(7, 8, 7, 8); breadcrumbs.Children.Add(b); var chevron = UiIcons.Icon("right", 10); chevron.Margin = new Thickness(0, 0, 3, 0); breadcrumbs.Children.Add(chevron); }
    }
    void SetAddressMode(bool edit) { editAddress = edit; address.Visibility = edit ? Visibility.Visible : Visibility.Collapsed; breadcrumbs.Visibility = edit ? Visibility.Collapsed : Visibility.Visible; if (edit) { address.Text = CurrentFolder; address.Focus(); address.SelectAll(); } }
    internal void Load(string target, bool history = true) => _ = NavigateAsync(target, history);
    internal async Task NavigateAsync(string target, bool history = true)
    {
        if (closing) return; if (NavigateAppFolder(target, history)) return; int current = ++generation; var tab = active;
        try {
            target = Path.GetFullPath(Environment.ExpandEnvironmentVariables(target.Trim('"', ' '))); if (!Directory.Exists(target)) throw new DirectoryNotFoundException("This folder is not available."); status.Text = "Opening folder…";
            bool hidden = shell.Config.FilesShowHidden; string destination = target;
            var result = await Task.Run(() => { var files = new List<FileEntry>(); foreach (var item in new DirectoryInfo(destination).EnumerateFileSystemInfos()) { if (files.Count >= 100000) break; try { if (!hidden && (item.Attributes & FileAttributes.Hidden) != 0) continue; files.Add(new FileEntry(item.FullName, item.Name, (item.Attributes & FileAttributes.Directory) != 0, item is FileInfo f ? f.Length : 0, item.LastWriteTime)); } catch (IOException) { } catch (UnauthorizedAccessException) { } } return files; });
            if (closing || current != generation || active != tab) return;
            tab.Visit(target, history); tab.Entries = result; heading.Text = tab.Name; Title = tab.Name + " — Harbor Files"; RenderTabs(); RenderLocation(); SetAddressMode(false); Filter();
            backButton.IsEnabled = tab.Back.Count > 0; forwardButton.IsEnabled = tab.Forward.Count > 0;
            watcher?.Dispose(); watcher = new FileSystemWatcher(target) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, EnableRaisingEvents = true }; watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed;
        } catch (Exception ex) { if (!closing && current == generation) { status.Text = ex.Message; address.Text = CurrentFolder; } }
    }
    void Changed(object sender, FileSystemEventArgs args) { if (!closing) Dispatcher.BeginInvoke(new Action(() => { if (!closing && !busy) { watcherDelay.Stop(); watcherDelay.Start(); } })); }
    internal void SetView(string mode) { shell.Config.FilesView = mode; SavePreferences(); ConfigureView(); Filter(); }
    void ConfigureView()
    {
        bool grid = shell.Config.FilesView == "grid"; list.View = null; list.ItemTemplate = null;
        var panel = new FrameworkElementFactory(grid ? typeof(WrapPanel) : typeof(VirtualizingStackPanel)); list.ItemsPanel = new ItemsPanelTemplate(panel); ScrollViewer.SetCanContentScroll(list, !grid);
        var itemStyle = new Style(typeof(ListViewItem)); itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Text));
        var surface = new FrameworkElementFactory(typeof(Border)); surface.Name = "Surface"; surface.SetValue(Border.CornerRadiusProperty, new CornerRadius(5)); surface.SetValue(Border.PaddingProperty, new Thickness(grid ? 10 : 7)); surface.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var presenter = new FrameworkElementFactory(grid ? typeof(ContentPresenter) : typeof(GridViewRowPresenter));
        if (grid) { presenter.SetBinding(ContentPresenter.ContentProperty, new Binding()); presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding("ItemTemplate") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListView), 1) }); }
        else { presenter.SetBinding(GridViewRowPresenter.ContentProperty, new Binding()); presenter.SetBinding(GridViewRowPresenter.ColumnsProperty, new Binding("View.Columns") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListView), 1) }); }
        surface.AppendChild(presenter); var itemTemplate = new ControlTemplate(typeof(ListViewItem)) { VisualTree = surface };
        var hover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true }; hover.Setters.Add(new Setter(Border.BackgroundProperty, Theme.Raised, "Surface")); itemTemplate.Triggers.Add(hover);
        var selected = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true }; selected.Setters.Add(new Setter(Border.BackgroundProperty, Theme.Brush("#34465F"), "Surface")); itemTemplate.Triggers.Add(selected); itemStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate)); list.ItemContainerStyle = itemStyle;
        if (grid)
        {
            var template = new DataTemplate(typeof(FileEntry)); var card = new FrameworkElementFactory(typeof(StackPanel)); card.SetValue(FrameworkElement.WidthProperty, 126.0); card.SetValue(FrameworkElement.HeightProperty, 110.0); var icon = new FrameworkElementFactory(typeof(ContentControl)); icon.SetBinding(ContentControl.ContentProperty, new Binding { Converter = new FileIconConverter(43) }); icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 10)); card.AppendChild(icon); var title = new FrameworkElementFactory(typeof(TextBlock)); title.SetBinding(TextBlock.TextProperty, new Binding("Name")); title.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap); title.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center); title.SetValue(TextBlock.FontSizeProperty, 12.0); title.SetValue(FrameworkElement.MaxHeightProperty, 36.0); title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis); card.AppendChild(title); template.VisualTree = card; list.ItemTemplate = template;
        }
        else
        {
            var view = new GridView(); var name = new DataTemplate(); var row = new FrameworkElementFactory(typeof(StackPanel)); row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal); var icon = new FrameworkElementFactory(typeof(ContentControl)); icon.SetBinding(ContentControl.ContentProperty, new Binding { Converter = new FileIconConverter(20) }); icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 9, 0)); row.AppendChild(icon); var text = new FrameworkElementFactory(typeof(TextBlock)); text.SetBinding(TextBlock.TextProperty, new Binding("Name")); text.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); row.AppendChild(text); name.VisualTree = row;
            var style = new Style(typeof(GridViewColumnHeader)); style.Setters.Add(new Setter(Control.BackgroundProperty, Theme.Panel)); style.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Muted)); style.Setters.Add(new Setter(Control.BorderBrushProperty, Theme.Line)); style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 9, 10, 9))); style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Left)); view.ColumnHeaderContainerStyle = style;
            view.Columns.Add(new GridViewColumn { Header = Header("Name"), CellTemplate = name, Width = 300 }); view.Columns.Add(new GridViewColumn { Header = Header("Modified"), DisplayMemberBinding = new Binding("Date"), Width = 154 }); view.Columns.Add(new GridViewColumn { Header = Header("Type"), DisplayMemberBinding = new Binding("Kind"), Width = 100 }); view.Columns.Add(new GridViewColumn { Header = Header("Size"), DisplayMemberBinding = new Binding("Size"), Width = 90 }); list.View = view;
            GridViewColumnHeader Header(string name) { var h = new GridViewColumnHeader { Content = name }; h.Click += (_, _) => { if (sort == name) descending = !descending; else { sort = name; descending = false; } Filter(); }; return h; }
        }
    }
    void Filter(bool reset = true)
    {
        if (CurrentAppFolder != null) { RenderAppFolderPage(); return; }
        if (reset) page = 0; var selected = list.SelectedItems.OfType<FileEntry>().Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        filtered = FileBrowserState.Query(active.Entries, active.Search, sort, descending); bool grid = shell.Config.FilesView == "grid"; int pages = Math.Max(1, (filtered.Count + 199) / 200); page = Math.Clamp(page, 0, pages - 1);
        list.ItemsSource = grid ? filtered.Skip(page * 200).Take(200).ToList() : filtered; foreach (var file in list.Items.OfType<FileEntry>()) if (selected.Contains(file.Path)) list.SelectedItems.Add(file);
        empty.Visibility = filtered.Count == 0 ? Visibility.Visible : Visibility.Collapsed; summary.Text = active.Search.Length > 0 ? filtered.Count + " matches in this folder" : filtered.Count(f => f.Folder) + " folders · " + filtered.Count(f => !f.Folder) + " files";
        previousPage.Visibility = nextPage.Visibility = grid && pages > 1 ? Visibility.Visible : Visibility.Collapsed; previousPage.IsEnabled = page > 0; nextPage.IsEnabled = page + 1 < pages; UpdateStatus();
    }
    void UpdateStatus() { if (busy) return; status.Text = filtered.Count + " items" + (shell.Config.FilesView == "grid" && filtered.Count > 200 ? $" · page {page + 1}/{(filtered.Count + 199) / 200}" : "") + (list.SelectedItems.Count > 0 ? $" · {list.SelectedItems.Count} selected" : ""); }
    void ShowPreviewPane() { previewFrame.Visibility = shell.Config.FilesPreview ? Visibility.Visible : Visibility.Collapsed; previewColumn.Width = new GridLength(shell.Config.FilesPreview ? 280 : 0); _ = PreviewSelection(); }
    async Task PreviewSelection()
    {
        if (CurrentAppFolder != null) { RenderFolderAppPreview(); return; }
        int version = ++previewGeneration; preview.Children.Clear(); if (!shell.Config.FilesPreview) return; preview.Children.Add(Theme.Label("PREVIEW", 10, Theme.Muted));
        if (list.SelectedItems.Count != 1 || list.SelectedItem is not FileEntry entry) { var text = Theme.Label(list.SelectedItems.Count > 1 ? list.SelectedItems.Count + " items selected" : "Select a file", 21); text.Margin = new Thickness(0, 30, 0, 12); preview.Children.Add(text); preview.Children.Add(new TextBlock { Text = "Images and text files can be read here without opening another app.", TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 12 }); return; }
        var icon = (FrameworkElement)new FileIconConverter(56).Convert(entry, typeof(FrameworkElement), null!, System.Globalization.CultureInfo.InvariantCulture); icon.Margin = new Thickness(0, 25, 0, 15); icon.HorizontalAlignment = HorizontalAlignment.Left; preview.Children.Add(icon);
        var title = Theme.Label(entry.Name, 21); title.TextWrapping = TextWrapping.Wrap; preview.Children.Add(title); Info(entry.Kind); Info(entry.Size); Info("Modified " + entry.Date); preview.Children.Add(ActionButton(entry.Folder ? "folder" : "open", entry.Folder ? "Open folder" : "Open file", () => Open(entry))); preview.Children.Add(Theme.Button("Copy path", "Copy full file path", () => Clipboard.SetText(entry.Path)));
        if (entry.Folder) return;
        try {
            string ext = Path.GetExtension(entry.Path).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" && entry.Bytes < 32_000_000)
            {
                var bitmap = await Task.Run(() => { using var stream = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); var b = new BitmapImage(); b.BeginInit(); b.CacheOption = BitmapCacheOption.OnLoad; b.DecodePixelWidth = 360; b.StreamSource = stream; b.EndInit(); b.Freeze(); return b; });
                if (version == previewGeneration && !closing) preview.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform, MaxHeight = 330, Margin = new Thickness(0, 18, 0, 0) });
            }
            else if (new[] { ".txt", ".md", ".json", ".csv", ".log", ".ini", ".xml", ".yaml", ".yml", ".cs", ".py", ".js", ".ts", ".css", ".html", ".luau" }.Contains(ext))
            {
                string text = await Task.Run(() => { using var stream = new FileStream(entry.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete); using var reader = new StreamReader(stream, true); var buffer = new char[32768]; int read = reader.ReadBlock(buffer, 0, buffer.Length); return new string(buffer, 0, read); });
                if (version == previewGeneration && !closing) preview.Children.Add(new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 11, BorderThickness = new Thickness(0), Padding = new Thickness(9), Margin = new Thickness(0, 18, 0, 0), MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            }
        } catch (Exception ex) { if (version == previewGeneration && !closing) Info("Preview unavailable: " + ex.Message); }
        void Info(string text) { var label = Theme.Label(text, 11, Theme.Muted); label.TextWrapping = TextWrapping.Wrap; label.Margin = new Thickness(0, 8, 0, 8); preview.Children.Add(label); }
    }
    static FileEntry? EntryAt(DependencyObject? node) { while (node != null) { if (node is FrameworkElement e && e.DataContext is FileEntry entry) return entry; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node); } return null; }
    string[] SelectedPaths() => list.SelectedItems.OfType<FileEntry>().Select(e => e.Path).ToArray();
    void Open(FileEntry entry) { if (entry.Folder) Load(entry.Path); else shell.Launch(entry.Path); }
    void Back() { if (active.Previous() is string target) Load(target, false); }
    void Forward() { if (active.Next() is string target) Load(target, false); }
    void Up() { if (CurrentAppFolder != null) { Load(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); return; } var parent = Directory.GetParent(CurrentFolder); if (parent != null) Load(parent.FullName); }
    void PinFolder() { if (CurrentAppFolder != null) { status.Text = "App folders are already in the sidebar."; return; } if (!shell.Config.FileFavorites.Contains(CurrentFolder, StringComparer.OrdinalIgnoreCase)) shell.Config.FileFavorites.Add(CurrentFolder); SavePreferences(); RenderSidebar(); }
    void BuildMenu(ContextMenu menu)
    {
        menu.Items.Clear(); var item = list.SelectedItem as FileEntry;
        if (item != null) { menu.Items.Add(Theme.Item("Open", () => Open(item))); if (item.Folder) menu.Items.Add(Theme.Item("Open in new tab", () => NewTab(item.Path))); menu.Items.Add(Theme.Item("Copy", () => Copy(false))); menu.Items.Add(Theme.Item("Cut", () => Copy(true))); menu.Items.Add(Theme.Item("Copy path", () => Clipboard.SetText(item.Path))); menu.Items.Add(Theme.Item("Rename", Rename)); menu.Items.Add(Theme.Item("Move to Recycle Bin", Recycle)); menu.Items.Add(new Separator()); }
        menu.Items.Add(Theme.Item("New folder", NewFolder)); menu.Items.Add(Theme.Item("Paste", Paste)); menu.Items.Add(Theme.Item("Refresh", () => Load(CurrentFolder, false)));
    }
    void Copy(bool move)
    {
        if (CurrentAppFolder != null) { status.Text = "Drag shortcuts to move them between folders or onto the taskbar."; return; }
        var paths = SelectedPaths(); if (paths.Length == 0) return;
        try { var data = new DataObject(); data.SetData(DataFormats.FileDrop, paths); data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes(move ? 2 : 1))); Clipboard.SetDataObject(data, true); cut = move; clipboardSequence = Native.GetClipboardSequenceNumber(); status.Text = move ? "Ready to move" : "Copied"; } catch (Exception ex) { status.Text = ex.Message; }
    }
    void Paste()
    {
        if (CurrentAppFolder is { } folder) { if (Clipboard.GetDataObject() is { } data) { foreach (var pin in TaskbarFolders.Shortcuts(data)) TaskbarFolders.Add(shell.Config, folder, pin); shell.SaveShortcuts(); } return; }
        if (busy) return; try { if (!Clipboard.ContainsFileDropList()) return; var sources = Clipboard.GetFileDropList().Cast<string>().ToArray(); bool move = cut && clipboardSequence == Native.GetClipboardSequenceNumber(); if (!move && Clipboard.GetData("Preferred DropEffect") is MemoryStream effect && effect.Length >= 4) move = BitConverter.ToInt32(effect.ToArray(), 0) == 2; Transfer(sources, CurrentFolder, move); } catch (Exception ex) { status.Text = ex.Message; }
    }
    void Transfer(string[] sources, string destination, bool move) => Work(() => { int count = 0; foreach (string source in sources) { if (cancel) break; string path = FileTools.Unique(destination, Path.GetFileName(source.TrimEnd('\\'))); FileTools.Transfer(source, path, move); count++; int done = count; Dispatcher.BeginInvoke(new Action(() => { if (!closing) status.Text = (move ? "Moved " : "Copied ") + done + " / " + sources.Length; })); } }, () => cut = false);
    void NewFolder() { if (CurrentAppFolder != null) { NewAppFolder(); return; } if (busy) return; string? name = NameDialog.Ask(this, "New folder", "New folder"); if (name != null) { string path = FileTools.Unique(CurrentFolder, name); Work(() => Directory.CreateDirectory(path)); } }
    void Rename() { if (CurrentAppFolder is { } folder) { FolderUi.Rename(shell, folder, this); return; } if (busy || list.SelectedItems.Count != 1 || list.SelectedItem is not FileEntry entry) return; string? name = NameDialog.Ask(this, "Rename", entry.Name); if (name == null || name == entry.Name) return; string target = Path.Combine(Path.GetDirectoryName(entry.Path)!, name); Work(() => FileTools.Transfer(entry.Path, target, true)); }
    void Recycle()
    {
        if (CurrentAppFolder is { } folder) { if (selectedFolderApp != null) { folder.Apps.RemoveAll(p => p.Path.Equals(selectedFolderApp.Path, StringComparison.OrdinalIgnoreCase)); selectedFolderApp = null; shell.SaveShortcuts(); } return; }
        if (busy) return; var selected = list.SelectedItems.OfType<FileEntry>().ToArray(); if (selected.Length == 0 || !NameDialog.Confirm(this, "Move to Recycle Bin", "Move " + selected.Length + " selected items to the Recycle Bin?")) return;
        Work(() => { foreach (var item in selected) { if (cancel) break; if (item.Folder) FileSystem.DeleteDirectory(Path.GetFullPath(item.Path), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); else FileSystem.DeleteFile(Path.GetFullPath(item.Path), UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); } });
    }
    async void Work(Action action, Action? completed = null)
    {
        if (busy) return; busy = true; cancel = false; progress.Visibility = Visibility.Visible; stopButton.Visibility = Visibility.Visible; status.Text = "Working…"; string? error = null;
        try { await Task.Run(action); completed?.Invoke(); if (!closing) await NavigateAsync(CurrentFolder, false); }
        catch (Exception ex) { error = ex.Message; if (!closing) shell.Notifications.Add("File operation stopped", ex.Message); }
        finally { busy = false; if (!closing) { progress.Visibility = Visibility.Collapsed; stopButton.Visibility = Visibility.Collapsed; if (error != null) status.Text = error; else if (cancel) status.Text = "Stopped after the current item."; else UpdateStatus(); } }
    }
    void Keys(object sender, KeyEventArgs e)
    {
        var modifiers = System.Windows.Input.Keyboard.Modifiers; bool ctrl = modifiers.HasFlag(ModifierKeys.Control), alt = modifiers.HasFlag(ModifierKeys.Alt);
        if (ctrl && e.Key == Key.L) { SetAddressMode(true); e.Handled = true; } else if (alt && e.SystemKey == Key.D) { SetAddressMode(true); e.Handled = true; }
        else if (ctrl && e.Key == Key.F) { search.Focus(); search.SelectAll(); e.Handled = true; } else if (ctrl && e.Key == Key.T) { NewTab(CurrentFolder); e.Handled = true; }
        else if (ctrl && e.Key == Key.W) { CloseTab(active); e.Handled = true; } else if (ctrl && e.Key == Key.Tab) { int offset = modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1; SelectTab(tabs[(tabs.IndexOf(active) + offset + tabs.Count) % tabs.Count]); e.Handled = true; }
        else if (alt && e.SystemKey == Key.Left) { Back(); e.Handled = true; } else if (alt && e.SystemKey == Key.Right) { Forward(); e.Handled = true; } else if (alt && e.SystemKey == Key.Up) { Up(); e.Handled = true; }
        else if (e.Key == Key.F5) { Load(CurrentFolder, false); e.Handled = true; }
        else if (System.Windows.Input.Keyboard.FocusedElement is not TextBoxBase) {
            if (ctrl && e.Key == Key.C) { Copy(false); e.Handled = true; } else if (ctrl && e.Key == Key.X) { Copy(true); e.Handled = true; } else if (ctrl && e.Key == Key.V) { Paste(); e.Handled = true; }
            else if (e.Key == Key.F2) { Rename(); e.Handled = true; } else if (e.Key == Key.Delete) { Recycle(); e.Handled = true; } else if (e.Key == Key.Enter && list.SelectedItem is FileEntry entry) { Open(entry); e.Handled = true; }
        }
    }
    internal void PreparePreview(string? selectedName = null) { if (selectedName != null) list.SelectedItem = list.Items.OfType<FileEntry>().FirstOrDefault(e => e.Name == selectedName); }
    internal void PrepareGridPreview() { shell.Config.FilesView = "grid"; ConfigureView(); Filter(); }
}

