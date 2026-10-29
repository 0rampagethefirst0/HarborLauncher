using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

// Owned windows exercise Harbor's real foreground events, per-monitor layout,
// menu dismissal and work areas. No game process is modified or terminated.
internal static class Check
{
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct MonitorInfo
    {
        public int Size; public Rect Bounds, Work; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out Rect r);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attribute, out Rect r, int size);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr h, uint flags);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point p, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h, ref MonitorInfo info);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint extended, string name, string title, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr data);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
    static MonitorInfo Monitor(IntPtr handle) { var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" }; GetMonitorInfo(handle, ref info); return info; }
    static void Focus(IntPtr handle)
    {
        uint a = GetCurrentThreadId(), b = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        bool attached = a != b && AttachThreadInput(a, b, true);
        try { SetForegroundWindow(handle); }
        finally { if (attached) AttachThreadInput(a, b, false); }
    }
    static readonly List<object> Results = new();
    [STAThread] static void Main(string[] args)
    {
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        var app = new Application();
        if (args[0] == "--native-neutral")
        {
            var handle = CreateWindowEx(0, "STATIC", "Harbor non-game focus check", 0x10CF0000, 160, 140, 600, 320, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (handle == IntPtr.Zero) throw new InvalidOperationException("The owned native fixture could not open.");
            SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x57); Focus(handle); File.WriteAllText(args[1], handle.ToInt64().ToString());
            int seconds = args.Length >= 3 && int.TryParse(args[2], out int duration) ? Math.Clamp(duration, 5, 120) : 25;
            var until = DateTime.UtcNow.AddSeconds(seconds); var close = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            close.Tick += (_, _) => { if (DateTime.UtcNow >= until || !IsWindow(handle)) { close.Stop(); if (IsWindow(handle)) DestroyWindow(handle); app.Shutdown(); } }; close.Start(); app.Run(); return;
        }
        bool neutral = args[0] == "--neutral";
        var window = new Window { Title = neutral ? "Harbor non-game focus check" : "Harbor game layout check", Width = neutral ? 600 : 1900, Height = neutral ? 320 : 1080, Left = 0, Top = 86, WindowStartupLocation = WindowStartupLocation.Manual, Content = "Harbor is checking desktop behavior. This test window closes automatically." };
        if (neutral && args.Contains("--maximized")) window.WindowState = WindowState.Maximized;
        window.Loaded += async (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (neutral)
            {
                SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0, 0x57);
                Focus(handle); File.WriteAllText(args[1], handle.ToInt64().ToString());
                int seconds = args.Length >= 3 && int.TryParse(args[2], out int duration) ? Math.Clamp(duration, 5, 120) : 25;
                var close = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) }; close.Tick += (_, _) => { close.Stop(); window.Close(); }; close.Start(); return;
            }
            Process? other = null;
            try
            {
                var before = await Status(); var monitor = Monitor(MonitorFromWindow(handle, 2));
                Focus(handle);
                var state = await Wait(s => IsGame(s, handle));
                Assert("Owned windowed game detected", IsGame(state, handle), new { Foreground = GetForegroundWindow().ToInt64(), Expected = handle.ToInt64() });
                await Task.Delay(1000); state = await Status();
                Assert("Only the game monitor hides its bars", HiddenOn(state, monitor.Device) && OtherBarsVisible(state, monitor.Device), SurfaceEvidence(state));
                if (DwmGetWindowAttribute(handle, 9, out var rect, Marshal.SizeOf<Rect>()) != 0) GetWindowRect(handle, out rect);
                var bounds = monitor.Bounds;
                Assert("Visible game frame fits its monitor", rect.Left >= bounds.Left - 1 && rect.Top >= bounds.Top - 1 && rect.Right <= bounds.Right + 1 && rect.Bottom <= bounds.Bottom + 1, new { rect.Left, rect.Top, rect.Right, rect.Bottom });
                Assert("Game monitor receives its full work area", FullArea(state, monitor), SurfaceEvidence(state));
                Assert("Other monitors keep their configured bar space", OtherAreasReserved(state, monitor.Device), SurfaceEvidence(state));

                state = await Status(); if (state["LauncherOpen"]!.GetValue<bool>()) await Command("launcher");
                await Command("launcher"); state = await Wait(s => s["LauncherOpen"]!.GetValue<bool>());
                Assert("Apps screen opens above the fixture", state["LauncherOpen"]!.GetValue<bool>(), null);
                Focus(handle); state = await Wait(s => IsGame(s, handle) && !s["LauncherOpen"]!.GetValue<bool>());
                Assert("Focusing a game closes the Apps screen", IsGame(state, handle) && !state["LauncherOpen"]!.GetValue<bool>(), null);

                window.WindowStyle = WindowStyle.None; window.ResizeMode = ResizeMode.NoResize;
                SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0040); Focus(handle);
                state = await Wait(s => IsGame(s, handle) && HiddenOn(s, monitor.Device));
                Assert("Borderless game hides only its monitor", HiddenOn(state, monitor.Device) && OtherBarsVisible(state, monitor.Device), SurfaceEvidence(state));

                var otherSurface = state["Surfaces"]!.AsArray().FirstOrDefault(s => s!["Monitor"]!.GetValue<string>() != monitor.Device);
                if (otherSurface != null)
                {
                    var bar = otherSurface["Bounds"]!;
                    var secondary = Monitor(MonitorFromPoint(new Point { X = bar["Left"]!.GetValue<int>() + 1, Y = bar["Top"]!.GetValue<int>() + 1 }, 2));
                    SetWindowPos(handle, IntPtr.Zero, secondary.Bounds.Left + 20, secondary.Bounds.Top + 80, 1000, 800, 0x0040); Focus(handle);
                    state = await Wait(s => IsGame(s, handle) && HiddenOn(s, secondary.Device) && OtherBarsVisible(s, secondary.Device) && FullArea(s, secondary));
                    Assert("Moving the game transfers hiding to its new monitor", HiddenOn(state, secondary.Device) && OtherBarsVisible(state, secondary.Device) && FullArea(state, secondary), SurfaceEvidence(state));
                }

                string neutralExe = Path.Combine(AppContext.BaseDirectory, "HarborTestDesktop.exe"), handleFile = args[0] + "." + Environment.ProcessId + ".neutral-handle";
                SetWindowPos(handle, IntPtr.Zero, bounds.Left, bounds.Top, bounds.Right - bounds.Left, bounds.Bottom - bounds.Top, 0x0040); Focus(handle);
                await Wait(s => IsGame(s, handle) && HiddenOn(s, monitor.Device) && FullArea(s, monitor));
                File.Copy(Environment.ProcessPath!, neutralExe, true);
                var start = new ProcessStartInfo(neutralExe) { UseShellExecute = false }; start.ArgumentList.Add("--neutral"); start.ArgumentList.Add(handleFile); start.ArgumentList.Add("25"); start.ArgumentList.Add("--maximized");
                other = Process.Start(start)!;
                for (int i = 0; i < 30 && !File.Exists(handleFile); i++) await Task.Delay(100);
                var neutralHandle = new IntPtr(long.Parse(File.ReadAllText(handleFile))); File.Delete(handleFile); Focus(neutralHandle);
                state = await Wait(s => !s["GameMode"]!["Active"]!.GetValue<bool>() && s["Surfaces"]!.AsArray().All(x => x!["Fullscreen"]!.GetValue<bool>() || x["IsVisible"]!.GetValue<bool>()));
                Assert("Ordinary app focus restores bars outside remaining fullscreen apps", GetForegroundWindow() == neutralHandle && !state["GameMode"]!["Active"]!.GetValue<bool>() && state["Surfaces"]!.AsArray().All(s => s!["Fullscreen"]!.GetValue<bool>() || s["IsVisible"]!.GetValue<bool>()), SurfaceEvidence(state));
                state = await Wait(s => FrameInside(neutralHandle, s));
                Assert("Maximized app fits between the returning bars and stays maximized", IsZoomed(neutralHandle) && FrameInside(neutralHandle, state), SurfaceEvidence(state));
                Assert("Native taskbars stay hidden throughout the check", state["Taskbars"]!.AsArray().All(t => !t!["Visible"]!.GetValue<bool>()), null);
            }
            catch (Exception ex) { Results.Add(new { Name = "Runtime check completed", Passed = false, Error = ex.ToString() }); }
            finally
            {
                if (other != null) { if (!other.HasExited) { other.CloseMainWindow(); if (!other.WaitForExit(1000)) other.Kill(); } other.Dispose(); }
                File.WriteAllText(args[0], System.Text.Json.JsonSerializer.Serialize(Results, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })); window.Close();
            }
        };
        app.Run(window);
    }
    static bool IsGame(JsonNode s, IntPtr h) => s["GameMode"]!["Active"]!.GetValue<bool>() && s["GameMode"]!["Handle"]!.GetValue<long>() == h.ToInt64();
    static bool FrameInside(IntPtr handle, JsonNode state)
    {
        var monitor = Monitor(MonitorFromWindow(handle, 2));
        var surface = state["Surfaces"]!.AsArray().First(s => s!["Monitor"]!.GetValue<string>() == monitor.Device)!; var work = surface["WorkArea"]!;
        if (DwmGetWindowAttribute(handle, 9, out var frame, Marshal.SizeOf<Rect>()) != 0) GetWindowRect(handle, out frame);
        return frame.Left >= work["Left"]!.GetValue<int>() - 1 && frame.Top >= work["Top"]!.GetValue<int>() - 1 && frame.Right <= work["Right"]!.GetValue<int>() + 1 && frame.Bottom <= work["Bottom"]!.GetValue<int>() + 1;
    }
    static bool HiddenOn(JsonNode s, string monitor) => s["Surfaces"]!.AsArray().Where(x => x!["Monitor"]!.GetValue<string>() == monitor).All(x => !x!["IsVisible"]!.GetValue<bool>());
    static bool OtherBarsVisible(JsonNode s, string monitor) => s["Surfaces"]!.AsArray().Where(x => x!["Monitor"]!.GetValue<string>() != monitor).All(x => x!["Fullscreen"]!.GetValue<bool>() || x["IsVisible"]!.GetValue<bool>());
    static bool FullArea(JsonNode s, MonitorInfo monitor) => s["Surfaces"]!.AsArray().Where(x => x!["Monitor"]!.GetValue<string>() == monitor.Device).All(x =>
    {
        var work = x!["WorkArea"]!; var bounds = monitor.Bounds;
        return work["Left"]!.GetValue<int>() == bounds.Left && work["Top"]!.GetValue<int>() == bounds.Top && work["Right"]!.GetValue<int>() == bounds.Right && work["Bottom"]!.GetValue<int>() == bounds.Bottom;
    });
    static bool OtherAreasUnchanged(JsonNode before, JsonNode after, string monitor) => after["Surfaces"]!.AsArray().Where(x => x!["Monitor"]!.GetValue<string>() != monitor).All(x =>
        JsonNode.DeepEquals(x!["WorkArea"], before["Surfaces"]!.AsArray().First(y => y!["Monitor"]!.GetValue<string>() == x["Monitor"]!.GetValue<string>())!["WorkArea"]));
    static bool OtherAreasReserved(JsonNode state, string activeMonitor)
    {
        bool overlay = state["DockInteraction"]?["Enabled"]?.GetValue<bool>() == true;
        foreach (var group in state["Surfaces"]!.AsArray().Where(s => s!["Monitor"]!.GetValue<string>() != activeMonitor).GroupBy(s => s!["Monitor"]!.GetValue<string>()))
        {
            var bar = group.First(s => s!["Title"]!.GetValue<string>().Contains("top bar"))!; var dock = group.First(s => s!["Title"]!.GetValue<string>().Contains("dock"))!;
            var top = bar["Bounds"]!; var bottom = dock["Bounds"]!; var work = bar["WorkArea"]!;
            var monitor = Monitor(MonitorFromPoint(new Point { X = top["Left"]!.GetValue<int>() + 1, Y = top["Top"]!.GetValue<int>() + 1 }, 2));
            int topHeight = top["Bottom"]!.GetValue<int>() - top["Top"]!.GetValue<int>(), bottomHeight = overlay ? 0 : bottom["Bottom"]!.GetValue<int>() - bottom["Top"]!.GetValue<int>();
            if (work["Top"]!.GetValue<int>() != monitor.Bounds.Top + topHeight || work["Bottom"]!.GetValue<int>() != monitor.Bounds.Bottom - bottomHeight) return false;
        }
        return true;
    }
    static object SurfaceEvidence(JsonNode s) => s["Surfaces"]!.AsArray().Select(x => new { Monitor = x!["Monitor"]!.GetValue<string>(), Visible = x["IsVisible"]!.GetValue<bool>(), Work = x["WorkArea"]!.DeepClone() }).ToArray();
    static void Assert(string name, bool passed, object? evidence) => Results.Add(new { Name = name, Passed = passed, Evidence = evidence });
    static async Task<JsonNode> Wait(Func<JsonNode, bool> predicate)
    {
        JsonNode state = await Status();
        for (int i = 0; i < 24 && !predicate(state); i++) { await Task.Delay(200); state = await Status(); }
        return state;
    }
    static async Task<JsonNode> Status() => JsonNode.Parse(await Command("status"))!;
    static async Task<string> Command(string text)
    {
        using var pipe = new NamedPipeClientStream(".", "Harbor.Desktop." + Environment.UserName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(2000);
        // The server closes after its response. Disposing a buffered writer then
        // would flush a closed pipe; it has already been flushed explicitly here.
        var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync(text); using var reader = new StreamReader(pipe, leaveOpen: true);
        return await reader.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(3));
    }
}
