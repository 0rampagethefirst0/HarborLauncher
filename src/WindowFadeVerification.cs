using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Harbor;

internal static class WindowFadeVerification
{
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory); var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; Theme.Install(); Motion.Configure(new Settings { DockMotionMilliseconds = 190 });
        var rows = new List<object>(); int failures = 0; IntPtr foreground = Native.GetForegroundWindow();
        void Check(string name, bool passed, object? detail = null) { rows.Add(new { Name = name, Passed = passed, Detail = detail }); if (!passed) failures++; }
        var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) }; timeout.Tick += (_, _) => { timeout.Stop(); Check("Fade verification completed within its time bound", false); app.Shutdown(1); }; timeout.Start();
        app.Dispatcher.BeginInvoke(new Action(async () =>
        {
            try
            {
                Window Create()
                {
                    var window = new Window { Width = 220, Height = 100, Title = "Harbor fade verification", ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None, Content = new TextBlock { Text = "Owned fade test" } };
                    WindowFade.Attach(window); window.SourceInitialized += (_, _) => { var handle = new WindowInteropHelper(window).Handle; Native.ToolWindow(window); if (!Native.CloakWindow(handle, true)) throw new InvalidOperationException("The test window could not be concealed."); }; return window;
                }
                var panel = Create(); var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); panel.Closed += (_, _) => ended.SetResult(); panel.Show();
                await Task.Delay(65); Check("A newly opened panel passes through a real intermediate fade", panel.Opacity > 0 && panel.Opacity < 1, new { panel.Opacity });
                await Task.Delay(240); Check("The opening fade finishes fully visible", panel.IsVisible && Math.Abs(panel.Opacity - 1) < .001);
                WindowFade.Visible(panel, false); await Task.Delay(55); Check("Hiding fades out and releases pointer interaction immediately", panel.Opacity is > 0 and < 1 && !panel.IsHitTestVisible);
                WindowFade.Visible(panel, true); await Task.Delay(240); Check("Reappearing reverses an unfinished fade without disappearing", panel.IsVisible && panel.IsHitTestVisible && Math.Abs(panel.Opacity - 1) < .001);
                panel.Close(); await ended.Task;
                var modal = Create(); var answer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; answer.Tick += (_, _) => { answer.Stop(); modal.DialogResult = true; }; answer.Start(); bool? result = modal.ShowDialog();
                Check("Modal confirmation results survive the closing fade", result == true);
                Check("Concealed fade tests preserve the user's foreground window", Native.GetForegroundWindow() == foreground);
            }
            catch (Exception ex) { Check("Fade verification finished", false, ex.ToString()); }
            finally { timeout.Stop(); app.Shutdown(failures == 0 ? 0 : 1); }
        }));
        app.Run(); File.WriteAllText(Path.Combine(directory, "verification.json"), JsonSerializer.Serialize(rows, Mods.Json)); Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
