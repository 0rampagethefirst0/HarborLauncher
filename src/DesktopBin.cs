using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Harbor;

internal sealed class DesktopBin : Grid
{
    readonly Shell shell;
    readonly TextBlock count = Theme.Label("", 11, Theme.Muted);
    readonly BinArt art = new();
    DateTime nextQuery;
    internal DesktopBin(Shell shell)
    {
        this.shell = shell; HorizontalAlignment = HorizontalAlignment.Center; VerticalAlignment = VerticalAlignment.Center;
        var body = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center }; body.Children.Add(art);
        var title = Theme.Label("Recycle Bin", 13); title.HorizontalAlignment = HorizontalAlignment.Center; body.Children.Add(title); count.HorizontalAlignment = HorizontalAlignment.Center; count.Margin = new Thickness(0, 4, 0, 0); body.Children.Add(count);
        var button = Theme.Button(body, "Open Recycle Bin — deleted files and folders", () => shell.OpenFiles(RecycleBin.Location)); button.Padding = new Thickness(14, 10, 14, 10); Children.Add(button);
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Open Recycle Bin", () => shell.OpenFiles(RecycleBin.Location))); menu.Items.Add(Theme.Item("Refresh", () => { nextQuery = DateTime.MinValue; Tick(); })); ContextMenu = menu;
        Tick();
    }
    internal void Tick()
    {
        Visibility = shell.Config.DesktopRecycleBin ? Visibility.Visible : Visibility.Collapsed;
        RenderTransform = new TranslateTransform(0, -shell.Config.DesktopLogoSize / 2d - 100);
        if (DateTime.UtcNow < nextQuery) return; nextQuery = DateTime.UtcNow.AddSeconds(5);
        var info = RecycleBin.Query(); count.Text = info.Items == 0 ? "Empty" : info.Items + (info.Items == 1 ? " item" : " items"); art.Full = info.Items > 0; art.InvalidateVisual();
    }
}
internal sealed class BinArt : FrameworkElement
{
    internal bool Full { get; set; }
    internal BinArt() { Width = 64; Height = 62; HorizontalAlignment = HorizontalAlignment.Center; }
    protected override void OnRender(DrawingContext dc)
    {
        var edge = new Pen(Theme.Accent, 2); dc.DrawRoundedRectangle(Theme.Raised, edge, new Rect(18, 19, 28, 34), 5, 5);
        if (Full) { dc.DrawRoundedRectangle(Theme.Accent, null, new Rect(23, 8, 13, 22), 2, 2); dc.DrawRoundedRectangle(Theme.Muted, null, new Rect(33, 11, 10, 20), 2, 2); }
        dc.DrawRoundedRectangle(Theme.Panel, edge, new Rect(13, 15, 38, 5), 2, 2); dc.DrawRoundedRectangle(Theme.Panel, edge, new Rect(25, 8, 14, 7), 2, 2);
        foreach (double x in new[] { 27d, 37d }) dc.DrawLine(new Pen(Theme.Accent, 1.5), new Point(x, 29), new Point(x, 44));
    }
}
