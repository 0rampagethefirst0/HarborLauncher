using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace Harbor;

internal static class ElementFade
{
    static readonly ConditionalWeakTable<FrameworkElement, State> states = new();
    internal static void Visible(FrameworkElement element, bool visible) => states.GetValue(element, e => new State(e)).Visible(visible);
    sealed class State
    {
        readonly FrameworkElement element; bool target, fading; int revision;
        internal State(FrameworkElement element) { this.element = element; target = element.Visibility == Visibility.Visible; }
        internal void Visible(bool visible)
        {
            if (target == visible && (fading || (element.Visibility == Visibility.Visible) == visible)) return;
            target = visible; int current = ++revision; double from = element.Opacity; element.BeginAnimation(UIElement.OpacityProperty, null);
            if (visible && element.Visibility != Visibility.Visible) { element.Opacity = from = 0; element.Visibility = Visibility.Visible; }
            element.IsHitTestVisible = visible;
            if (!Motion.Enabled(element)) { element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed; element.Opacity = 1; fading = false; return; }
            fading = true; element.Opacity = from; double to = visible ? 1 : 0; var animation = Motion.Animation(from, to, visible ? 190 : 150); animation.FillBehavior = FillBehavior.HoldEnd; animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
            animation.Completed += (_, _) => { if (current != revision) return; fading = false; element.Opacity = to; element.BeginAnimation(UIElement.OpacityProperty, null); if (!visible) element.Visibility = Visibility.Collapsed; };
            element.BeginAnimation(UIElement.OpacityProperty, animation);
        }
    }
}
