using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

internal sealed partial class Files
{
    readonly StackPanel recycleActions = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    readonly DispatcherTimer recycleRefresh = new() { Interval = TimeSpan.FromSeconds(2) };
    List<RecycledItem> recycled = new();
    bool readingRecycle, leavingRecycle;
    internal bool IsRecycle => CurrentFolder == RecycleBin.Location;
    internal int RecycledCount => recycled.Count;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr owner, string? root, uint flags);
    void InitializeRecycle()
    {
        ((DockPanel)fileActions.Parent).Children.Add(recycleActions);
        recycleActions.Children.Add(ActionButton("up", "Restore selected", () => RestoreRecycled(false)));
        recycleActions.Children.Add(ActionButton("up", "Restore all", () => RestoreRecycled(true)));
        recycleActions.Children.Add(ActionButton("trash", "Empty bin", () => { if (!IsRecycle || recycled.Count == 0) return; int result = SHEmptyRecycleBin(new WindowInteropHelper(this).Handle, null, 0); if (result != 0) status.Text = "The Recycle Bin was not emptied."; Load(RecycleBin.Location, false); }));
        recycleRefresh.Tick += async (_, _) => { if (IsRecycle && !busy) await ReadRecycle(false); };
        Closed += (_, _) => recycleRefresh.Stop();
    }
    async Task<bool> NavigateRecycleAsync(string target, bool history)
    {
        if (target != RecycleBin.Location && !target.Equals("Recycle Bin", StringComparison.OrdinalIgnoreCase) && !target.Equals("shell:RecycleBinFolder", StringComparison.OrdinalIgnoreCase)) { recycleActions.Visibility = Visibility.Collapsed; recycleRefresh.Stop(); if (empty.Tag is UIElement originalEmpty) empty.Child = originalEmpty; return false; }
        leavingRecycle = true; generation++; watcher?.Dispose(); watcher = null; watcherDelay.Stop(); active.Visit(RecycleBin.Location, history); active.Label = "Recycle Bin";
        homeView.Visibility = appFolderView.Visibility = fileActions.Visibility = sortSelector.Visibility = sortDirection.Visibility = Visibility.Collapsed; recycleActions.Visibility = list.Visibility = Visibility.Visible;
        previousPage.Visibility = nextPage.Visibility = Visibility.Collapsed; heading.Text = "Recycle Bin"; Title = "Recycle Bin — Harbor Files";
        backButton.IsEnabled = active.Back.Count > 0; forwardButton.IsEnabled = active.Forward.Count > 0; RenderTabs(); RenderSidebar(); RenderLocation(); SetAddressMode(false);
        string previousView = shell.Config.FilesView; shell.Config.FilesView = "details"; ConfigureView(); shell.Config.FilesView = previousView;
        var headers = ((GridView)list.View).ColumnHeaderContainerStyle;
        var emptyBin = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; emptyBin.Children.Add(new BinArt()); emptyBin.Children.Add(Theme.Label("The Recycle Bin is empty", 23)); emptyBin.Children.Add(Theme.Label("Deleted items from Windows and Harbor appear here.", 14, Theme.Muted)); empty.Child = emptyBin;
        var view = new GridView { ColumnHeaderContainerStyle = headers }; view.Columns.Add(new GridViewColumn { Header = "Name", DisplayMemberBinding = new Binding("Name"), Width = 240 }); view.Columns.Add(new GridViewColumn { Header = "Original location", DisplayMemberBinding = new Binding("OriginalFolder"), Width = 280 }); view.Columns.Add(new GridViewColumn { Header = "Deleted", DisplayMemberBinding = new Binding("Deleted") { StringFormat = "MMM d, yyyy  HH:mm" }, Width = 170 }); view.Columns.Add(new GridViewColumn { Header = "Size", DisplayMemberBinding = new Binding("DisplaySize"), Width = 90 });
        list.GroupStyle.Clear(); list.View = view; list.ItemTemplate = null; list.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(VirtualizingStackPanel)));
        await ReadRecycle(true); recycleRefresh.Start(); Motion.Enter(list, 0); return true;
    }
    async Task ReadRecycle(bool force)
    {
        if (readingRecycle || closing) return; readingRecycle = true;
        try
        {
            var items = await RecycleBin.ReadAsync(); if (!IsRecycle || closing) return;
            if (!force && recycled.SequenceEqual(items)) return;
            using var viewport = ScrollPosition.Keep(FindScroller(list) ?? (FrameworkElement)list);
            var selected = list.SelectedItems.OfType<RecycledItem>().Select(i => i.Path).ToHashSet(StringComparer.OrdinalIgnoreCase); recycled = items; list.ItemsSource = recycled.Where(i => (i.Name + " " + i.OriginalFolder).Contains(active.Search, StringComparison.CurrentCultureIgnoreCase)).ToList();
            foreach (var item in list.Items.OfType<RecycledItem>()) if (selected.Contains(item.Path)) list.SelectedItems.Add(item);
            empty.Visibility = list.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed; summary.Text = recycled.Count + " deleted items · restore them to their original folders"; status.Text = recycled.Count + " items · " + FileTools.Size(recycled.Sum(i => i.Bytes)); RenderRecyclePreview();
        }
        catch (Exception ex) { status.Text = "Could not read the Recycle Bin: " + ex.Message; }
        finally { readingRecycle = false; }
    }
    async void RestoreRecycled(bool all)
    {
        if (!IsRecycle || busy) return; string[] paths = (all ? recycled : list.SelectedItems.OfType<RecycledItem>()).Select(i => i.Path).ToArray(); if (paths.Length == 0) return;
        if (all && !NameDialog.Confirm(this, "Restore all items", "Restore " + paths.Length + " items to their original locations?")) return;
        busy = true; status.Text = "Restoring…";
        try { await RecycleBin.RestoreAsync(paths); await Task.Delay(350); await ReadRecycle(true); }
        catch (Exception ex) { status.Text = ex.Message; }
        finally { busy = false; }
    }
    void BuildRecycleMenu(ContextMenu menu)
    {
        menu.Items.Clear(); menu.Items.Add(Theme.Item("Restore selected", () => RestoreRecycled(false))); menu.Items.Add(Theme.Item("Restore all", () => RestoreRecycled(true))); menu.Items.Add(Theme.Item("Refresh", () => Load(RecycleBin.Location, false)));
    }
    void RenderRecyclePreview()
    {
        preview.Children.Clear(); preview.Children.Add(new BinArt { Full = recycled.Count > 0, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 14) });
        if (list.SelectedItem is RecycledItem item) { preview.Children.Add(Theme.Label(item.Name, 22)); Note("Original location\n" + item.OriginalFolder); Note("Deleted " + item.Deleted.ToString("MMM d, yyyy  h:mm tt")); Note(FileTools.Size(item.Bytes)); preview.Children.Add(ActionButton("up", "Restore this item", () => RestoreRecycled(false))); }
        else { preview.Children.Add(Theme.Label("Your deleted items", 23)); Note("These are the files in Windows’ real Recycle Bin. Select an item to restore it to its original folder."); }
        void Note(string text) => preview.Children.Add(new TextBlock { Text = text, FontSize = 14, TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, Margin = new Thickness(0, 12, 0, 8) });
    }
    internal void PrepareRecyclePreview()
    {
        recycled = new() { new RecycledItem(@"C:\fixture\notes.txt", "Project notes.txt", @"C:\Projects", DateTime.Today.AddHours(8), 2048, false), new RecycledItem(@"C:\fixture\draft.zip", "Old draft.zip", @"C:\Downloads", DateTime.Today.AddDays(-1), 1048576, false) };
        list.ItemsSource = recycled; list.SelectedItem = recycled[0]; summary.Text = "2 deleted items · sample data"; empty.Visibility = Visibility.Collapsed; RenderRecyclePreview();
    }
}
