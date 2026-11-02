using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Harbor;

internal sealed partial class Files
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct OpenWithInfo { [MarshalAs(UnmanagedType.LPWStr)] public string File; public IntPtr Class; public uint Flags; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHOpenWithDialog(IntPtr owner, ref OpenWithInfo info);
    [DllImport("mpr.dll")] static extern uint WNetConnectionDialog(IntPtr owner, uint type);
    [DllImport("mpr.dll")] static extern uint WNetDisconnectDialog(IntPtr owner, uint type);
    readonly MenuItem searchChildren = new() { Header = "Search subfolders", IsCheckable = true };
    CancellationTokenSource? recursiveSearch;
    System.Collections.Generic.List<FileEntry>? treeSearchResults;
    IntPtr OwnerHandle => new WindowInteropHelper(this).Handle;
    void InitializeActions()
    {
        var more = ActionButton("more", "More", () => { }); var menu = new ContextMenu(); more.ContextMenu = menu;
        more.Click += (_, _) => { BuildActionsMenu(menu); menu.PlacementTarget = more; menu.IsOpen = true; }; fileActions.Children.Add(more);
        var searchMenu = new ContextMenu(); searchMenu.Items.Add(searchChildren); search.ContextMenu = searchMenu; searchChildren.IsChecked = shell.Config.FilesSearchSubfolders; searchChildren.Click += (_, _) => { shell.Config.FilesSearchSubfolders = searchChildren.IsChecked; SavePreferences(); SearchFolder(); };
        PreviewMouseDown += (_, e) => { if (e.ChangedButton == MouseButton.XButton1) { Back(); e.Handled = true; } else if (e.ChangedButton == MouseButton.XButton2) { Forward(); e.Handled = true; } };
        Closed += (_, _) => recursiveSearch?.Cancel();
        list.TextSearchEnabled();
    }
    void BuildActionsMenu(ContextMenu menu)
    {
        menu.Items.Clear(); Add("New text file", NewTextFile); Add("Create shortcut", CreateShortcut); menu.Items.Add(new Separator());
        Add("Undo", () => ShellCommand("undo")); Add("Redo", () => ShellCommand("redo")); Add("Copy to…", () => TransferTo(false)); Add("Move to…", () => TransferTo(true));
        Add("Compress to ZIP", CompressSelected); Add("Extract ZIP", ExtractSelected); menu.Items.Add(new Separator());
        var grouping = new MenuItem { Header = "Group by" }; foreach (string mode in new[] { "None", "Type", "Date" }) { var option = Theme.Item(mode, () => { shell.Config.FilesGroupBy = mode; SavePreferences(); Filter(false); }); option.IsCheckable = true; option.IsChecked = shell.Config.FilesGroupBy == mode; grouping.Items.Add(option); } menu.Items.Add(grouping);
        var columns = new MenuItem { Header = "Details columns" }; foreach (string column in new[] { "Modified", "Type", "Size", "Created", "Extension", "Attributes" }) { var option = Theme.Item(column, () => { if (!shell.Config.FilesDetailColumns.Remove(column)) shell.Config.FilesDetailColumns.Add(column); SavePreferences(); ConfigureView(); Filter(false); }); option.IsCheckable = true; option.IsChecked = shell.Config.FilesDetailColumns.Contains(column); columns.Items.Add(option); } menu.Items.Add(columns);
        Add("Select all", () => list.SelectAll()); Add("Invert selection", () => { foreach (var item in list.Items.Cast<object>().ToArray()) if (list.SelectedItems.Contains(item)) list.SelectedItems.Remove(item); else list.SelectedItems.Add(item); });
        var extensions = Theme.Item("Show file extensions", () => { shell.Config.FilesShowExtensions = !shell.Config.FilesShowExtensions; SavePreferences(); ConfigureView(); Filter(false); }); extensions.IsCheckable = true; extensions.IsChecked = shell.Config.FilesShowExtensions; menu.Items.Add(extensions);
        var subfolders = Theme.Item("Search subfolders", () => { shell.Config.FilesSearchSubfolders = !shell.Config.FilesSearchSubfolders; searchChildren.IsChecked = shell.Config.FilesSearchSubfolders; SavePreferences(); SearchFolder(); }); subfolders.IsCheckable = true; subfolders.IsChecked = shell.Config.FilesSearchSubfolders; menu.Items.Add(subfolders);
        menu.Items.Add(new Separator()); Add("Properties", Properties); Add("Full file menu…", FullFileMenu); Add("Map network drive…", () => { WNetConnectionDialog(OwnerHandle, 1); RenderSidebar(); }); Add("Disconnect network drive…", () => { WNetDisconnectDialog(OwnerHandle, 1); RenderSidebar(); });
        void Add(string name, Action action) => menu.Items.Add(Theme.Item(name, action));
    }
    void ApplyGrouping()
    {
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(list.ItemsSource); if (view == null) return;
        string? group = shell.Config.FilesGroupBy == "Type" ? "Kind" : shell.Config.FilesGroupBy == "Date" ? "DateGroup" : null;
        if (group == null && view.GroupDescriptions.Count == 0 && list.GroupStyle.Count == 0 || group != null && view.GroupDescriptions.Count == 1 && view.GroupDescriptions[0] is System.Windows.Data.PropertyGroupDescription current && current.PropertyName == group && list.GroupStyle.Count == 1) return;
        view.GroupDescriptions.Clear(); list.GroupStyle.Clear();
        if (shell.Config.FilesGroupBy is not "Type" and not "Date") return;
        view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(shell.Config.FilesGroupBy == "Type" ? "Kind" : "DateGroup"));
        var header = new FrameworkElementFactory(typeof(TextBlock)); header.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name")); header.SetValue(TextBlock.FontSizeProperty, 17d); header.SetValue(TextBlock.ForegroundProperty, Theme.Accent); header.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 20, 8, 8)); list.GroupStyle.Add(new GroupStyle { HeaderTemplate = new DataTemplate { VisualTree = header } }); VirtualizingPanel.SetIsVirtualizingWhenGrouping(list, true);
    }
    void FullFileMenu() { if (IsHome || IsRecycle || CurrentAppFolder != null) return; try { ShellFileMenu.Show(this, SelectedPaths(), CurrentFolder); } catch (Exception ex) { status.Text = ex.Message; } }
    void ShellCommand(string verb) { if (IsHome || IsRecycle || CurrentAppFolder != null) return; try { if (!ShellFileMenu.InvokeVerb(this, CurrentFolder, verb)) status.Text = "Nothing available to " + verb + "."; else Load(CurrentFolder, false); } catch (Exception ex) { status.Text = ex.Message; } }
    void Properties() { if (list.SelectedItem is not FileEntry item) { if (!IsHome && !IsRecycle && CurrentAppFolder == null) ShellProperties.Show(CurrentFolder, OwnerHandle); return; } ShellProperties.Show(item.Path, OwnerHandle); }
    void OpenWith() { if (list.SelectedItem is not FileEntry { Folder: false } item) return; var info = new OpenWithInfo { File = item.Path, Flags = 1 | 4 }; SHOpenWithDialog(OwnerHandle, ref info); }
    void NewTextFile()
    {
        if (IsHome || IsRecycle || CurrentAppFolder != null || busy) return; string? name = NameDialog.Ask(this, "New text file", "New document.txt"); if (name == null) return;
        try { ShellFileOps.ValidateName(name); string folder = CurrentFolder; IntPtr owner = OwnerHandle; NativeWork(() => ShellFileOps.NewTextAsync(folder, name, owner)); } catch (Exception ex) { status.Text = ex.Message; }
    }
    async void CreateShortcut()
    {
        if (IsHome || IsRecycle || CurrentAppFolder != null) return; string? target = list.SelectedItem is FileEntry e ? e.Path : NameDialog.Ask(this, "Shortcut destination", ""); if (string.IsNullOrWhiteSpace(target)) return;
        string? name = NameDialog.Ask(this, "Shortcut name", Path.GetFileNameWithoutExtension(target) + " - Shortcut"); if (name == null) return;
        try { ShellFileOps.ValidateName(name); string path = FileTools.Unique(CurrentFolder, name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? name : name + ".lnk"); await RecycleBin.Sta(() => { dynamic host = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!; dynamic link = host.CreateShortcut(path); try { link.TargetPath = target; link.WorkingDirectory = Directory.Exists(target) ? target : Path.GetDirectoryName(target); link.Save(); } finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(host); } return true; }); await NavigateAsync(CurrentFolder, false); } catch (Exception ex) { status.Text = ex.Message; }
    }
    void TransferTo(bool move) { if (SelectedPaths().Length == 0 || busy) return; using var dialog = new Forms.FolderBrowserDialog { Description = move ? "Move selected items to" : "Copy selected items to", UseDescriptionForTitle = true }; if (dialog.ShowDialog() == Forms.DialogResult.OK) Transfer(SelectedPaths(), dialog.SelectedPath, move); }
    void CompressSelected() { string[] paths = SelectedPaths(); if (paths.Length == 0 || busy) return; string? name = NameDialog.Ask(this, "ZIP archive", Path.GetFileNameWithoutExtension(paths[0]) + ".zip"); if (name == null) return; try { ShellFileOps.ValidateName(name); string path = Path.Combine(CurrentFolder, name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name : name + ".zip"); Work(() => FileArchives.Compress(paths, path, () => cancel)); } catch (Exception ex) { status.Text = ex.Message; } }
    void ExtractSelected() { if (list.SelectedItem is not FileEntry entry || !entry.Path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || busy) return; string? name = NameDialog.Ask(this, "Extract ZIP to new folder", Path.GetFileNameWithoutExtension(entry.Name)); if (name == null) return; try { ShellFileOps.ValidateName(name); string path = Path.Combine(CurrentFolder, name); Work(() => FileArchives.Extract(entry.Path, path, () => cancel)); } catch (Exception ex) { status.Text = ex.Message; } }
    async void NativeWork(Func<Task> operation)
    {
        if (busy) return; busy = true; progress.Visibility = Visibility.Visible; stopButton.Visibility = Visibility.Collapsed; status.Text = "Working — use the transfer dialog to pause or cancel.";
        try { await operation(); cut = false; if (!closing) await NavigateAsync(CurrentFolder, false); }
        catch (OperationCanceledException) { if (!closing) status.Text = "Cancelled."; }
        catch (Exception ex) { if (!closing) status.Text = ex.Message; }
        finally { busy = false; if (!closing) progress.Visibility = Visibility.Collapsed; }
    }
    async void SearchFolder()
    {
        recursiveSearch?.Cancel(); treeSearchResults = null; if (IsRecycle) { await ReadRecycle(true); return; }
        if (!shell.Config.FilesSearchSubfolders || string.IsNullOrWhiteSpace(active.Search) || IsHome || CurrentAppFolder != null) { Filter(); return; }
        var token = recursiveSearch = new CancellationTokenSource(); string root = CurrentFolder, term = active.Search; var tab = active; status.Text = "Searching subfolders…";
        try
        {
            var found = await Task.Run(() => FileSearch.Find(root, term, shell.Config.FilesShowHidden, token.Token));
            if (token.IsCancellationRequested || closing || active != tab || CurrentFolder != root) return; treeSearchResults = found; Filter(); summary.Text = found.Count + " matches including subfolders";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!closing) status.Text = ex.Message; }
    }
}

internal static class FileSearch
{
    internal static System.Collections.Generic.List<FileEntry> Find(string root, string term, bool hidden, CancellationToken token)
    {
        var result = new System.Collections.Generic.List<FileEntry>(); var pending = new System.Collections.Generic.Stack<string>(); pending.Push(root);
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested(); string folder = pending.Pop();
            try { foreach (var item in new DirectoryInfo(folder).EnumerateFileSystemInfos()) { token.ThrowIfCancellationRequested(); try { if (!hidden && (item.Attributes & FileAttributes.Hidden) != 0) continue; bool directory = (item.Attributes & FileAttributes.Directory) != 0; if (FileBrowserState.Matches(item.Name, term)) result.Add(new FileEntry(item.FullName, item.Name, directory, item is FileInfo f ? f.Length : 0, item.LastWriteTime, item.CreationTime, item.Attributes)); if (directory && (item.Attributes & FileAttributes.ReparsePoint) == 0) pending.Push(item.FullName); } catch (IOException) { } catch (UnauthorizedAccessException) { } } } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        return result;
    }
}
internal static class FileListSearch { internal static void TextSearchEnabled(this ListView list) { System.Windows.Controls.TextSearch.SetTextPath(list, "Name"); list.IsTextSearchEnabled = true; } }
internal static class ShellProperties
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Info { public int Size; public uint Mask; public IntPtr Owner; [MarshalAs(UnmanagedType.LPWStr)] public string Verb; [MarshalAs(UnmanagedType.LPWStr)] public string File; public IntPtr Parameters, Directory; public int Show; public IntPtr Instance, Item, Class, ClassKey; public uint HotKey; public IntPtr Icon, Process; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool ShellExecuteEx(ref Info info);
    internal static void Show(string path, IntPtr owner) { var info = new Info { Size = Marshal.SizeOf<Info>(), Mask = 0xC, Owner = owner, Verb = "properties", File = path, Show = 1 }; ShellExecuteEx(ref info); }
}
