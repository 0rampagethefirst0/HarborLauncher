using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

// A refresh keeps its viewport; deliberate navigation starts at the top.
// Each viewer owns at most one pending restore, canceled by fresh user input.
internal sealed class ScrollPosition : IDisposable
{
    static readonly ConditionalWeakTable<ScrollViewer, Pending> pending = new();
    sealed class Pending { internal Action? Cancel; }
    readonly ScrollViewer? viewer;
    readonly double x, y;
    readonly Pending? owner;
    readonly MouseWheelEventHandler wheel;
    readonly MouseButtonEventHandler click;
    readonly KeyEventHandler key;
    readonly RequestBringIntoViewEventHandler bring;
    bool canceled, finished;
    ScrollPosition(ScrollViewer? viewer, bool keep)
    {
        this.viewer = viewer; wheel = (_, _) => Cancel(); click = (_, _) => Cancel(); key = (_, _) => Cancel(); bring = (_, e) => e.Handled = true;
        if (viewer == null) return;
        owner = pending.GetValue(viewer, _ => new Pending()); owner.Cancel?.Invoke(); owner.Cancel = Cancel;
        ScrollMotion.Stop(viewer); x = keep ? viewer.HorizontalOffset : 0; y = keep ? viewer.VerticalOffset : 0;
        viewer.AddHandler(Mouse.PreviewMouseWheelEvent, wheel, true); viewer.AddHandler(Mouse.PreviewMouseDownEvent, click, true); viewer.AddHandler(System.Windows.Input.Keyboard.PreviewKeyDownEvent, key, true); viewer.RequestBringIntoView += bring;
    }
    internal static ScrollPosition Keep(FrameworkElement content, bool keep = true) => new(Find(content), keep);
    static ScrollViewer? Find(DependencyObject? content)
    {
        for (var node = content; node != null; node = VisualTreeHelper.GetParent(node)) if (node is ScrollViewer viewer) return viewer;
        return null;
    }
    void Cancel() { canceled = true; Detach(); }
    void Detach()
    {
        if (finished || viewer == null) return; finished = true;
        viewer.RemoveHandler(Mouse.PreviewMouseWheelEvent, wheel); viewer.RemoveHandler(Mouse.PreviewMouseDownEvent, click); viewer.RemoveHandler(System.Windows.Input.Keyboard.PreviewKeyDownEvent, key); viewer.RequestBringIntoView -= bring;
        if (owner != null && owner.Cancel == Cancel) owner.Cancel = null;
    }
    public void Dispose()
    {
        if (viewer == null || canceled) { Detach(); return; }
        viewer.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() => {
            try { if (!canceled) { viewer.UpdateLayout(); ScrollMotion.Jump(viewer, Math.Min(x, viewer.ScrollableWidth), Math.Min(y, viewer.ScrollableHeight)); } }
            finally { Detach(); }
        }));
    }
}
