using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Harbor;

// Short, event-driven transitions. No bitmap captures, blur or idle render loop.
internal sealed class QuickSettingsMotion : IDisposable
{
    sealed record Running(DependencyObject Target, DependencyProperty Property, AnimationClock Clock, string Scope);
    readonly FrameworkElement surface;
    readonly bool test;
    readonly List<Running> running = new();
    readonly TranslateTransform offset = new();
    readonly ScaleTransform scale = new(1, 1);
    bool disposed;
    internal bool Enabled => !disposed && (test || Motion.Enabled(surface));
    internal int ActiveCount => running.Count;
    internal QuickSettingsMotion(FrameworkElement surface, bool test = false)
    {
        this.surface = surface; this.test = test && Settings.TestDataDir != null;
        surface.RenderTransformOrigin = new Point(.5, .25);
        surface.RenderTransform = new TransformGroup { Children = { scale, offset } };
    }
    internal void Enter()
    {
        if (!Enabled) return;
        Animate(surface, UIElement.OpacityProperty, 0, 1, 220, "window");
        Animate(offset, TranslateTransform.YProperty, 12, 0, 220, "window");
        Animate(scale, ScaleTransform.ScaleXProperty, .97, 1, 220, "window");
        Animate(scale, ScaleTransform.ScaleYProperty, .97, 1, 220, "window");
    }
    internal void Exit(Action completed)
    {
        if (!Enabled) { completed(); return; }
        double opacity = surface.Opacity, y = offset.Y, size = scale.ScaleX;
        Abort("window");
        Animate(offset, TranslateTransform.YProperty, y, 8, 140, "window");
        Animate(scale, ScaleTransform.ScaleXProperty, size, .98, 140, "window");
        Animate(scale, ScaleTransform.ScaleYProperty, size, .98, 140, "window");
        Animate(surface, UIElement.OpacityProperty, opacity, 0, 140, "window", completed);
    }
    internal void Swap(Grid host, FrameworkElement next, int direction = 1)
    {
        var previous = host.Children.OfType<FrameworkElement>().LastOrDefault();
        double opacity = previous?.Opacity ?? 1;
        Abort("page"); Abort("expand"); host.Children.Clear();
        if (!Enabled || previous == null) { host.Children.Add(next); return; }
        previous.IsHitTestVisible = false; host.Children.Add(previous); host.Children.Add(next);
        var outgoing = new TranslateTransform(); previous.RenderTransform = outgoing;
        var incoming = new TranslateTransform(); next.RenderTransform = incoming;
        Animate(previous, UIElement.OpacityProperty, opacity, 0, 160, "page", () => host.Children.Remove(previous));
        Animate(outgoing, direction == 0 ? TranslateTransform.YProperty : TranslateTransform.XProperty, 0, direction == 0 ? -6 : -18 * direction, 180, "page");
        Animate(next, UIElement.OpacityProperty, 0, 1, 200, "page");
        Animate(incoming, direction == 0 ? TranslateTransform.YProperty : TranslateTransform.XProperty, direction == 0 ? 8 : 24 * direction, 0, 220, "page");
    }
    internal void Indicator(TranslateTransform indicator, double x)
    {
        if (!Enabled) { indicator.X = x; return; }
        Animate(indicator, TranslateTransform.XProperty, indicator.X, x, 200, "navigation");
    }
    internal void Expand(FrameworkElement element, bool open)
    {
        double from = element.Visibility == Visibility.Collapsed ? 0 : element.ActualHeight;
        double opacity = element.Opacity;
        Abort("expand");
        if (!Enabled) { element.Height = double.NaN; element.Visibility = open ? Visibility.Visible : Visibility.Collapsed; element.Opacity = 1; return; }
        if (open)
        {
            element.Visibility = Visibility.Visible; element.Height = double.NaN;
            element.Measure(new Size(Math.Max(100, ((element.Parent as FrameworkElement)?.ActualWidth ?? surface.ActualWidth) - 4), double.PositiveInfinity));
            double height = Math.Max(element.MinHeight, element.DesiredSize.Height);
            element.ClipToBounds = true;
            Animate(element, FrameworkElement.HeightProperty, from, height, 220, "expand", () => element.Height = double.NaN);
            Animate(element, UIElement.OpacityProperty, from == 0 ? 0 : opacity, 1, 180, "expand");
        }
        else
        {
            Animate(element, UIElement.OpacityProperty, opacity, 0, 130, "expand");
            Animate(element, FrameworkElement.HeightProperty, from, 0, 180, "expand", () => { element.Visibility = Visibility.Collapsed; element.Height = double.NaN; element.Opacity = 1; });
        }
    }
    void Animate(DependencyObject target, DependencyProperty property, double from, double to, int milliseconds, string scope, Action? completed = null)
    {
        foreach (var old in running.Where(r => ReferenceEquals(r.Target, target) && r.Property == property).ToArray()) Remove(old);
        target.SetValue(property, to);
        var clock = (AnimationClock)Motion.Animation(from, to, milliseconds).CreateClock(true);
        var item = new Running(target, property, clock, scope); running.Add(item);
        clock.Completed += (_, _) => { if (!running.Contains(item)) return; Remove(item); completed?.Invoke(); };
        Apply(target, property, clock); clock.Controller!.Begin();
    }
    static void Apply(DependencyObject target, DependencyProperty property, AnimationClock? clock)
    {
        if (target is UIElement element) element.ApplyAnimationClock(property, clock);
        else ((Animatable)target).ApplyAnimationClock(property, clock);
    }
    void Remove(Running item) { running.Remove(item); item.Clock.Controller?.Stop(); Apply(item.Target, item.Property, null); }
    void Abort(string scope) { foreach (var item in running.Where(r => r.Scope == scope).ToArray()) Remove(item); }
    internal void Seek(int milliseconds) { if (!test) throw new InvalidOperationException("Only isolated motion checks can seek clocks."); foreach (var item in running.ToArray()) { item.Clock.Controller!.Pause(); item.Clock.Controller.SeekAlignedToLastTick(TimeSpan.FromMilliseconds(milliseconds), TimeSeekOrigin.BeginTime); } }
    public void Dispose() { disposed = true; foreach (var item in running.ToArray()) Remove(item); }
}
