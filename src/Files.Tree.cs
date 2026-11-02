using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Harbor;

internal sealed partial class Files
{
    readonly TreeView folderTree = new() { Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Theme.Text, Margin = new Thickness(0, 2, 0, 4) };
    void RenderFolderTree()
    {
        if (folderTree.Items.Count == 0)
        {
            foreach (var drive in DriveInfo.GetDrives()) folderTree.Items.Add(FolderNode(drive.Name, FriendlyName(drive.Name), true));
            folderTree.SelectedItemChanged += (_, _) => { if (folderTree.SelectedItem is TreeViewItem { Tag: string path } && !path.Equals(CurrentFolder, StringComparison.OrdinalIgnoreCase)) Load(path); };
        }
        sidebar.Children.Add(folderTree);
    }
    TreeViewItem FolderNode(string path, string name, bool drive = false)
    {
        var node = new TreeViewItem { Header = UiIcons.Label(name, 13, drive ? "storage" : "folder"), Tag = path, ToolTip = path }; node.Items.Add(new TreeViewItem { Header = "Loading…", IsEnabled = false }); bool loaded = false;
        node.Expanded += async (_, e) =>
        {
            if (e.OriginalSource != node || loaded) return; e.Handled = true; loaded = true;
            try
            {
                var children = await Task.Run(() => new DirectoryInfo(path).EnumerateDirectories().Where(d => shell.Config.FilesShowHidden || (d.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).Select(d => (d.FullName, d.Name)).ToArray());
                if (closing) return; node.Items.Clear(); foreach (var child in children) node.Items.Add(FolderNode(child.FullName, child.Name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { node.Items.Clear(); node.ToolTip = path + "\n" + ex.Message; }
        };
        node.Collapsed += (_, e) => { if (e.OriginalSource == node) { loaded = false; node.Items.Clear(); node.Items.Add(new TreeViewItem { Header = "Loading…", IsEnabled = false }); } };
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Open", () => Load(path))); menu.Items.Add(Theme.Item("Open in new tab", () => NewTab(path))); menu.Items.Add(Theme.Item("Open in new window", () => OpenNewWindow(path))); menu.Items.Add(Theme.Item("Properties", () => ShellProperties.Show(path, OwnerHandle))); menu.Items.Add(Theme.Item("Full file menu…", () => { try { ShellFileMenu.Show(this, new[] { path }, Path.GetDirectoryName(path) ?? path); } catch (Exception ex) { status.Text = ex.Message; } })); node.ContextMenu = menu;
        return node;
    }
    void OpenNewWindow(string? path = null) { var window = new Files(shell, path ?? CurrentFolder); window.Show(); Native.Activate(new System.Windows.Interop.WindowInteropHelper(window).Handle); }
}
