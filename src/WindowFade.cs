using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace Harbor;

internal static class WindowFade
{
    static readonly ConditionalWeakTable<Window, State> states = new();
    internal static void Attach(Window window)
    {
        if (Settings.TestDataDir != null || window is PluginQuickSettings) return;
        states.GetValue(window, w => new State(w));
    }
    internal static void Visible(Window window, bool visible)
    {
        if (Settings.TestDataDir != null) { if (visible) window.Show(); else window.Hide(); return; }
        states.GetValue(window, w => new State(w)).Visible(visible);
    }
    sealed class State
    {
        readonly Window window;
        bool target, fading, closeReady, closed, closing;
        int revision;
        internal State(Window window)
        {
            this.window = window; target = window.IsVisible; if (!window.IsVisible) window.Opacity = 0;
            window.SourceInitialized += (_, _) => { Native.DisableWindowTransitions(new WindowInteropHelper(window).Handle); if (!window.IsVisible && Motion.TargetFPS > 0) window.Opacity = 0; };
            window.IsVisibleChanged += (_, _) => { if (closed || fading || !window.IsVisible) return; target = true; Fade(1); };
            window.Closing += (_, e) => { if (closed || closeReady || !window.IsVisible || window.Dispatcher.HasShutdownStarted || !Motion.Enabled(window)) return; bool? dialogResult = window.DialogResult; e.Cancel = true; if (closing) return; closing = true; target = false; window.IsHitTestVisible = false; Fade(0, () => { closeReady = true; if (dialogResult.HasValue) window.DialogResult = dialogResult; else window.Close(); }); };
            window.Closed += (_, _) => { closed = true; revision++; };
        }
        internal void Visible(bool visible)
        {
            if (closed || closing || target == visible && (fading || window.IsVisible == visible)) return;
            target = visible; revision++; window.IsHitTestVisible = visible;
            if (visible)
            {
                if (!window.IsVisible) { window.Opacity = 0; fading = true; window.Show(); fading = false; }
                Fade(1);
            }
            else if (window.IsVisible) Fade(0, () => window.Hide());
        }
        void Fade(double to, Action? completed = null)
        {
            int current = ++revision; double from = window.Opacity; window.BeginAnimation(Window.OpacityProperty, null); window.Opacity = from;
            if (!Motion.Enabled(window)) { fading = false; window.Opacity = to; completed?.Invoke(); return; }
            fading = true;
            var animation = Motion.Animation(from, to, to > from ? 190 : 150); animation.FillBehavior = FillBehavior.HoldEnd;
            animation.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut };
            animation.Completed += (_, _) => { if (closed || current != revision) return; window.Opacity = to; window.BeginAnimation(Window.OpacityProperty, null); fading = false; completed?.Invoke(); };
            window.BeginAnimation(Window.OpacityProperty, animation);
        }
    }
}
