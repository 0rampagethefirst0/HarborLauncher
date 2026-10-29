using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace Harbor;

internal sealed class Shell : IDisposable
{
    internal Settings Config { get; }
    internal Catalog Catalog { get; } = new();
    internal WindowTracker Tracker { get; } = new();
    internal Recovery Recovery { get; }
    internal Games Games { get; }
    internal Mods Mods { get; }
    internal Tweaks Tweaks { get; }
    internal WindowSpace WindowSpace { get; }
    internal DockInteraction DockInteraction { get; }
    internal bool DockMenuOpen => launcher?.IsVisible == true || flyout?.IsVisible == true;
    internal Notifications Notifications { get; }
    internal Dictionary<string, object> ModRuntime { get; } = new();
    internal double CpuUsage { get; private set; }
    internal uint MemoryUsage { get; private set; }
    readonly List<TopBar> bars = new();
    readonly List<Dock> docks = new();
    readonly List<Desktop> desktops = new();
    readonly Dictionary<IntPtr, (uint Pid, Native.Placement Placement)> desktopUndo = new();
    IntPtr desktopForeground;
    ModCatalog? modCatalog;
    Files? files;
    Appearance? appearance;
    ProfileWindow? profiles;
    readonly HwndSource messages;
    readonly DispatcherTimer timer;
    readonly DispatcherTimer transientWatch = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(100) };
    DateTime transientOpened;
    readonly Native.WinEventProc? foregroundChanged;
    readonly IntPtr foregroundHook;
    readonly Native.WinEventProc? taskbarShown;
    readonly IntPtr taskbarHook;
    readonly bool barsOnly;
    int foregroundPending;
    readonly Dictionary<IntPtr, Native.Placement> layoutUndo = new();
    Keyboard? keyboard;
    Launcher? launcher;
    SettingsWindow? settings;
    Window? flyout;
    bool disposed, rebuilding;
    ulong oldIdle, oldTotal;
    string lastWindowSet = "";
    // Used only by the offscreen visual check; it never creates shell surfaces or hooks.
    internal Shell(Settings config)
    {
        Config = config;
        Games = new Games(config);
        Mods = new Mods(config); Notifications = new Notifications(config); Mods.ApplyTheme();
        Tweaks = new Tweaks(this);
        WindowSpace = new WindowSpace(this);
        DockInteraction = new DockInteraction(this, false);
        Recovery = null!; messages = null!; timer = null!;
    }
    internal Shell(bool barsOnly = false)
    {
        this.barsOnly = barsOnly;
        Config = Settings.Load();
        Games = new Games(Config);
        Mods = new Mods(Config); Notifications = new Notifications(Config); Mods.ApplyTheme(); Mods.Changed += RebuildMods;
        Tweaks = new Tweaks(this);
        WindowSpace = new WindowSpace(this);
        DockInteraction = new DockInteraction(this, true);
        transientWatch.Tick += (_, _) => CloseTransientWhenExternal();
        Recovery = new Recovery();
        Catalog.SeedPins(Config);
        messages = new HwndSource(new HwndSourceParameters("Harbor message window") { ParentWindow = new IntPtr(-3), WindowStyle = 0 });
        messages.AddHook(MessageHook);
        bool launcherHotkey = Native.RegisterHotKey(messages.Handle, 1, 0x4003, 0x20);
        bool restoreHotkey = Native.RegisterHotKey(messages.Handle, 2, 0x4003, 0x7B);
        Log.Write($"Hotkeys: launcher={launcherHotkey}; restore={restoreHotkey}");
        foregroundChanged = ForegroundChanged;
        foregroundHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, foregroundChanged, 0, 0, 0);
        taskbarShown = (_, _, window, objectId, childId, _, _) =>
        {
            if (objectId != 0 || childId != 0 || disposed || !Config.ReplaceTaskbar || !Recovery.ShouldHideTaskbars) return;
            if (Native.Class(window) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") Native.ShowWindowAsync(window, 0);
        };
        taskbarHook = Native.SetWinEventHook(0x8002, 0x8002, IntPtr.Zero, taskbarShown, 0, 0, 0);
        Tracker.Changed += WindowsChanged;
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        Recovery.SetReplacement(Config.ReplaceTaskbar);
        UpdateGame(); BuildSurfaces();
        BuildDesktops();
        ApplySettings();
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick(); timer.Start();
        Tracker.Refresh(); Catalog.Start(); Tick();
        Log.Write("Harbor started. Render mode=" + System.Windows.Media.RenderOptions.ProcessRenderMode);
    }
    internal IntPtr CurrentMonitor()
    {
        Native.GetCursorPos(out var point);
        return Native.MonitorFromPoint(point, 2);
    }
    void DisplaysChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(new Action(() => { BuildSurfaces(); BuildDesktops(); }));
    void ForegroundChanged(IntPtr hook, uint kind, IntPtr window, int objectId, int childId, uint threadId, uint time)
    {
        Native.TrackForeground(window);
        if (disposed || Interlocked.Exchange(ref foregroundPending, 1) != 0) return;
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            Volatile.Write(ref foregroundPending, 0);
            if (disposed) return;
            CloseTransientWhenExternal();
            UpdateGame();
            UpdateWorkAreas();
            WindowSpace.Tick();
            Tracker.Refresh();
            foreach (var bar in bars) bar.Tick();
            foreach (var dock in docks) dock.Tick();
        }));
    }
    void BuildSurfaces()
    {
        if (disposed || rebuilding) return;
        rebuilding = true;
        try
        {
            foreach (var b in bars.ToArray()) b.Close(); foreach (var d in docks.ToArray()) d.Close();
            bars.Clear(); docks.Clear();
            foreach (var screen in Forms.Screen.AllScreens.Where(s => Config.AllMonitors || s.Primary))
            {
                var point = new Native.Point { X = screen.Bounds.Left + screen.Bounds.Width / 2, Y = screen.Bounds.Top + screen.Bounds.Height / 2 };
                var monitor = Native.MonitorFromPoint(point, 2);
                var bar = new TopBar(this, monitor); bars.Add(bar); bar.Show();
                var dock = new Dock(this, monitor); docks.Add(dock); dock.Show();
            }
        }
        finally { rebuilding = false; }
    }
    internal void ApplySettings(bool rebuild = false)
    {
        Config.Save();
        Games.Invalidate(); UpdateGame();
        Recovery.SetReplacement(Config.ReplaceTaskbar);
        if (Config.ReplaceTaskbar && keyboard == null) keyboard = new Keyboard(Application.Current.Dispatcher, () => ToggleLauncher(CurrentMonitor()), Shortcut, DockInteraction.NotifyTyping);
        else if (!Config.ReplaceTaskbar && keyboard != null) { keyboard.Dispose(); keyboard = null; }
        if (rebuild) BuildSurfaces();
        else foreach (var dock in docks) dock.Refresh(true);
        Mods.ApplyTheme();
        if (rebuild || Config.ReplaceDesktop != (desktops.Count > 0)) BuildDesktops();
        // The native taskbar work area changes asynchronously. Re-negotiate bars afterwards.
        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => { foreach (var b in bars) b.Tick(); foreach (var d in docks) d.Tick(); }));
    }
    internal static bool KeysMatch(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        a = AppIdentity.Normalize(a); b = AppIdentity.Normalize(b);
        if (a.Equals(b, StringComparison.OrdinalIgnoreCase)) return true;
        if (AppIdentity.Packaged(a) || AppIdentity.Packaged(b)) return false;
        // A protected process can expose its name without exposing its full path.
        if ((!a.Contains('\\') && !a.Contains('/')) || (!b.Contains('\\') && !b.Contains('/')))
            return Path.GetFileNameWithoutExtension(a).Equals(Path.GetFileNameWithoutExtension(b), StringComparison.OrdinalIgnoreCase);
        return false;
    }
    internal bool HideBars(IntPtr monitor) => (Config.HideInGames && Games.Active && Native.MonitorFromWindow(Games.ActiveWindow, 2) == monitor) || (Config.HideInFullscreen && Native.FullscreenOn(monitor));
    void BuildDesktops()
    {
        foreach (var desktop in desktops.ToArray()) desktop.Close(); desktops.Clear();
        if (barsOnly || !Config.ReplaceDesktop || Native.DesktopIconWindows().Count == 0) { Recovery.SetDesktop(false); return; }
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
            var desktop = new Desktop(this, monitor, screen.Primary); desktops.Add(desktop); desktop.Show();
        }
        Recovery.SetDesktop(true);
    }
    internal void RebuildMods()
    {
        Tweaks.Reload();
        Mods.ApplyTheme(); BuildDesktops();
        foreach (var bar in bars) bar.Tick();
        foreach (var dock in docks) dock.Refresh(true);
    }
    internal void OpenProfiles()
    {
        launcher?.Close(); flyout?.Close();
        if (profiles?.IsVisible == true) { Native.Activate(new WindowInteropHelper(profiles).Handle); return; }
        profiles = new ProfileWindow(this); profiles.Closed += (_, _) => profiles = null; profiles.Show(); Native.Activate(new WindowInteropHelper(profiles).Handle);
    }
    internal void OpenAppMods(WindowEntry window)
    {
        OpenMods(); modCatalog?.ShowAppRules(window);
    }
    internal void OpenMods()
    {
        launcher?.Close(); flyout?.Close();
        if (modCatalog?.IsVisible == true) { Native.Activate(new WindowInteropHelper(modCatalog).Handle); return; }
        modCatalog = new ModCatalog(this); modCatalog.Closed += (_, _) => modCatalog = null; modCatalog.Show(); Native.Activate(new WindowInteropHelper(modCatalog).Handle);
    }
    internal void OpenAppearance()
    {
        launcher?.Close(); flyout?.Close();
        if (appearance?.IsVisible == true) { Native.Activate(new WindowInteropHelper(appearance).Handle); return; }
        appearance = new Appearance(this); appearance.Closed += (_, _) => appearance = null; appearance.Show(); Native.Activate(new WindowInteropHelper(appearance).Handle);
    }
    internal void OpenFiles(string? path = null)
    {
        launcher?.Close(); flyout?.Close();
        if (files?.IsVisible == true) { if (path != null) files.Load(path); Native.Activate(new WindowInteropHelper(files).Handle); return; }
        files = new Files(this, path); files.Closed += (_, _) => files = null; files.Show(); Native.Activate(new WindowInteropHelper(files).Handle);
    }
    internal void Launch(string path)
    {
        string target = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? Catalog.Resolve(path) : path;
        if (Config.UseHarborFiles && (Directory.Exists(target) || target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) || target.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase)))
        { OpenFiles(Directory.Exists(target) ? target : null); return; }
        Native.Launch(path);
    }
    void Shortcut(uint key)
    {
        switch (key) { case 0x45: OpenFiles(); break; case 0x44: ToggleDesktop(); break; case 0x4E: OpenNotifications(CurrentMonitor()); break; case 0x41: OpenQuick(CurrentMonitor()); break; }
    }
    internal void ToggleDesktop()
    {
        launcher?.Close(); flyout?.Close();
        if (desktopUndo.Count > 0)
        {
            foreach (var entry in desktopUndo) { Native.GetWindowThreadProcessId(entry.Key, out uint pid); if (!Native.IsWindow(entry.Key) || pid != entry.Value.Pid || !Native.IsIconic(entry.Key)) continue; var placement = entry.Value.Placement; Native.SetWindowPlacement(entry.Key, ref placement); }
            desktopUndo.Clear(); if (Native.IsWindow(desktopForeground)) Native.Activate(desktopForeground); return;
        }
        desktopForeground = Native.GetForegroundWindow();
        foreach (var window in Tracker.Windows)
        {
            if (Native.IsIconic(window.Handle)) continue;
            var placement = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() }; Native.GetWindowThreadProcessId(window.Handle, out uint pid);
            if (Native.GetWindowPlacement(window.Handle, ref placement)) { desktopUndo[window.Handle] = (pid, placement); Native.ShowWindowAsync(window.Handle, 6); }
        }
        var host = Native.GetParent(Native.DesktopIconWindows().FirstOrDefault()); if (host != IntPtr.Zero) Native.Activate(host);
    }
    void UpdateWorkAreas()
    {
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
            int top = BarLayout.ReservedTop(Config, screen.Primary);
            Recovery.UpdateWorkArea(monitor, HideBars(monitor), Config.AllMonitors || screen.Primary, top, DockInteraction.Enabled ? 0 : Config.DockHeight);
        }
    }
    void UpdateGame()
    {
        if (!Games.Update() || !Config.HideInGames || !Games.Active) return;
        IntPtr game = Games.ActiveWindow;
        // Let ABM_REMOVE finish before fitting an already-cut-off windowed game.
        var fit = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        fit.Tick += (_, _) => { fit.Stop(); if (!disposed && Games.ActiveWindow == game) Native.KeepOnScreen(game); };
        fit.Start();
    }
    internal void ToggleGame(string path)
    {
        bool game = Games.IsGame(path);
        Config.GameApps.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        Config.GameExclusions.RemoveAll(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        (game ? Config.GameExclusions : Config.GameApps).Add(path);
        ApplySettings();
    }
    internal void Pin(Pin pin)
    {
        if (Config.Pins.Any(p => p.Path.Equals(pin.Path, StringComparison.OrdinalIgnoreCase) || KeysMatch(p.Target, pin.Target))) return;
        Config.Pins.Add(pin); Config.Save(); foreach (var d in docks) d.Refresh(true);
    }
    internal void Unpin(Pin pin) { Config.Pins.Remove(pin); Config.Save(); foreach (var d in docks) d.Refresh(true); }
    internal void MovePin(Pin pin, int direction)
    {
        int index = Config.Pins.IndexOf(pin), target = index + direction;
        if (index < 0 || target < 0 || target >= Config.Pins.Count) return;
        Config.Pins.RemoveAt(index); Config.Pins.Insert(target, pin); Config.Save(); foreach (var d in docks) d.Refresh(true);
    }
    internal void ToggleLauncher(IntPtr monitor)
    {
        if (launcher?.IsVisible == true) { launcher.Close(); launcher = null; return; }
        flyout?.Close();
        launcher = new Launcher(this, monitor);
        launcher.Closed += (_, _) => launcher = null;
        launcher.Show();
        Native.Activate(new WindowInteropHelper(launcher).Handle);
        transientOpened = DateTime.UtcNow; transientWatch.Start();
    }
    void CloseTransientWhenExternal()
    {
        if (disposed || (launcher?.IsVisible != true && flyout?.IsVisible != true)) { transientWatch.Stop(); return; }
        if (DateTime.UtcNow - transientOpened < TimeSpan.FromMilliseconds(250)) return;
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;
        Native.GetWindowThreadProcessId(foreground, out uint owner);
        if (owner == Environment.ProcessId) return;
        launcher?.Close(); flyout?.Close(); transientWatch.Stop();
    }
    internal void OpenSettings()
    {
        launcher?.Close(); flyout?.Close();
        if (settings?.IsVisible == true) { Native.Activate(new WindowInteropHelper(settings).Handle); return; }
        settings = new SettingsWindow(this); settings.Closed += (_, _) => settings = null; settings.Show(); Native.Activate(new WindowInteropHelper(settings).Handle);
    }
    void OpenFlyout(Func<Window> create)
    {
        launcher?.Close(); flyout?.Close();
        var next = create(); flyout = next; next.Closed += (_, _) => { if (flyout == next) flyout = null; }; next.Show(); Native.Activate(new WindowInteropHelper(next).Handle); transientOpened = DateTime.UtcNow; transientWatch.Start();
    }
    internal void OpenQuick(IntPtr monitor) => OpenFlyout(() => new QuickPanel(this, monitor));
    internal void OpenCalendar(IntPtr monitor) => OpenFlyout(() => new CalendarPanel(monitor));
    internal void OpenArrange(IntPtr monitor) => OpenFlyout(() => new ArrangePanel(this, monitor));
    internal void OpenNotifications(IntPtr monitor) => OpenFlyout(() => new NotificationPanel(this, monitor));
    internal void OpenDockControls(IntPtr monitor, Native.Rect bounds) => OpenFlyout(() => new DockControls(this, monitor, bounds));
    internal void OpenTopDockControls(IntPtr monitor) => OpenFlyout(() => new TopDockControls(this, monitor));
    internal void ApplyTopDockPreferences()
    {
        Config.Normalize(); foreach (var bar in bars) bar.Tick();
        if (Recovery != null) { UpdateGame(); UpdateWorkAreas(); foreach (var dock in docks) dock.Tick(); WindowSpace.Tick(); }
    }
    internal void OpenDockControls()
    {
        var monitor = CurrentMonitor(); var dock = docks.FirstOrDefault(d => d.MonitorHandle == monitor);
        if (dock != null) { Native.GetWindowRect(new WindowInteropHelper(dock).Handle, out var bounds); OpenDockControls(monitor, bounds); }
    }
    internal void ApplyDockPreferences()
    {
        Config.Normalize();
        foreach (var dock in docks) dock.Refresh(true);
        DockInteraction.Refresh();
        if (Recovery != null) { UpdateWorkAreas(); WindowSpace.Tick(); }
    }
    void WindowsChanged()
    {
        foreach (var dock in docks) dock.Refresh();
        string ids = string.Join(",", Tracker.Windows.Select(w => w.Handle).OrderBy(h => h.ToInt64()));
        bool changed = lastWindowSet != ids; lastWindowSet = ids;
        if (Config.AutoTile && changed) foreach (var b in bars) if (!HideBars(b.MonitorHandle)) Arrange(b.MonitorHandle, "grid", true);
    }
    void Tick()
    {
        Recovery.Tick();
        UpdateGame();
        UpdateWorkAreas();
        WindowSpace.Tick();
        Tweaks.Tick();
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            ulong total = kernel.Value + user.Value;
            if (oldTotal > 0 && total > oldTotal) CpuUsage = Math.Clamp(100.0 * (1 - (double)(idle.Value - oldIdle) / (total - oldTotal)), 0, 100);
            oldIdle = idle.Value; oldTotal = total;
        }
        var memory = new Native.MemoryInfo { Length = (uint)Marshal.SizeOf<Native.MemoryInfo>() };
        if (Native.GlobalMemoryStatusEx(ref memory)) MemoryUsage = memory.Load;
        foreach (var b in bars) b.Tick(); foreach (var d in docks) d.Tick();
        if (!barsOnly && Config.ReplaceDesktop && (desktops.Count == 0 || desktops.Any(d => !d.Healthy))) BuildDesktops();
        foreach (var desktop in desktops) if (!HideBars(desktop.MonitorHandle)) desktop.Tick();
        if (!Games.Active) Notifications.Poll();
        Tracker.Refresh();
    }
    internal void Arrange(IntPtr monitor, string layout, bool auto = false)
    {
        if (HideBars(monitor)) return;
        var windows = Tracker.Windows.Where(w => w.Monitor == monitor && !Native.IsIconic(w.Handle) && (Native.GetWindowLong(w.Handle, -16).ToInt64() & 0x40000) != 0).Take(16).ToList();
        if (windows.Count == 0) return;
        var work = Native.Monitor(monitor).Work; int gap = (int)(10 * Native.Scale(monitor));
        int cols = layout == "columns" ? windows.Count : layout == "stack" ? 1 : (int)Math.Ceiling(Math.Sqrt(windows.Count));
        int rows = layout == "stack" ? windows.Count : (int)Math.Ceiling(windows.Count / (double)cols);
        if (!auto) foreach (var w in windows) layoutUndo.Remove(w.Handle);
        for (int i = 0; i < windows.Count; i++)
        {
            var w = windows[i];
            if (!layoutUndo.ContainsKey(w.Handle))
            {
                var placement = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() };
                if (Native.GetWindowPlacement(w.Handle, ref placement)) layoutUndo[w.Handle] = placement;
            }
            int row = i / cols, col = i % cols;
            int x1 = work.Left + gap + col * (work.Width - gap) / cols;
            int y1 = work.Top + gap + row * (work.Height - gap) / rows;
            int x2 = work.Left + (col + 1) * (work.Width - gap) / cols;
            int y2 = work.Top + (row + 1) * (work.Height - gap) / rows;
            if (Native.IsZoomed(w.Handle)) Native.ShowWindowAsync(w.Handle, 9);
            Native.SetWindowPos(w.Handle, IntPtr.Zero, x1, y1, x2 - x1, y2 - y1, 0x14);
        }
    }
    internal void PlaceWindow(IntPtr window, Native.Rect rect)
    {
        if (!Native.IsWindow(window) || Native.IsIconic(window)) return;
        if (!layoutUndo.ContainsKey(window)) { var placement = new Native.Placement { Length = Marshal.SizeOf<Native.Placement>() }; if (Native.GetWindowPlacement(window, ref placement)) layoutUndo[window] = placement; }
        if (Native.IsZoomed(window)) Native.ShowWindowAsync(window, 9);
        Native.SetWindowPos(window, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height, 0x4014);
    }
    internal void UndoLayout(IntPtr monitor)
    {
        foreach (var pair in layoutUndo.ToArray())
        {
            if (!Native.IsWindow(pair.Key)) { layoutUndo.Remove(pair.Key); continue; }
            if (Native.MonitorFromWindow(pair.Key, 2) != monitor) continue;
            var placement = pair.Value; Native.SetWindowPlacement(pair.Key, ref placement); layoutUndo.Remove(pair.Key);
        }
    }
    IntPtr MessageHook(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
    {
        if (msg == 0x312)
        {
            if (w.ToInt32() == 1) ToggleLauncher(CurrentMonitor());
            else if (w.ToInt32() == 2) Exit();
            handled = true;
        }
        if (msg == 0x11) { Dispose(); handled = false; }
        return IntPtr.Zero;
    }
    internal string Command(string command)
    {
        switch (command.Trim().ToLowerInvariant())
        {
            case "launcher": ToggleLauncher(CurrentMonitor()); return "ok";
            case "settings": OpenSettings(); return "ok";
            case "quick": OpenQuick(CurrentMonitor()); return "ok";
            case "calendar": OpenCalendar(CurrentMonitor()); return "ok";
            case "arrange": OpenArrange(CurrentMonitor()); return "ok";
            case "mods": OpenMods(); return "ok";
            case "appearance": OpenAppearance(); return "ok";
            case "profiles": OpenProfiles(); return "ok";
            case "dock-controls": OpenDockControls(); return "ok";
            case "top-dock-controls": OpenTopDockControls(CurrentMonitor()); return "ok";
            case "files": OpenFiles(); return "ok";
            case "notifications": OpenNotifications(CurrentMonitor()); return "ok";
            case "refresh-notifications": Notifications.Poll(true); return "ok";
            case "close-panels": launcher?.Close(); flyout?.Close(); settings?.Close(); modCatalog?.Close(); files?.Close(); appearance?.Close(); return "ok";
            case "desktop": ToggleDesktop(); return "ok";
            case "exit": Application.Current.Dispatcher.BeginInvoke(new Action(Exit)); return "ok";
            case "status": return Status();
            default: return "Unknown command";
        }
    }
    internal string Status()
    {
        using var process = Process.GetCurrentProcess();
        object Surface(Window w, IntPtr monitor)
        {
            Native.GetWindowRect(new WindowInteropHelper(w).Handle, out var r);
            var m = Native.Monitor(monitor);
            return new { w.Title, Handle = new WindowInteropHelper(w).Handle.ToInt64(), w.IsVisible, Monitor = m.Device, Dpi = Native.GetDpiForWindow(new WindowInteropHelper(w).Handle), Bounds = new { r.Left, r.Top, r.Right, r.Bottom }, WorkArea = new { m.Work.Left, m.Work.Top, m.Work.Right, m.Work.Bottom }, Fullscreen = Native.FullscreenOn(monitor) };
        }
        return JsonSerializer.Serialize(new {
            Version = Program.Version, Pid = Environment.ProcessId, RenderMode = System.Windows.Media.RenderOptions.ProcessRenderMode.ToString(),
            GameMode = new { Enabled = Config.HideInGames, Games.Active, Handle = Games.ActiveWindow.ToInt64(), Path = Games.ActivePath },
            NativeTaskbarState = Native.TaskbarState(),
            DesktopCount = desktops.Count, FilesOpen = files?.IsVisible == true, ModsOpen = modCatalog?.IsVisible == true,
            AppearanceOpen = appearance?.IsVisible == true, BuilderOpen = Application.Current.Windows.OfType<ModDesigner>().Any(w => w.IsVisible),
            ProfilesOpen = profiles?.IsVisible == true, ProfileCount = Config.Profiles.Count, BehaviorMods = Tweaks.Summary(),
            WindowSpaceAdjustments = WindowSpace.Adjustments,
            DockInteraction = DockInteraction.Status(),
            WindowFrames = Tracker.Windows.Select(w => { Native.GetWindowRect(w.Handle, out var outer); var frame = Native.VisibleBounds(w.Handle, outer); return new { Handle = w.Handle.ToInt64(), w.ProcessName, w.Title, Maximized = Native.IsZoomed(w.Handle), Minimized = Native.IsIconic(w.Handle), Monitor = Native.Monitor(w.Monitor).Device, Frame = new { frame.Left, frame.Top, frame.Right, frame.Bottom } }; }),
            WindowGroups = Tracker.Windows.GroupBy(w => w.Key, StringComparer.OrdinalIgnoreCase).Select(g => new { Key = g.Key, AppId = g.First().AppId, Name = g.First().ProcessName, Count = g.Count() }),
            Desktops = desktops.Select(d => new { Handle = d.Handle.ToInt64(), d.Healthy, Parent = Native.GetParent(d.Handle).ToInt64(), Monitor = Native.Monitor(d.MonitorHandle).Device }),
            ModCount = Mods.Catalog.Count, EnabledMods = Mods.Enabled.Select(m => m.Id), Notifications = new { Count = Notifications.Items.Count, Notifications.SyncStatus },
            ReplaceTaskbar = Config.ReplaceTaskbar, RecoveryActive = File.Exists(Path.Combine(Settings.DataDir, "recovery.json")),
            AppCount = Catalog.Apps.Count, Catalog.Ready, PinCount = Config.Pins.Count, WindowCount = Tracker.Windows.Count,
            MemoryMB = Math.Round(process.WorkingSet64 / 1048576.0, 1), PrivateMemoryMB = Math.Round(process.PrivateMemorySize64 / 1048576.0, 1), CpuTimeSeconds = process.TotalProcessorTime.TotalSeconds,
            LauncherOpen = launcher?.IsVisible == true, SettingsOpen = settings?.IsVisible == true,
            Taskbars = Native.Taskbars().Select(h => new { Handle = h.ToInt64(), Visible = Native.IsWindowVisible(h), Class = Native.Class(h) }),
            Surfaces = bars.Select(b => Surface(b, b.MonitorHandle)).Concat(docks.Select(d => Surface(d, d.MonitorHandle)))
        }, new JsonSerializerOptions { WriteIndented = true });
    }
    internal void Exit()
    {
        Dispose();
        // All shell state is restored before ending the process and its input hook.
        Environment.Exit(0);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Log.Write("Exiting Harbor.");
        timer?.Stop();
        transientWatch.Stop();
        if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
        if (taskbarHook != IntPtr.Zero) Native.UnhookWinEvent(taskbarHook);
        keyboard?.Dispose(); keyboard = null;
        DockInteraction.Dispose();
        Native.UnregisterHotKey(messages.Handle, 1); Native.UnregisterHotKey(messages.Handle, 2);
        SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        launcher?.Close(); settings?.Close(); flyout?.Close(); modCatalog?.Close(); files?.Close(); appearance?.Close(); profiles?.Close();
        foreach (var desktop in desktops.ToArray()) desktop.Close(); desktops.Clear();
        foreach (var b in bars.ToArray()) b.Close(); foreach (var d in docks.ToArray()) d.Close();
        bars.Clear(); docks.Clear();
        Recovery.Dispose(); messages.Dispose();
        Log.Write("Shell cleanup complete.");
    }
}
