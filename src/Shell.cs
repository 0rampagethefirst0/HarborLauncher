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
    internal PluginControls Plugins { get; }
    internal bool DockMenuOpen => launcher?.IsVisible == true || flyout?.IsVisible == true || home?.IsVisible == true;
    internal bool AppsOpen => launcher?.IsVisible == true || home?.Ready == true;
    internal Notifications Notifications { get; }
    internal Screenshots Screenshots { get; }
    internal Dictionary<string, object> ModRuntime { get; } = new();
    internal double CpuUsage { get; private set; }
    internal event Action? ShortcutsChanged;
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
    HarborHome? home;
    int homeRequest;
    bool preparingHome;
    StartupPresentation? introPreview;
    SettingsWindow? settings;
    Window? flyout;
    bool disposed, rebuilding;
    string featureLayout = "";
    ulong oldIdle, oldTotal;
    string lastWindowSet = "";
    // Used only by the offscreen visual check; it never creates shell surfaces or hooks.
    internal Shell(Settings config)
    {
        Config = config; Motion.Configure(Config); Screenshots = new Screenshots(this);
        Games = new Games(config);
        Mods = new Mods(config); Notifications = new Notifications(config); Mods.ApplyTheme();
        Tweaks = new Tweaks(this);
        WindowSpace = new WindowSpace(this);
        DockInteraction = new DockInteraction(this, false);
        Plugins = new PluginControls(this);
        Recovery = null!; messages = null!; timer = null!;
    }
    internal Shell(bool barsOnly = false, Action<string>? startupStage = null)
    {
        this.barsOnly = barsOnly;
        Config = Settings.Load(); Motion.Configure(Config); Screenshots = new Screenshots(this);
        Games = new Games(Config);
        Mods = new Mods(Config); Notifications = new Notifications(Config); Mods.ApplyTheme(); Mods.Changed += RebuildMods;
        Tweaks = new Tweaks(this);
        WindowSpace = new WindowSpace(this);
        DockInteraction = new DockInteraction(this, true);
        Plugins = new PluginControls(this, true);
        transientWatch.Tick += (_, _) => CloseTransientWhenExternal();
        Recovery = new Recovery();
        startupStage?.Invoke("Preparing your workspace");
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
        startupStage?.Invoke("Bringing everything together");
        ApplySettings();
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick(); timer.Start();
        Tracker.Refresh(); Catalog.Start(); Tick();
        if (ShellSession.IsShellProcess && (desktops.Count != Forms.Screen.AllScreens.Length || desktops.Any(d => !d.Healthy))) throw new InvalidOperationException("Harbor's independent desktop could not be created on every monitor.");
        Recovery.MarkShellReady();
        if (Config.FeatureSetupPending) { Config.FeatureSetupPending = false; Config.Save(); Application.Current.Dispatcher.BeginInvoke(new Action(() => { OpenMods(); modCatalog?.ShowFeatures(); })); }
        Log.Write("Harbor started. Render mode=" + System.Windows.Media.RenderOptions.ProcessRenderMode);
    }
    internal IntPtr CurrentMonitor()
    {
        Native.GetCursorPos(out var point);
        return Native.MonitorFromPoint(point, 2);
    }
    void DisplaysChanged(object? sender, EventArgs e) => Application.Current.Dispatcher.BeginInvoke(new Action(() => { Plugins.ClosePanel(); BuildSurfaces(); BuildDesktops(); Plugins.Rebuild(); }));
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
            foreach (var screen in Forms.Screen.AllScreens.Where(s => Config.AllMonitors && ShellFeatures.On(Config, "multi-monitor") || s.Primary))
            {
                var point = new Native.Point { X = screen.Bounds.Left + screen.Bounds.Width / 2, Y = screen.Bounds.Top + screen.Bounds.Height / 2 };
                var monitor = Native.MonitorFromPoint(point, 2);
                if (ShellFeatures.On(Config, "top-dock")) { var bar = new TopBar(this, monitor); bars.Add(bar); bar.Show(); }
                if (ShellFeatures.On(Config, "taskbar")) { var dock = new Dock(this, monitor); docks.Add(dock); dock.Show(); }
            }
        }
        finally { rebuilding = false; }
    }
    internal void ApplySettings(bool rebuild = false)
    {
        Plugins.ClosePanel();
        ShellFeatures.Sync(Config, Mods);
        Config.Save();
        Games.Invalidate(); UpdateGame();
        Recovery.SetReplacement(Config.ReplaceTaskbar);
        if (Config.ReplaceTaskbar && keyboard == null) keyboard = new Keyboard(Application.Current.Dispatcher, () => ToggleLauncher(CurrentMonitor()), Shortcut, DockInteraction.NotifyTyping, Screenshots.Take);
        else if (!Config.ReplaceTaskbar && keyboard != null) { keyboard.Dispose(); keyboard = null; }
        if (rebuild || featureLayout != FeatureLayout()) { featureLayout = FeatureLayout(); BuildSurfaces(); }
        else foreach (var dock in docks) dock.Refresh(true);
        Mods.ApplyTheme();
        if (rebuild || Config.ReplaceDesktop != (desktops.Count > 0)) BuildDesktops();
        Plugins.Rebuild();
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
        if (barsOnly || !Config.ReplaceDesktop) { Recovery.SetDesktop(false); return; }
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
            var desktop = new Desktop(this, monitor, screen.Primary); desktops.Add(desktop); desktop.Show();
        }
        Recovery.SetDesktop(true);
    }
    internal void RebuildMods()
    {
        Plugins.ClosePanel();
        ShellFeatures.Sync(Config, Mods);
        if (featureLayout != FeatureLayout()) { featureLayout = FeatureLayout(); BuildSurfaces(); }
        Tweaks.Reload();
        Mods.ApplyTheme(); BuildDesktops();
        Plugins.Rebuild(); Config.Save();
        if (!ShellFeatures.On(Config, "file-manager")) files?.Close(); else files?.RefreshFeatures();
        foreach (var bar in bars) bar.Tick();
        foreach (var dock in docks) dock.Refresh(true);
    }
    internal void OpenProfiles()
    {
        CloseDesktopMenus();
        if (profiles?.IsVisible == true) { Native.Activate(new WindowInteropHelper(profiles).Handle); return; }
        profiles = new ProfileWindow(this); profiles.Closed += (_, _) => profiles = null; profiles.Show(); Native.Activate(new WindowInteropHelper(profiles).Handle);
    }
    internal void OpenAppMods(WindowEntry window)
    {
        OpenMods(); modCatalog?.ShowAppRules(window);
    }
    internal void OpenMods()
    {
        CloseDesktopMenus();
        if (modCatalog?.IsVisible == true) { Native.Activate(new WindowInteropHelper(modCatalog).Handle); return; }
        modCatalog = new ModCatalog(this); modCatalog.Closed += (_, _) => modCatalog = null; modCatalog.Show(); Native.Activate(new WindowInteropHelper(modCatalog).Handle);
    }
    internal void OpenAppearance()
    {
        CloseDesktopMenus();
        if (appearance?.IsVisible == true) { Native.Activate(new WindowInteropHelper(appearance).Handle); return; }
        appearance = new Appearance(this); appearance.Closed += (_, _) => appearance = null; appearance.Show(); Native.Activate(new WindowInteropHelper(appearance).Handle);
    }
    internal void OpenFiles(string? path = null)
    {
        if (!RequireFeature("file-manager")) return;
        CloseDesktopMenus();
        if (files?.IsVisible == true) { if (path != null) files.Load(path); Native.Activate(new WindowInteropHelper(files).Handle); return; }
        files = new Files(this, path); files.Closed += (_, _) => files = null; files.Show(); Native.Activate(new WindowInteropHelper(files).Handle);
    }
    internal void Launch(string path)
    {
        if (TaskbarFolders.Find(Config, path) is { } folder) { OpenAppFolder(folder, CurrentMonitor()); return; }
        string target = path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? Catalog.Resolve(path) : path;
        if ((Directory.Exists(target) || target.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) || target.Equals(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase)))
        { OpenFiles(Directory.Exists(target) ? target : null); return; }
        if (Native.Launch(path)) HomeLayout.Record(Config, path);
    }
    void Shortcut(uint key)
    {
        switch (key) { case 0x45: OpenFiles(); break; case 0x44: ToggleDesktop(); break; case 0x4E: OpenNotifications(CurrentMonitor()); break; case 0x41: OpenQuick(CurrentMonitor()); break; }
    }
    internal void ToggleDesktop()
    {
        CloseDesktopMenus();
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
        Plugins.UpdateVisibility();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.Bounds.Left + 1, Y = screen.Bounds.Top + 1 }, 2);
            int top = BarLayout.ReservedTop(Config, screen.Primary);
            Recovery.UpdateWorkArea(monitor, HideBars(monitor), Config.AllMonitors && ShellFeatures.On(Config, "multi-monitor") || screen.Primary, top, !ShellFeatures.On(Config, "taskbar") || DockInteraction.Enabled ? 0 : Config.DockHeight);
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
        if (TaskbarFolders.AllApps(Config).Any(p => p.Path.Equals(pin.Path, StringComparison.OrdinalIgnoreCase) || KeysMatch(p.Target, pin.Target))) return;
        Config.Pins.Add(pin); SaveShortcuts();
    }
    internal void Unpin(Pin pin) { if (TaskbarFolders.Find(Config, pin.Path) is { } folder) TaskbarFolders.Unpack(Config, folder); else TaskbarFolders.RemoveApp(Config, pin.Path); SaveShortcuts(); }
    internal void MovePin(Pin pin, int direction)
    {
        int index = Config.Pins.IndexOf(pin), target = index + direction;
        if (index < 0 || target < 0 || target >= Config.Pins.Count) return;
        Config.Pins.RemoveAt(index); Config.Pins.Insert(target, pin); SaveShortcuts();
    }
    internal void SaveShortcuts() { Config.Save(); foreach (var d in docks) d.Refresh(true); ShortcutsChanged?.Invoke(); }
    internal void OpenAppFolder(AppFolder folder, IntPtr monitor) => OpenFlyout(() => new AppFolderPopup(this, folder, monitor));
    internal void ActivateApp(Pin pin)
    {
        var windows = Tracker.Windows.Where(w => w.Matches(pin)).ToList();
        if (windows.Count > 0) Native.Activate(windows[0].Handle); else Launch(pin.Path);
    }
    internal AppFolder? DropTaskbarApps(IReadOnlyList<Pin> sources, Pin? target, string mode)
    {
        if (mode == "folder" && !ShellFeatures.On(Config, "taskbar-folders")) mode = "after";
        AppFolder? created = null, destination = target == null ? null : TaskbarFolders.Find(Config, target.Path);
        foreach (var source in sources) {
            if (source.Path.Equals(target?.Path, StringComparison.OrdinalIgnoreCase)) continue;
            if (mode == "folder" && target != null) {
                if (destination == null) { destination = TaskbarFolders.Create(Config, source, target); created = destination; }
                else TaskbarFolders.Add(Config, destination, source);
            } else TaskbarFolders.ToTaskbar(Config, source, target?.Path, mode == "after");
        }
        SaveShortcuts(); return created;
    }
    internal void ToggleLauncher(IntPtr monitor)
    {
        CancelHomePreparation();
        if (launcher?.IsVisible == true) { launcher.Close(); launcher = null; return; }
        home?.Dismiss(false);
        flyout?.Close();
        launcher = new Launcher(this, monitor);
        launcher.Closed += (_, _) => { launcher = null; Plugins.UpdateVisibility(); foreach (var desktop in desktops) desktop.Tick(); foreach (var bar in bars) bar.Tick(); };
        launcher.Show();
        Plugins.UpdateVisibility(); Plugins.Tick(); foreach (var desktop in desktops) desktop.Tick(); foreach (var bar in bars) bar.Tick();
        Native.Activate(new WindowInteropHelper(launcher).Handle);
        transientOpened = DateTime.UtcNow; transientWatch.Start();
    }
    void CloseTransientWhenExternal()
    {
        if (preparingHome) { var active = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(active, out uint activeOwner); if (active != IntPtr.Zero && activeOwner != Environment.ProcessId) CancelHomePreparation(); }
        if (disposed || (launcher?.IsVisible != true && flyout?.IsVisible != true && home?.IsVisible != true)) { transientWatch.Stop(); return; }
        if (DateTime.UtcNow - transientOpened < TimeSpan.FromMilliseconds(250)) return;
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == IntPtr.Zero) return;
        Native.GetWindowThreadProcessId(foreground, out uint owner);
        if (owner == Environment.ProcessId) return;
        if (flyout is PluginPopup { Pinned: true } pinned && (!Games.IsGame(Native.ProcessPath(owner)) || Native.MonitorFromWindow(foreground, 2) != pinned.MonitorHandle)) return;
        launcher?.Close(); flyout?.Close(); home?.Dismiss(false); transientWatch.Stop();
    }
    internal bool HomeOpenOn(IntPtr monitor) => home?.IsVisible == true && home.MonitorHandle == monitor;
    internal async void OpenHome(IntPtr monitor)
    {
        if (!RequireFeature("desktop-icons")) return;
        int request = ++homeRequest; var previousFocus = Native.GetForegroundWindow(); preparingHome = true;
        List<DesktopLibraryItem> items;
        try
        {
            items = await Task.Run(() => DesktopLibraryModel.Scan());
            var initial = Config.HomeStartTab == "All apps" ? DesktopLibraryModel.AllApps(Catalog.Apps) : Config.HomeStartTab == "Frequently used" ? HomeLayout.Frequent(Config, items.Concat(DesktopLibraryModel.AllApps(Catalog.Apps))) : items;
            var visible = initial.Take(Config.HomeColumns * Config.HomeRows).Where(i => !i.Folder);
            var icons = Task.WhenAll(visible.Select(i => Icons.Get(i.Path)));
            await Task.WhenAny(icons, Task.Delay(250));
        }
        catch (Exception ex) { if (request == homeRequest) preparingHome = false; Log.Write("Home preparation: " + ex); return; }
        if (request != homeRequest || disposed) return;
        preparingHome = false;
        var currentFocus = Native.GetForegroundWindow(); Native.GetWindowThreadProcessId(currentFocus, out uint focusOwner);
        if (Screenshots.Selecting || currentFocus != previousFocus && focusOwner != Environment.ProcessId) return;
        home?.Dismiss(false); launcher?.Close(); flyout?.Close();
        var source = desktops.FirstOrDefault(d => d.MonitorHandle == monitor);
        var anchor = source?.HoldEmblem(); HarborHome next;
        try { next = new HarborHome(this, monitor, anchor, items: items); next.PrepareOpening(); }
        catch { source?.ReleaseEmblem(); throw; }
        home = next;
        next.Closed += (_, _) => { source?.ReleaseEmblem(); if (home == next) home = null; Plugins.UpdateVisibility(); foreach (var desktop in desktops) desktop.Tick(); };
        next.Opened += () => { if (home != next || !next.Ready) return; Plugins.UpdateVisibility(); Plugins.Tick(); foreach (var desktop in desktops) desktop.Tick(); };
        next.Show(); Native.Activate(new WindowInteropHelper(next).Handle); transientOpened = DateTime.UtcNow; transientWatch.Start();
    }
    internal void PreviewIntro() { introPreview?.Dispose(); introPreview = new StartupPresentation(Config, true); }
    internal void OpenHomeSettings() { CloseDesktopMenus(); var existing = Application.Current.Windows.OfType<HomeSettings>().FirstOrDefault(w => w.IsVisible); if (existing != null) { Native.Activate(new WindowInteropHelper(existing).Handle); return; } var editor = new HomeSettings(this); editor.Show(); Native.Activate(new WindowInteropHelper(editor).Handle); }
    internal void OpenSettings()
    {
        CloseDesktopMenus();
        if (settings?.IsVisible == true) { Native.Activate(new WindowInteropHelper(settings).Handle); return; }
        settings = new SettingsWindow(this); settings.Closed += (_, _) => settings = null; settings.Show(); Native.Activate(new WindowInteropHelper(settings).Handle);
    }
    internal void CancelHomePreparation() { homeRequest++; preparingHome = false; }
    void CloseDesktopMenus() { CancelHomePreparation(); home?.Dismiss(false); launcher?.Close(); flyout?.Close(); }
    void OpenFlyout(Func<Window> create, bool keepApps = false)
    {
        if (!keepApps) { CancelHomePreparation(); home?.Dismiss(false); launcher?.Close(); } flyout?.Close();
        var next = create(); flyout = next; next.Closed += (_, _) => { if (flyout == next) flyout = null; }; next.Show(); Native.Activate(new WindowInteropHelper(next).Handle); transientOpened = DateTime.UtcNow; transientWatch.Start();
    }
    internal void OpenQuick(IntPtr monitor) { if (RequireFeature("quick-settings")) OpenFlyout(() => new QuickPanel(this, monitor)); }
    internal void ShowPluginPanel(PluginPopup panel) { if (panel.Pinned) panel.Show(); else OpenFlyout(() => panel, true); }
    internal void DetachPluginPanel(PluginPopup panel) { if (flyout == panel) { flyout = null; transientWatch.Stop(); } }
    internal void OpenPlugin(string id, IntPtr monitor)
    {
        if (Mods.Enabled.FirstOrDefault(m => m.Id == id && m.Kind == "widget") is not { } mod) return;
        if (PluginVisibility.Mode(Mods, mod) == PluginVisibility.Apps && !AppsOpen) ToggleLauncher(monitor);
        if (PluginVisibility.Mode(Mods, mod) == PluginVisibility.Selected && !PluginVisibility.Allowed(this, mod)) Plugins.Customize(mod, monitor); else Plugins.Open(mod, monitor);
    }
    internal void OpenPluginSettings(ModDefinition mod) { Plugins.ClosePanel(); OpenMods(); modCatalog?.SelectPlugin(mod); }
    internal void OpenCalendar(IntPtr monitor) => OpenFlyout(() => new CalendarPanel(monitor));
    internal void OpenArrange(IntPtr monitor) => OpenFlyout(() => new ArrangePanel(this, monitor));
    internal void OpenNotifications(IntPtr monitor) { if (RequireFeature("notification-center")) OpenFlyout(() => new NotificationPanel(this, monitor)); }
    internal void OpenDockControls(IntPtr monitor, Native.Rect bounds) => OpenFlyout(() => new DockControls(this, monitor, bounds));
    internal void OpenTopDockControls(IntPtr monitor) => OpenFlyout(() => new TopDockControls(this, monitor));
    internal void ApplyTopDockPreferences()
    {
        ShellFeatures.Sync(Config, Mods);
        if (featureLayout != FeatureLayout()) { featureLayout = FeatureLayout(); BuildSurfaces(); }
        Plugins.RebuildIfAccessChanged();
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
        Plugins.UpdateVisibility(); Plugins.Tick();
        foreach (var dock in docks) dock.Refresh();
        string ids = string.Join(",", Tracker.Windows.Select(w => w.Handle).OrderBy(h => h.ToInt64()));
        bool changed = lastWindowSet != ids; lastWindowSet = ids;
        if (Config.AutoTile && changed) foreach (var b in bars) if (!HideBars(b.MonitorHandle)) Arrange(b.MonitorHandle, "grid", true);
    }
    void Tick()
    {
        Recovery.Tick();
        // The foreground hook handles app/game switches immediately. Avoid
        // polling widgets, disks and window layouts during the short UI morph.
        if (home?.Transitioning == true) return;
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
        foreach (var desktop in desktops) desktop.Tick();
        Plugins.Tick();
        if (!Games.Active && ShellFeatures.On(Config, "notification-center")) Notifications.Poll();
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
        if (command.StartsWith("files-path:", StringComparison.Ordinal)) { OpenFiles(command[11..]); return "ok"; }
        if (command.StartsWith("plugin:", StringComparison.Ordinal)) { string id = command[7..]; if (!Mods.Enabled.Any(m => m.Id == id && m.Kind == "widget")) return "Plugin is not enabled"; OpenPlugin(id, CurrentMonitor()); return "ok"; }
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
            case "home": OpenHome(CurrentMonitor()); return "ok";
            case "home-settings": OpenHomeSettings(); return "ok";
            case "intro": PreviewIntro(); return "ok";
            case "screenshot": Screenshots.Take(ScreenshotKind.Region); return "ok";
            case "screenshot-all": Screenshots.Take(ScreenshotKind.AllScreens); return "ok";
            case "screenshot-window": Screenshots.Take(ScreenshotKind.ActiveWindow); return "ok";
            case "cancel-screenshot": Screenshots.Dispose(); return "ok";
            case "notifications": OpenNotifications(CurrentMonitor()); return "ok";
            case "refresh-notifications": Notifications.Poll(true); return "ok";
            case "close-panels": home?.Dismiss(false); introPreview?.Dispose(); launcher?.Close(); flyout?.Close(); settings?.Close(); modCatalog?.Close(); foreach (var window in Application.Current.Windows.OfType<Files>().ToArray()) window.Close(); foreach (var window in Application.Current.Windows.OfType<HomeSettings>().ToArray()) window.Close(); appearance?.Close(); return "ok";
            case "desktop": ToggleDesktop(); return "ok";
            case "exit": Application.Current.Dispatcher.BeginInvoke(new Action(Exit)); return "ok";
            case "status": return Status();
            default: return "Unknown command";
        }
    }
    string FeatureLayout() => string.Join(",", Config.FeatureComponents.Where(s => s is "taskbar" or "top-dock" or "multi-monitor").OrderBy(s => s)) + "/" + Config.AllMonitors;
    internal bool RequireFeature(string component) { if (ShellFeatures.On(Config, component)) return true; var mod = Mods.Catalog.First(m => m.Id == ShellFeatures.Id(component)); OpenPluginSettings(mod); return false; }
    internal void CustomizeFeature(ModDefinition mod)
    {
        switch (mod.Component) { case "desktop-icons": OpenAppearance(); break; case "top-dock": case "media-controls": case "dock-clock": case "dock-stats": case "active-title": case "network-control": case "volume-control": case "dock-shortcuts": case "dock-dividers": OpenTopDockControls(CurrentMonitor()); break; case "taskbar": case "taskbar-folders": case "taskbar-dividers": OpenDockControls(); break; case "file-manager": case "file-preview": case "file-thumbnails": case "collection-tabs": OpenFiles(); break; default: OpenSettings(); break; }
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
            ShellSession = new { ShellSession.IsShellProcess, Installed = ShellSession.Enabled, Supervisor = ShellSession.HostRunning, ShellSession.BackupPath },
            GameMode = new { Enabled = Config.HideInGames, Games.Active, Handle = Games.ActiveWindow.ToInt64(), Path = Games.ActivePath },
            NativeTaskbarState = Native.TaskbarState(), ThemeSkin = Theme.Skin, Panels = Application.Current.Windows.Cast<Window>().Where(w => w is Files or HomeSettings or PluginHandle or PluginPopup).Select(w => new { Type = w.GetType().Name, w.IsVisible, w.Opacity }), RecycleBin = new { Count = Harbor.RecycleBin.Query().Items, Open = files?.IsRecycle == true, VisibleItems = files?.IsRecycle == true ? files.RecycledCount : 0 }, HomeCustomization = new { Config.HomeStartTab, Config.HomeColumns, Config.HomeRows, Config.HomeIconSize, Config.HomeTextSize, Config.HomeSidebarWidth, Config.HomeWidth, Config.HomeHeight, Config.HomeCornerRadius, Config.HomeShowLabels, Config.HomeShowKinds, Config.HomeShowCounts, Tabs = HomeLayout.OrderedTabs(Config), Config.HomeHiddenTabs },
            Screenshot = new { Screenshots.Selecting, Screenshots.Status, Screenshots.LastSavedPath },
            Home = new { Open = home?.IsVisible == true, Preparing = preparingHome, Ready = home?.Ready == true, Items = home?.ItemCount ?? 0, Category = home?.CurrentCategory, Source = home?.CurrentSource, LibraryEnabled = Config.DesktopLibraryEnabled, Config.HarborWallpaper, IntroEnabled = Config.StartupIntroEnabled, IntroActive = StartupPresentation.Active, CachedMorph = home?.UsesCachedMorph, SnapshotMilliseconds = home?.SnapshotMilliseconds, FramePacing = home?.FramePacing, Presentation = home?.PresentationState },
            DesktopCount = desktops.Count, DesktopMotion = desktops.Select(d => new { Monitor = Native.Monitor(d.MonitorHandle).Device, Animation = d.AnimationStatus }), FilesOpen = files?.IsVisible == true, FilesFolder = files?.CurrentFolder, ModsOpen = modCatalog?.IsVisible == true,
            AppearanceOpen = appearance?.IsVisible == true, BuilderOpen = Application.Current.Windows.OfType<ModDesigner>().Any(w => w.IsVisible),
            ProfilesOpen = profiles?.IsVisible == true, ProfileCount = Config.Profiles.Count, BehaviorMods = Tweaks.Summary(),
            WindowSpaceAdjustments = WindowSpace.Adjustments,
            DockInteraction = DockInteraction.Status(),
            Taskbar = new { Config.TaskbarShowRunningApps, Surfaces = docks.Select(d => new { Monitor = Native.Monitor(d.MonitorHandle).Device, d.RunningAppCount }) },
            TopDock = bars.Select(b => new { Monitor = Native.Monitor(b.MonitorHandle).Device, Pieces = b.PlacementPanel.Slots.Count }),
            WindowFrames = Tracker.Windows.Select(w => { Native.GetWindowRect(w.Handle, out var outer); var frame = Native.VisibleBounds(w.Handle, outer); return new { Handle = w.Handle.ToInt64(), w.ProcessName, w.Title, Maximized = Native.IsZoomed(w.Handle), Minimized = Native.IsIconic(w.Handle), Monitor = Native.Monitor(w.Monitor).Device, Frame = new { frame.Left, frame.Top, frame.Right, frame.Bottom } }; }),
            WindowGroups = Tracker.Windows.GroupBy(w => w.Key, StringComparer.OrdinalIgnoreCase).Select(g => new { Key = g.Key, AppId = g.First().AppId, Name = g.First().ProcessName, Count = g.Count() }),
            Desktops = desktops.Select(d => new { Handle = d.Handle.ToInt64(), d.Healthy, d.Independent, Parent = Native.GetParent(d.Handle).ToInt64(), Monitor = Native.Monitor(d.MonitorHandle).Device }),
            ModCount = Mods.Catalog.Count, EnabledMods = Mods.Enabled.Select(m => m.Id), Notifications = new { Count = Notifications.Items.Count, Notifications.SyncStatus },
            ReplaceTaskbar = Config.ReplaceTaskbar, RecoveryActive = File.Exists(Path.Combine(Settings.DataDir, "recovery.json")),
            PluginAccess = new { Controls = Plugins.HandleCount, Plugins.VisibleHandleCount, BackgroundTools = Plugins.BackgroundCount, Plugins.OpenTool, Plugins.AlwaysOnCount, Plugins.VisibleAlwaysOnCount, Modes = Mods.Enabled.Where(m => m.Kind == "widget").Select(m => new { m.Id, Mode = PluginPresentation.Mode(Mods, m), Snap = PluginSnapping.Selected(Mods, m), AlwaysOn = Mods.Value(m, "always-on", "No") == "Yes", Visibility = PluginVisibility.Mode(Mods, m), RuleMatched = PluginVisibility.Allowed(this, m), ChosenAppCount = PluginVisibility.Rules(Mods, m).Length }) },
            Features = Config.FeatureComponents.OrderBy(s => s),
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
        if (ShellSession.IsShellProcess && !ShellSession.HostRunning) ShellSession.EnsureExplorer();
        // All shell state is restored before ending the process and its input hook.
        Environment.Exit(0);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        CancelHomePreparation();
        Log.Write("Exiting Harbor.");
        timer?.Stop();
        transientWatch.Stop();
        if (foregroundHook != IntPtr.Zero) Native.UnhookWinEvent(foregroundHook);
        if (taskbarHook != IntPtr.Zero) Native.UnhookWinEvent(taskbarHook);
        keyboard?.Dispose(); keyboard = null;
        Screenshots.Dispose();
        DockInteraction.Dispose();
        Plugins.Dispose();
        home?.Dismiss(false); introPreview?.Dispose();
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
