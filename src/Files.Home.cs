using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

internal sealed partial class Files
{
    internal const string HomePath = "harbor-home://";
    readonly ScrollViewer homeView = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Visibility = Visibility.Collapsed };
    readonly DispatcherTimer sizeSave = new() { Interval = TimeSpan.FromMilliseconds(350) };
    internal bool IsHome => CurrentFolder == HomePath;
    void InitializeHome()
    {
        ((Grid)list.Parent).Children.Add(homeView); Motion.SmoothScroll(homeView);
        sizeSave.Tick += (_, _) => { sizeSave.Stop(); if (!closing) SavePreferences(); }; Closed += (_, _) => { if (sizeSave.IsEnabled) { sizeSave.Stop(); SavePreferences(); } };
        PreviewMouseWheel += (_, e) => { if (!System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) return; SetFileZoom(shell.Config.FilesTileSize + Math.Sign(e.Delta) * 10); e.Handled = true; };
    }
    bool NavigateHome(string target, bool history)
    {
        if (target != HomePath && !target.Equals("Home", StringComparison.OrdinalIgnoreCase)) { homeView.Visibility = Visibility.Collapsed; return false; }
        generation++; watcher?.Dispose(); watcher = null; watcherDelay.Stop(); active.Visit(HomePath, history); active.Label = "Home";
        list.Visibility = empty.Visibility = appFolderView.Visibility = fileActions.Visibility = sortSelector.Visibility = sortDirection.Visibility = Visibility.Collapsed;
        homeView.Visibility = Visibility.Visible; heading.Text = "Home"; summary.Text = "Your folders, app collections, and drives";
        backButton.IsEnabled = active.Back.Count > 0; forwardButton.IsEnabled = active.Forward.Count > 0;
        RenderHome(); RenderTabs(); RenderLocation(); RenderSidebar(); SetAddressMode(false); Motion.Enter(homeView); return true;
    }
    void RenderHome()
    {
        if (!IsHome) return;
        var body = new StackPanel { Margin = new Thickness(0, 0, 4, 24) }; homeView.Content = body;
        Section("Folders you use", "Open a place, or pin a folder from View options."); var places = new WrapPanel(); body.Children.Add(places);
        string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var place in new[] { ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)), ("Downloads", Path.Combine(user, "Downloads")), ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)), ("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)), ("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)), ("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)) }) Card(places, place.Item1, place.Item2, FileIconConverter.FolderArt(place.Item1, 42), "Open folder");
        if (shell.Config.FileFavorites.Count > 0) { Section("Pinned folders", "Your saved shortcuts"); var pinned = new WrapPanel(); body.Children.Add(pinned); foreach (string path in shell.Config.FileFavorites) Card(pinned, FriendlyName(path), path, FileIconConverter.FolderArt(FriendlyName(path), 42), path); }
        if (shell.Config.AppFolders.Count > 0) { Section("App collections", "Shared with the taskbar. Rename and rearrange them in either place."); var folders = new WrapPanel(); body.Children.Add(folders); foreach (var folder in shell.Config.AppFolders) Card(folders, folder.Name, TaskbarFolders.Path(folder), FolderUi.Icon(folder, 48), folder.Apps.Count + " apps"); }
        if (shell.Config.RecentFolders.Count > 0) { Section("Recently opened", "Folders you visited in Files"); foreach (string path in shell.Config.RecentFolders.Take(5)) { var row = ActionButton("folder", FriendlyName(path) + "   ·   " + path, () => Load(path)); row.HorizontalContentAlignment = HorizontalAlignment.Left; body.Children.Add(row); } }
        Section("Drives", "Storage on this PC");
        foreach (var drive in DriveInfo.GetDrives()) try {
            if (!drive.IsReady) continue;
            var content = new StackPanel { Width = 310, Margin = new Thickness(14) }; content.Children.Add(Theme.Label(FriendlyName(drive.Name), 18));
            content.Children.Add(new ProgressBar { Minimum = 0, Maximum = 100, Value = (1 - drive.AvailableFreeSpace / (double)drive.TotalSize) * 100, Height = 7, Margin = new Thickness(0, 14, 0, 10) }); content.Children.Add(Theme.Label(FileTools.Size(drive.AvailableFreeSpace) + " free of " + FileTools.Size(drive.TotalSize), 13, Theme.Muted));
            var b = Theme.Button(content, "Open " + drive.Name, () => Load(drive.Name)); b.Background = Theme.Panel; b.HorizontalAlignment = HorizontalAlignment.Left; b.Margin = new Thickness(0, 0, 8, 8); body.Children.Add(b);
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
        previousPage.Visibility = nextPage.Visibility = Visibility.Collapsed; status.Text = "Home · Ctrl + L for a path · Ctrl + mouse wheel to resize"; _ = PreviewSelection();
        void Section(string title, string note) { var label = Theme.Label(title, 20); label.FontWeight = FontWeights.SemiBold; label.Margin = new Thickness(4, body.Children.Count == 0 ? 0 : 24, 0, 6); body.Children.Add(label); var detail = Theme.Label(note, 13, Theme.Muted); detail.Margin = new Thickness(4, 0, 0, 14); detail.TextWrapping = TextWrapping.Wrap; body.Children.Add(detail); }
        void Card(Panel parent, string name, string path, FrameworkElement icon, string note) {
            if (active.Search.Length > 0 && !name.Contains(active.Search, StringComparison.CurrentCultureIgnoreCase)) return;
            var content = new StackPanel { Width = 156, Margin = new Thickness(12, 14, 12, 14) }; icon.HorizontalAlignment = HorizontalAlignment.Left; content.Children.Add(icon);
            var title = Theme.Label(name, 15); title.FontWeight = FontWeights.SemiBold; title.TextTrimming = TextTrimming.CharacterEllipsis; title.Margin = new Thickness(0, 12, 0, 5); content.Children.Add(title);
            var subtitle = Theme.Label(note, 12, Theme.Muted); subtitle.TextTrimming = TextTrimming.CharacterEllipsis; content.Children.Add(subtitle);
            var b = Theme.Button(content, "Open " + name, () => { if (TaskbarFolders.IsFolder(path)) OpenAppFolderTab(path); else Load(path); }); b.Background = Theme.Panel; b.Margin = new Thickness(0, 0, 8, 8); parent.Children.Add(b);
        }
    }
    internal void SetFileZoom(int size)
    {
        shell.Config.FilesTileSize = Math.Clamp(size, 140, 240); shell.Config.FilesTextSize = Math.Clamp(14 + (size - 160) / 12, 12, 20); list.FontSize = shell.Config.FilesTextSize;
        ConfigureView(); Filter(); sizeSave.Stop(); sizeSave.Start();
    }
    static ScrollViewer? FindScroller(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) if (FindScroller(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }
    internal static string FriendlyName(string path)
    {
        if (path == HomePath) return "Home";
        try { if (path.TrimEnd('\\').Equals(Path.GetPathRoot(path)?.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) { var drive = new DriveInfo(path); return (drive.IsReady && drive.VolumeLabel.Length > 0 ? drive.VolumeLabel : "Local disk") + " (" + drive.Name.TrimEnd('\\') + ")"; } } catch { }
        string name = Path.GetFileName(path.TrimEnd('\\')); return name.Length > 0 ? name : path;
    }
    void FolderOverview()
    {
        preview.Children.Clear(); preview.Children.Add(Theme.Label("FOLDER DETAILS", 11, Theme.Muted));
        var icon = FileIconConverter.FolderArt(IsHome ? "Home" : active.Name, 72); icon.HorizontalAlignment = HorizontalAlignment.Left; icon.Margin = new Thickness(0, 28, 0, 18); preview.Children.Add(icon);
        var title = Theme.Label(IsHome ? "Your workspace" : FriendlyName(CurrentFolder), 24); title.TextWrapping = TextWrapping.Wrap; preview.Children.Add(title);
        preview.Children.Add(new TextBlock { Text = IsHome ? "Open a folder, an app collection, or a drive. Select a file to see its details here." : CurrentFolder, Foreground = Theme.Muted, FontSize = 13, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 20) });
        if (IsHome) { preview.Children.Add(Theme.Label(shell.Config.AppFolders.Count + " app collections", 16)); preview.Children.Add(Theme.Label(shell.Config.FileFavorites.Count + " pinned folders", 14, Theme.Muted)); return; }
        preview.Children.Add(Theme.Label(active.Entries.Count(e => e.Folder) + " folders", 18)); preview.Children.Add(Theme.Label(active.Entries.Count(e => !e.Folder) + " files", 18));
        foreach (var type in active.Entries.Where(e => !e.Folder).GroupBy(e => e.Kind).OrderByDescending(g => g.Count()).Take(5)) { var line = Theme.Label(type.Key + "   " + type.Count(), 13, Theme.Muted); line.Margin = new Thickness(0, 9, 0, 0); preview.Children.Add(line); }
        preview.Children.Add(ActionButton("pin", "Pin this folder", PinFolder)); preview.Children.Add(ActionButton("copy", "Copy folder path", () => Clipboard.SetText(CurrentFolder)));
    }
}
