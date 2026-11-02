using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Harbor;

internal static class QuickSettingsMotionVerification
{
    internal static void Run(Action<string, bool, object?> check)
    {
        var surface = new Border { Width = 470, Height = 670 };
        using var motion = new QuickSettingsMotion(surface, true);
        motion.Enter(); Pump(); motion.Seek(60); Pump();
        check("Control-panel entrance renders an intermediate fade and scale", surface.Opacity > 0 && surface.Opacity < 1 && ((ScaleTransform)((TransformGroup)surface.RenderTransform).Children[0]).ScaleX < 1, surface.Opacity);
        motion.Seek(300); Pump();
        check("Entrance finishes at full size without an idle animation clock", surface.Opacity == 1 && motion.ActiveCount == 0, motion.ActiveCount);
        var host = new Grid(); var first = new Border(); var second = new Border(); var third = new Border();
        motion.Swap(host, first); motion.Swap(host, second); Pump(); motion.Seek(60); Pump();
        check("Settings pages overlap briefly during their slide and crossfade", host.Children.Count == 2 && second.Opacity > 0 && second.Opacity < 1 && !first.IsHitTestVisible, second.Opacity);
        motion.Swap(host, third, -1); Pump(); motion.Seek(300); Pump();
        check("Rapid tab changes leave only the newest page interactive", host.Children.Count == 1 && ReferenceEquals(host.Children[0], third) && third.IsHitTestVisible && third.Opacity == 1, host.Children.Count);
        var parent = new StackPanel { Width = 400 }; var expansion = new Border { Child = new Border { Height = 80 }, Visibility = Visibility.Collapsed }; parent.Children.Add(expansion); parent.Measure(new Size(400, 500)); parent.Arrange(new Rect(0, 0, 400, 500));
        motion.Expand(expansion, true); Pump(); motion.Seek(60); Pump();
        check("Extra app options expand through a partial height", expansion.Visibility == Visibility.Visible && expansion.Height > 0 && expansion.Height < 80, expansion.Height);
        motion.Expand(expansion, false); motion.Expand(expansion, true); Pump(); motion.Seek(300); Pump();
        check("Reversing expansion keeps the newest choice and restores automatic sizing", expansion.Visibility == Visibility.Visible && double.IsNaN(expansion.Height) && expansion.Opacity == 1, null);
        int exits = 0; motion.Exit(() => exits++); Pump(); motion.Seek(300); Pump();
        check("Animated close finishes once and releases its clocks", exits == 1 && motion.ActiveCount == 0, new { exits, motion.ActiveCount });
        var immediateSurface = new Border(); using var immediate = new QuickSettingsMotion(immediateSurface);
        int closed = 0; immediate.Exit(() => closed++); var unanimated = new Grid(); immediate.Swap(unanimated, new Border()); immediate.Swap(unanimated, new Border());
        check("Hidden or motion-disabled panels close and switch without waiting", closed == 1 && immediate.ActiveCount == 0 && unanimated.Children.Count == 1, null);
    }
    static void Pump()
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
}
