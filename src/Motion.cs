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
    static readonly ConditionalWeakTable<ScrollViewer, object> scrolling = new();
    static readonly ConditionalWeakTable<FrameworkElement, TranslateTransform> offsets = new();
    static readonly DependencyProperty ScrollY = DependencyProperty.RegisterAttached("ScrollY", typeof(double), typeof(Motion), new PropertyMetadata(0d, (d, e) => ((ScrollViewer)d).ScrollToVerticalOffset((double)e.NewValue)));
    static readonly DependencyProperty ScrollX = DependencyProperty.RegisterAttached("ScrollX", typeof(double), typeof(Motion), new PropertyMetadata(0d, (d, e) => ((ScrollViewer)d).ScrollToHorizontalOffset((double)e.NewValue)));
    internal static void Configure(Settings value) => config = value;
    internal static int TargetFPS => config?.AnimationFramesPerSecond ?? 60;
    internal static bool Enabled(FrameworkElement element) => Settings.TestDataDir == null && (config?.DockMotionMilliseconds ?? 170) > 0 && element.IsVisible;
    internal static DoubleAnimation Animation(double from, double to, int? milliseconds = null)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds ?? config?.DockMotionMilliseconds ?? 170)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        Timeline.SetDesiredFrameRate(animation, TargetFPS); return animation;
    }
    internal static void Enter(FrameworkElement element, double distance = 10)
    {
        if (!Enabled(element)) return;
        var offset = Offset(element); offset.BeginAnimation(TranslateTransform.YProperty, null); offset.Y = 0;
        offset.BeginAnimation(TranslateTransform.YProperty, Animation(distance, 0));
        element.BeginAnimation(UIElement.OpacityProperty, null); element.Opacity = 1; element.BeginAnimation(UIElement.OpacityProperty, Animation(.15, 1));
    }
    internal static void Move(FrameworkElement element, double x)
    {
        if (!Enabled(element) || Math.Abs(x) < .5 || Math.Abs(x) > 600) return;
        var offset = Offset(element); double from = offset.X + x; offset.BeginAnimation(TranslateTransform.XProperty, null); offset.X = 0; offset.BeginAnimation(TranslateTransform.XProperty, Animation(from, 0));
    }
    static TranslateTransform Offset(FrameworkElement element) => offsets.GetValue(element, e => { var offset = new TranslateTransform(); var group = new TransformGroup(); if (e.RenderTransform != null && e.RenderTransform != Transform.Identity) group.Children.Add(e.RenderTransform); group.Children.Add(offset); e.RenderTransform = group; return offset; });
    internal static void Hover(FrameworkElement element)
    {
        ScaleTransform? scale = null;
        void To(double value) {
            if (!Enabled(element)) return;
            if (scale == null) { scale = new ScaleTransform(1, 1); var group = new TransformGroup(); if (element.RenderTransform != null && element.RenderTransform != Transform.Identity) group.Children.Add(element.RenderTransform); group.Children.Add(scale); element.RenderTransformOrigin = new Point(.5, .5); element.RenderTransform = group; }
            double from = scale.ScaleX; scale.BeginAnimation(ScaleTransform.ScaleXProperty, null); scale.BeginAnimation(ScaleTransform.ScaleYProperty, null); scale.ScaleX = scale.ScaleY = value;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, Animation(from, value, Math.Min(150, config?.DockMotionMilliseconds ?? 170))); scale.BeginAnimation(ScaleTransform.ScaleYProperty, Animation(from, value, Math.Min(150, config?.DockMotionMilliseconds ?? 170)));
        }
        element.MouseEnter += (_, _) => To(1.025); element.MouseLeave += (_, _) => To(1);
    }
    internal static void SmoothScroll(ScrollViewer viewer, bool horizontal = false)
    {
        if (scrolling.TryGetValue(viewer, out _)) return; scrolling.Add(viewer, new object());
        viewer.PreviewMouseWheel += (_, e) => {
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
            bool side = horizontal || System.Windows.Input.Keyboard.Modifiers.HasFlag(ModifierKeys.Shift); double max = side ? viewer.ScrollableWidth : viewer.ScrollableHeight;
            if (max <= 0) return; var property = side ? ScrollX : ScrollY; double current = side ? viewer.HorizontalOffset : viewer.VerticalOffset;
            double previousTarget = viewer.HasAnimatedProperties ? (double)viewer.GetAnimationBaseValue(property) : current;
            double target = Math.Clamp(previousTarget - e.Delta * .8, 0, max); viewer.BeginAnimation(property, null); viewer.SetCurrentValue(property, target);
            if (Enabled(viewer)) viewer.BeginAnimation(property, Animation(current, target, 150)); e.Handled = true;
        };
    }
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
