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

namespace Harbor;

internal sealed record FileEntry(string Path, string Name, bool Folder, long Bytes, DateTime Modified)
{
    public string Display => (Folder ? "▸  " : "    ") + Name;
    public string Size => Folder ? "Folder" : FileTools.Size(Bytes);
    public string Date => Modified.ToString("MMM d, yyyy  HH:mm");
}
internal static class FileTools
{
    internal static string Size(long bytes) => bytes < 1024 ? bytes + " B" : bytes < 1048576 ? $"{bytes / 1024.0:0.#} KB" : bytes < 1073741824 ? $"{bytes / 1048576.0:0.#} MB" : $"{bytes / 1073741824.0:0.#} GB";
    internal static string Unique(string folder, string name)
    {
        string path = System.IO.Path.Combine(folder, name); int n = 2;
        while (File.Exists(path) || Directory.Exists(path)) path = System.IO.Path.Combine(folder, System.IO.Path.GetFileNameWithoutExtension(name) + " (" + n++ + ")" + System.IO.Path.GetExtension(name));
        return path;
    }
    internal static void Copy(string source, string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked folders and files must be copied with their original application.");
        if (!Directory.Exists(source)) { File.Copy(source, destination, false); return; }
        string src = System.IO.Path.GetFullPath(source).TrimEnd('\\') + "\\", dest = System.IO.Path.GetFullPath(destination).TrimEnd('\\') + "\\";
        if (dest.StartsWith(src, StringComparison.OrdinalIgnoreCase)) throw new IOException("A folder cannot be copied into itself.");
        Directory.CreateDirectory(destination);
        foreach (string child in Directory.EnumerateFileSystemEntries(source)) Copy(child, System.IO.Path.Combine(destination, System.IO.Path.GetFileName(child)));
    }
}

internal sealed class Files : Window
{
    readonly Shell shell;
    readonly TextBox address = new(), search = new();
    readonly ListView list = new() { SelectionMode = SelectionMode.Extended };
    readonly TextBlock status = Theme.Label("", 12, Theme.Muted);
    readonly Stack<string> back = new(), forward = new();
    readonly DispatcherTimer watcherDelay;
    FileSystemWatcher? watcher;
    List<FileEntry> entries = new();
    string folder = "";
    int generation;
    bool busy, closing, cut;
    uint clipboardSequence;
    internal string CurrentFolder => folder;
    internal Files(Shell shell, string? initial = null)
    {
        this.shell = shell; Title = "Harbor — files"; Theme.AppWindow(this, 1080, 720);
        var root = new DockPanel { Background = Theme.Bg }; var header = Theme.Header(this, "Harbor Files"); DockPanel.SetDock(header, System.Windows.Controls.Dock.Top); root.Children.Add(header);
        var tools = new DockPanel { Margin = new Thickness(14, 12, 14, 8) };
        var nav = new StackPanel { Orientation = Orientation.Horizontal }; nav.Children.Add(Theme.IconButton("\uE72B", "Back", Back)); nav.Children.Add(Theme.IconButton("\uE72A", "Forward", Forward)); nav.Children.Add(Theme.IconButton("\uE74A", "Parent folder", Up)); nav.Children.Add(Theme.IconButton("\uE72C", "Refresh files", () => Load(folder, false))); tools.Children.Add(nav);
        search.Width = 225; search.Margin = new Thickness(12, 0, 0, 0); search.ToolTip = "Search this folder"; AutomationProperties.SetName(search, "Search this folder"); search.TextChanged += (_, _) => Filter(); var searchControl = Theme.Placeholder(search, "Search this folder"); DockPanel.SetDock(searchControl, System.Windows.Controls.Dock.Right); tools.Children.Add(searchControl);
        address.Margin = new Thickness(8, 0, 0, 0); AutomationProperties.SetName(address, "Folder path"); address.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Load(address.Text); e.Handled = true; } }; tools.Children.Add(address); DockPanel.SetDock(tools, System.Windows.Controls.Dock.Top); root.Children.Add(tools);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(15, 0, 15, 10) };
        Add("New folder", NewFolder); Add("Copy", () => Copy(false)); Add("Cut", () => Copy(true)); Add("Paste", Paste); Add("Rename", Rename); Add("Recycle", Recycle);
        var mods = Theme.Button("Mods", "Open Harbor mods", shell.OpenMods); mods.Margin = new Thickness(20, 0, 0, 0); actions.Children.Add(mods); DockPanel.SetDock(actions, System.Windows.Controls.Dock.Top); root.Children.Add(actions);
        var footer = new Border { Child = status, Padding = new Thickness(18, 10, 18, 12), BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0) }; DockPanel.SetDock(footer, System.Windows.Controls.Dock.Bottom); root.Children.Add(footer);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new StackPanel { Margin = new Thickness(8) }; Shortcut("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); Shortcut("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); Shortcut("Downloads", System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")); Shortcut("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); Shortcut("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)); Shortcut("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)); Shortcut("Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        sidebar.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(8, 16, 8, 12) });
        foreach (var drive in DriveInfo.GetDrives()) Shortcut(drive.Name, drive.Name);
        grid.Children.Add(new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        list.Style = Application.Current.TryFindResource(typeof(ListBox)) as Style; list.BorderThickness = new Thickness(0); list.Background = Theme.Bg; list.Foreground = Theme.Text;
        VirtualizingPanel.SetIsVirtualizing(list, true); VirtualizingPanel.SetVirtualizationMode(list, VirtualizationMode.Recycling);
        var view = new GridView(); view.Columns.Add(new GridViewColumn { Header = "Name", DisplayMemberBinding = new Binding("Display"), Width = 430 }); view.Columns.Add(new GridViewColumn { Header = "Size", DisplayMemberBinding = new Binding("Size"), Width = 110 }); view.Columns.Add(new GridViewColumn { Header = "Modified", DisplayMemberBinding = new Binding("Date"), Width = 180 }); list.View = view;
        list.MouseDoubleClick += (_, e) => { if (e.OriginalSource is FrameworkElement element && element.DataContext is FileEntry item) Open(item); };
        list.KeyDown += (_, e) => { if (e.Key == Key.Enter && list.SelectedItem is FileEntry item) { Open(item); e.Handled = true; } };
        list.SelectionChanged += (_, _) => UpdateStatus();
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Open", () => { if (list.SelectedItem is FileEntry item) Open(item); })); menu.Items.Add(Theme.Item("Copy", () => Copy(false))); menu.Items.Add(Theme.Item("Cut", () => Copy(true))); menu.Items.Add(Theme.Item("Paste", Paste)); menu.Items.Add(Theme.Item("Rename", Rename)); menu.Items.Add(Theme.Item("Move to Recycle Bin", Recycle)); list.ContextMenu = menu;
        Grid.SetColumn(list, 1); grid.Children.Add(list); root.Children.Add(grid); Content = root;
        watcherDelay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) }; watcherDelay.Tick += (_, _) => { watcherDelay.Stop(); Load(folder, false); };
        Closed += (_, _) => { closing = true; watcher?.Dispose(); watcherDelay.Stop(); };
        PreviewKeyDown += Keys;
        Load(initial ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        void Add(string text, Action action) => actions.Children.Add(Theme.Button(text, text, action));
        void Shortcut(string text, string path) { var b = Theme.Button(text, "Open " + text, () => Load(path)); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(12, 10, 12, 10); sidebar.Children.Add(b); }
    }
    internal async void Load(string target, bool history = true)
    {
        if (closing) return;
        try
        {
            target = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(target.Trim('"', ' ')));
            if (!Directory.Exists(target)) throw new DirectoryNotFoundException("This folder is not available.");
            int current = ++generation; string previous = folder;
            status.Text = "Loading…";
            var result = await Task.Run(() => new DirectoryInfo(target).EnumerateFileSystemInfos().Where(f => (f.Attributes & FileAttributes.Hidden) == 0).Take(100000).Select(f => new FileEntry(f.FullName, f.Name, f is DirectoryInfo, f is FileInfo file ? file.Length : 0, f.LastWriteTime)).OrderByDescending(f => f.Folder).ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase).ToList());
            if (current != generation || closing) return;
            if (history && previous.Length > 0 && !previous.Equals(target, StringComparison.OrdinalIgnoreCase)) { back.Push(previous); forward.Clear(); }
            folder = target; address.Text = folder; entries = result; Filter();
            watcher?.Dispose(); watcher = new FileSystemWatcher(folder) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite, EnableRaisingEvents = true };
            watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed;
        }
        catch (Exception ex) { status.Text = ex.Message; address.Text = folder; }
    }
    void Changed(object sender, FileSystemEventArgs args) { if (!closing) Dispatcher.BeginInvoke(new Action(() => { if (closing) return; watcherDelay.Stop(); watcherDelay.Start(); })); }
    void Filter() { string term = search.Text.Trim(); list.ItemsSource = entries.Where(f => f.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase)).ToList(); UpdateStatus(); }
    void UpdateStatus() { if (!busy) status.Text = $"{list.Items.Count} items" + (list.SelectedItems.Count > 0 ? $" · {list.SelectedItems.Count} selected" : ""); }
    void Open(FileEntry item) { if (item.Folder) Load(item.Path); else shell.Launch(item.Path); }
    void Back() { if (back.Count == 0) return; forward.Push(folder); Load(back.Pop(), false); }
    void Forward() { if (forward.Count == 0) return; back.Push(folder); Load(forward.Pop(), false); }
    void Up() { var parent = Directory.GetParent(folder); if (parent != null) Load(parent.FullName); }
    void Copy(bool move)
    {
        var paths = list.SelectedItems.OfType<FileEntry>().Select(f => f.Path).ToArray(); if (paths.Length == 0) return;
        try { var drop = new StringCollection(); drop.AddRange(paths); Clipboard.SetFileDropList(drop); cut = move; clipboardSequence = Native.GetClipboardSequenceNumber(); status.Text = move ? "Ready to move" : "Copied to clipboard"; } catch (Exception ex) { status.Text = ex.Message; }
    }
    void Paste()
    {
        if (!Clipboard.ContainsFileDropList()) return;
        var sources = Clipboard.GetFileDropList().Cast<string>().ToArray(); string destination = folder; bool move = cut && clipboardSequence == Native.GetClipboardSequenceNumber();
        Work(() => { foreach (string source in sources) { string path = FileTools.Unique(destination, System.IO.Path.GetFileName(source)); if (move) { if (Directory.Exists(source)) Directory.Move(source, path); else File.Move(source, path); } else FileTools.Copy(source, path); } }); cut = false;
    }
    void NewFolder()
    {
        string? name = NameDialog.Ask(this, "New folder", "New folder"); if (name == null) return; string destination = folder;
        Work(() => Directory.CreateDirectory(FileTools.Unique(destination, name)));
    }
    void Rename()
    {
        if (list.SelectedItems.Count != 1 || list.SelectedItem is not FileEntry item) return;
        string? name = NameDialog.Ask(this, "Rename", item.Name); if (name == null || name == item.Name) return;
        string path = System.IO.Path.Combine(folder, name); Work(() => { if (item.Folder) Directory.Move(item.Path, path); else File.Move(item.Path, path, false); });
    }
    void Recycle()
    {
        var selected = list.SelectedItems.OfType<FileEntry>().ToArray(); if (selected.Length == 0) return;
        if (!NameDialog.Confirm(this, "Move to Recycle Bin", $"Move {selected.Length} selected item{(selected.Length == 1 ? "" : "s")} to the Recycle Bin?")) return;
        Work(() => { foreach (var item in selected) { if (item.Folder) FileSystem.DeleteDirectory(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); else FileSystem.DeleteFile(item.Path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin); } });
    }
    async void Work(Action action)
    {
        if (busy) return; busy = true; status.Text = "Working…";
        try { await Task.Run(action); if (!closing) Load(folder, false); }
        catch (Exception ex) { status.Text = ex.Message; shell.Notifications.Add("File operation stopped", ex.Message); }
        finally { busy = false; }
    }
    void Keys(object sender, KeyEventArgs e)
    {
        bool ctrl = System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Control), alt = System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);
        if (ctrl && e.Key == Key.L) { address.Focus(); address.SelectAll(); e.Handled = true; }
        else if (ctrl && e.Key == Key.F) { search.Focus(); search.SelectAll(); e.Handled = true; }
        else if (alt && e.SystemKey == Key.Left) { Back(); e.Handled = true; }
        else if (alt && e.SystemKey == Key.Right) { Forward(); e.Handled = true; }
        else if (alt && e.SystemKey == Key.Up) { Up(); e.Handled = true; }
        else if (e.OriginalSource is not TextBox)
        {
            if (ctrl && e.Key == Key.C) { Copy(false); e.Handled = true; } else if (ctrl && e.Key == Key.X) { Copy(true); e.Handled = true; } else if (ctrl && e.Key == Key.V) { Paste(); e.Handled = true; }
            else if (e.Key == Key.F2) { Rename(); e.Handled = true; } else if (e.Key == Key.Delete) { Recycle(); e.Handled = true; }
        }
        if (e.Key == Key.F5) { Load(folder, false); e.Handled = true; }
    }
}

internal static class NameDialog
{
    internal static string? Ask(Window owner, string title, string initial)
    {
        var window = new Window { Owner = owner, Title = title, Width = 440, Height = 205, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = Theme.Panel };
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(Theme.Label(title, 22));
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 18, 0, 14) }; body.Children.Add(box);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; buttons.Children.Add(Theme.Button("Cancel", "Cancel", () => window.DialogResult = false));
        var save = Theme.Button("Save", "Save name", () => { string value = box.Text.Trim(); if (value is "." or ".." || value.Length == 0 || value.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0 || value.EndsWith('.') || value.EndsWith(' ')) return; window.DialogResult = true; }); save.IsDefault = true; buttons.Children.Add(save); body.Children.Add(buttons); window.Content = Theme.Card(body); window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return window.ShowDialog() == true ? box.Text.Trim() : null;
    }
    internal static bool Confirm(Window owner, string title, string message)
    {
        var window = new Window { Owner = owner, Title = title, Width = 450, Height = 210, WindowStartupLocation = WindowStartupLocation.CenterOwner, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = Theme.Panel };
        var body = new StackPanel { Margin = new Thickness(24) }; body.Children.Add(Theme.Label(title, 22)); body.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 14, 0, 14) }); var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right }; row.Children.Add(Theme.Button("Cancel", "Cancel", () => window.DialogResult = false)); row.Children.Add(Theme.Button("Continue", "Confirm " + title, () => window.DialogResult = true)); body.Children.Add(row); window.Content = Theme.Card(body); return window.ShowDialog() == true;
    }
}
