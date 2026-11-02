using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Harbor;

// Keep a real base value after an animation ends. SetCurrentValue is temporary
// under an animation and can revert an attached offset to its default of zero.
internal static class ScrollMotion
{
    static readonly ConditionalWeakTable<ScrollViewer, State> states = new();
    static readonly DependencyProperty Y = DependencyProperty.RegisterAttached("Y", typeof(double), typeof(ScrollMotion), new PropertyMetadata(0d, (d, e) => ((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));
    static readonly DependencyProperty X = DependencyProperty.RegisterAttached("X", typeof(double), typeof(ScrollMotion), new PropertyMetadata(0d, (d, e) => ((ScrollViewer)d).ScrollToHorizontalOffset((double)e.NewValue)));
    internal static void Attach(ScrollViewer viewer, bool horizontal) => states.GetValue(viewer, v => new State(v, horizontal));
    internal static void Stop(ScrollViewer viewer) { if (states.TryGetValue(viewer, out var state)) state.Stop(); }
    internal static void Jump(ScrollViewer viewer, double x, double y)
    {
        Stop(viewer); viewer.ScrollToHorizontalOffset(x); viewer.ScrollToVerticalOffset(y);
    }
    sealed class Axis
    {
        readonly ScrollViewer viewer;
        readonly bool horizontal;
        readonly DependencyProperty property;
        int revision;
        bool active;
        double target;
        double Current => horizontal ? viewer.HorizontalOffset : viewer.VerticalOffset;
        internal Axis(ScrollViewer viewer, bool horizontal) { this.viewer = viewer; this.horizontal = horizontal; property = horizontal ? X : Y; }
        internal void Stop()
        {
            active = false; revision++;
            double current = Current;
            viewer.SetValue(property, current); // commit before removing the clock
            viewer.BeginAnimation(property, null);
        }
        internal void Wheel(int delta, double max)
        {
            double from = Current, next = Math.Clamp((active ? target : from) - delta * .8, 0, max);
            Stop(); target = next;
            viewer.SetValue(property, next);
            if (!Motion.Enabled(viewer)) return;
            active = true; int stamp = revision;
            var animation = Motion.Animation(from, next, 150); animation.FillBehavior = FillBehavior.HoldEnd;
            animation.Completed += (_, _) => {
                if (stamp != revision) return;
                active = false; viewer.SetValue(property, next); viewer.BeginAnimation(property, null);
            };
            viewer.BeginAnimation(property, animation);
        }
    }
    sealed class State
    {
        readonly ScrollViewer viewer;
        readonly Axis x, y;
        internal State(ScrollViewer viewer, bool horizontal)
        {
            this.viewer = viewer; x = new Axis(viewer, true); y = new Axis(viewer, false);
            viewer.PreviewMouseWheel += (_, e) => {
                if (e.Handled || System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
                bool side = horizontal || System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                if (!OwnsWheel(e.OriginalSource as DependencyObject, viewer, side, e.Delta)) return;
                double max = side ? viewer.ScrollableWidth : viewer.ScrollableHeight;
                if (max <= 0) return;
                (side ? x : y).Wheel(e.Delta, max); e.Handled = true;
            };
            viewer.PreviewMouseDown += (_, _) => Stop();
            viewer.PreviewKeyDown += (_, e) => { if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End or Key.Space) Stop(); };
            viewer.ScrollChanged += (_, e) => { if (ReferenceEquals(e.OriginalSource, viewer) && (e.ExtentHeightChange != 0 || e.ExtentWidthChange != 0 || e.ViewportHeightChange != 0 || e.ViewportWidthChange != 0)) Stop(); };
            viewer.Unloaded += (_, _) => Stop();
        }
        internal void Stop() { x.Stop(); y.Stop(); }
    }
    static bool OwnsWheel(DependencyObject? source, ScrollViewer viewer, bool side, int delta)
    {
        for (var node = source; node != null; node = Parent(node))
        {
            if (ReferenceEquals(node, viewer)) return true;
            if (node is ScrollViewer child)
            {
                double max = side ? child.ScrollableWidth : child.ScrollableHeight, offset = side ? child.HorizontalOffset : child.VerticalOffset;
                if (max > 0 && (delta < 0 ? offset < max - .5 : offset > .5)) return false;
            }
        }
        return source == null;
    }
    static DependencyObject? Parent(DependencyObject node) => node is Visual || node is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
}
