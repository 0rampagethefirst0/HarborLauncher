using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Harbor;

internal static class Motion
{
    static Settings? config;
    static readonly ConditionalWeakTable<FrameworkElement, TranslateTransform> offsets = new();
    static readonly ConditionalWeakTable<FrameworkElement, HoverState> hovering = new();
    internal static void Configure(Settings value) => config = value;
    internal static int TargetFPS => config?.AnimationFramesPerSecond ?? 120;
    internal static bool Enabled(FrameworkElement element) => Settings.TestDataDir == null && (config?.DockMotionMilliseconds ?? 170) > 0 && element.IsVisible;
    internal static DoubleAnimation Track(DoubleAnimation animation)
    {
        Timeline.SetDesiredFrameRate(animation, TargetFPS);
        if (animation.Duration.HasTimeSpan) AnimationTiming.For((int)animation.Duration.TimeSpan.TotalMilliseconds);
        return animation;
    }
    internal static DoubleAnimation Animation(double from, double to, int? milliseconds = null)
    {
        AnimationTiming.For(milliseconds ?? config?.DockMotionMilliseconds ?? 170);
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds ?? config?.DockMotionMilliseconds ?? 170)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(animation, TargetFPS); return animation;
    }
    internal static void Enter(FrameworkElement element, double distance = 10, int? milliseconds = null)
    {
        if (!Enabled(element)) return;
        var offset = Offset(element); offset.BeginAnimation(TranslateTransform.YProperty, null); offset.Y = 0;
        element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = 0;
        var fade = Animation(0, 1, milliseconds ?? 190); fade.FillBehavior = FillBehavior.HoldEnd;
        fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
        fade.Completed += (_, _) => { element.Opacity = 1; element.BeginAnimation(UIElement.OpacityProperty, null); };
        element.BeginAnimation(UIElement.OpacityProperty, fade);
    }
    internal static void Move(FrameworkElement element, double x)
    {
        if (!Enabled(element) || Math.Abs(x) < .5 || Math.Abs(x) > 600) return;
        var offset = Offset(element); double from = offset.X + x; offset.BeginAnimation(TranslateTransform.XProperty, null); offset.X = 0; offset.BeginAnimation(TranslateTransform.XProperty, Animation(from, 0));
    }
    static TranslateTransform Offset(FrameworkElement element) => offsets.GetValue(element, e => { var offset = new TranslateTransform(); var group = new TransformGroup(); if (e.RenderTransform != null && e.RenderTransform != Transform.Identity) group.Children.Add(e.RenderTransform); group.Children.Add(offset); e.RenderTransform = group; return offset; });
    internal static void Hover(FrameworkElement element) => hovering.GetValue(element, e => new HoverState(e));
    internal static void PauseHover(FrameworkElement element, bool paused) { if (hovering.TryGetValue(element, out var state)) state.Pause(paused); }
    sealed class HoverState
    {
        readonly FrameworkElement element;
        ScaleTransform? scale = null;
        bool paused;
        internal HoverState(FrameworkElement element) { this.element = element; element.MouseEnter += (_, _) => To(1.025); element.MouseLeave += (_, _) => To(1); }
        internal void Pause(bool value)
        {
            if (paused == value) return; paused = value;
            if (scale != null) { double current = scale.ScaleX; scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = current; }
            if (!value) To(element.IsMouseOver ? 1.025 : 1);
        }
        void To(double value) {
            if (paused || !Enabled(element)) return;
            if (scale == null) { scale = new ScaleTransform(1, 1); var group = new TransformGroup(); if (element.RenderTransform != null && element.RenderTransform != Transform.Identity) group.Children.Add(element.RenderTransform); group.Children.Add(scale); element.RenderTransformOrigin = new Point(.5, .5); element.RenderTransform = group; }
            double from = scale.ScaleX; scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = value;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(from, value, Math.Min(150, config?.DockMotionMilliseconds ?? 170))); scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(from, value, Math.Min(150, config?.DockMotionMilliseconds ?? 170)));
        }
    }
    internal static void SmoothScroll(ScrollViewer viewer, bool horizontal = false)
        => ScrollMotion.Attach(viewer, horizontal);
    internal static void Reflow(Panel panel, Func<bool> busy)
    {
        var previous = new Dictionary<FrameworkElement, double>();
        panel.LayoutUpdated += (_, _) => {
            var keep = new HashSet<FrameworkElement>();
            foreach (FrameworkElement child in panel.Children) { keep.Add(child); double x = System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(child).X;
                if (!busy() && previous.TryGetValue(child, out double before)) Move(child, before - x); previous[child] = x;
            }
            foreach (var old in new List<FrameworkElement>(previous.Keys)) if (!keep.Contains(old)) previous.Remove(old);
        };
    }
}
