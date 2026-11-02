using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Harbor;

internal sealed class Desktop : Window
{
    readonly Shell shell;
    internal readonly IntPtr MonitorHandle;
    readonly Canvas canvas = new();
    readonly List<Widget> widgets = new();
    readonly WrapPanel icons = new() { Orientation = Orientation.Vertical, Margin = new Thickness(16, 52, 0, 92) };
    readonly DesktopPortal? portal;
    readonly DesktopBin bin;
    string iconSignature = "";
    IntPtr parent;
    bool independent;
    internal bool Healthy => Native.IsWindow(Handle) && (independent || parent != IntPtr.Zero && Native.IsWindow(parent));
    internal bool Independent => independent;
    internal object? AnimationStatus => portal?.AnimationStatus;
    internal EmblemState? HoldEmblem() => portal?.Hold();
    internal void ReleaseEmblem() => portal?.Release();
    internal IntPtr Handle => new WindowInteropHelper(this).Handle;
    internal Desktop(Shell shell, IntPtr monitor, bool primary)
    {
        this.shell = shell; MonitorHandle = monitor; Title = "Harbor — desktop";
        var info = Native.Monitor(monitor); double scale = Native.Scale(monitor);
        icons.Margin = new Thickness(16, shell.Config.TopBarHeight + 14, 0, shell.Config.DockHeight + 12);
        Width = info.Monitor.Width / scale; Height = info.Monitor.Height / scale;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false; ShowActivated = false; Background = Theme.Bg;
        var root = new Grid();
        if (shell.Config.HarborWallpaper || Theme.Dragon) root.Background = HarborBackdrop.Brush(info.Monitor.Width, info.Monitor.Height);
        else try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"); string path = key?.GetValue("WallPaper") as string ?? "";
            if (!File.Exists(path)) path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Themes\TranscodedWallpaper");
            if (File.Exists(path)) { var image = new BitmapImage(); image.BeginInit(); image.UriSource = new Uri(path); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = info.Monitor.Width; image.EndInit(); image.Freeze(); root.Background = new ImageBrush(image) { Stretch = Stretch.UniformToFill }; }
        }
        catch (Exception ex) { Log.Write("Desktop wallpaper: " + ex.Message); }
        if (ShellFeatures.On(shell.Config, "desktop-icons")) {
            if (shell.Config.DesktopLibraryEnabled) { portal = new DesktopPortal(shell, monitor); root.Children.Add(portal); }
            else if (primary) root.Children.Add(icons);
        }
        root.Children.Add(canvas); bin = new DesktopBin(shell); root.Children.Add(bin); Content = root;
        foreach (var mod in shell.Mods.Enabled.Where(m => PluginPresentation.IsDesktop(shell.Mods, m)).ToArray())
        {
            string screen = shell.Mods.Value(mod, "screen", "primary"); if (screen == "primary" && !primary || screen == "secondary" && primary) continue;
            var widget = new Widget(shell, mod); widgets.Add(widget); canvas.Children.Add(widget.View);
            widget.View.Visibility = shell.Plugins.Allowed(mod, monitor) ? Visibility.Visible : Visibility.Collapsed;
            widget.View.MaxHeight = Math.Max(120, Height - shell.Config.TopBarHeight - shell.Config.DockHeight - 24);
            string xKey = "x-" + info.Device[^1], yKey = "y-" + info.Device[^1];
            double x = shell.Mods.Number(mod, xKey, Math.Max(16, Width - widget.View.Width - 24), 0, Math.Max(0, Width - widget.View.Width));
            double minY = shell.Config.TopBarHeight + 10;
            double y = shell.Mods.Number(mod, yKey, shell.Config.TopBarHeight + 26 + (widgets.Count - 1) * 190, minY, Math.Max(minY, Height - shell.Config.DockHeight - 80));
            Canvas.SetLeft(widget.View, x); Canvas.SetTop(widget.View, y);
            widget.Handle.DragDelta += (_, e) => { x = Math.Clamp(x + e.HorizontalChange, 0, Math.Max(0, ActualWidth - widget.View.ActualWidth)); y = Math.Clamp(y + e.VerticalChange, minY, Math.Max(minY, ActualHeight - widget.View.ActualHeight - shell.Config.DockHeight)); Canvas.SetLeft(widget.View, x); Canvas.SetTop(widget.View, y); };
            widget.Handle.DragCompleted += (_, _) => { shell.Mods.Set(mod, xKey, x.ToString(System.Globalization.CultureInfo.InvariantCulture), false); shell.Mods.Set(mod, yKey, y.ToString(System.Globalization.CultureInfo.InvariantCulture), false); };
        }
        var menu = new ContextMenu(); menu.Items.Add(Theme.Item("Harbor Home", () => shell.OpenHome(MonitorHandle))); menu.Items.Add(Theme.Item("Desktop appearance", shell.OpenAppearance)); menu.Items.Add(Theme.Item("Take a screenshot", () => Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, new Action(() => shell.Screenshots.Take(ScreenshotKind.Region))))); menu.Items.Add(Theme.Item("Open files", () => shell.OpenFiles())); menu.Items.Add(Theme.Item("Refresh desktop", () => RefreshIcons(true))); menu.Items.Add(Theme.Item("Mods", shell.OpenMods)); menu.Items.Add(Theme.Item("Settings", shell.OpenSettings)); menu.Items.Add(new Separator()); menu.Items.Add(Theme.Item("Exit Harbor and restore Windows", shell.Exit)); root.ContextMenu = menu;
        SourceInitialized += (_, _) => Attach();
        Loaded += (_, _) => RefreshIcons(true);
        Closed += (_, _) => { foreach (var widget in widgets) widget.Flush(); };
    }
    void Attach()
    {
        if (ShellSession.IsShellProcess) { AttachIndependent(); return; }
        var nativeIcons = Native.DesktopIconWindows().FirstOrDefault(); parent = Native.GetParent(nativeIcons);
        if (parent == IntPtr.Zero) { AttachIndependent(); return; }
        var handle = new WindowInteropHelper(this).Handle;
        Native.ToolWindow(this);
        long style = (Native.GetWindowLong(handle, -16).ToInt64() | 0x40000000L) & ~0x80000000L;
        Native.SetWindowLong(handle, -16, new IntPtr(style)); Native.SetParent(handle, parent);
        var monitor = Native.Monitor(MonitorHandle).Monitor;
        var point = new Native.Point { X = monitor.Left, Y = monitor.Top }; Native.ScreenToClient(parent, ref point);
        Native.SetWindowPos(handle, IntPtr.Zero, point.X, point.Y, monitor.Width, monitor.Height, 0x14);
    }
    void AttachIndependent()
    {
        independent = true; Native.ToolWindow(this);
        var rect = Native.Monitor(MonitorHandle).Monitor;
        Native.SetWindowPos(Handle, new IntPtr(1), rect.Left, rect.Top, rect.Width, rect.Height, 0x50);
        // Desktop interaction must never raise this background over normal apps.
        HwndSource.FromHwnd(Handle)?.AddHook(KeepBehindApps);
    }
    IntPtr KeepBehindApps(IntPtr window, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == 0x46 && independent)
        {
            var position = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.WindowPosition>(l);
            position.After = new IntPtr(1);
            position.Flags &= ~4u;
            position.Flags |= 0x10;
            System.Runtime.InteropServices.Marshal.StructureToPtr(position, l, false);
        }
        return IntPtr.Zero;
    }
    internal void Tick()
    {
        foreach (var widget in widgets) { var mod = (ModDefinition)widget.View.Tag; ElementFade.Visible(widget.View, shell.Plugins.Allowed(mod, MonitorHandle)); widget.Tick(); }
        portal?.Tick(shell.HomeOpenOn(MonitorHandle));
        bin.Tick();
        RefreshIcons();
    }
    void RefreshIcons(bool force = false)
    {
        if (icons.Parent == null) return;
        string[] paths;
        try { paths = new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) }.SelectMany(dir => Directory.EnumerateFileSystemEntries(dir)).Where(p => (File.GetAttributes(p) & FileAttributes.Hidden) == 0).OrderBy(p => p).Take(150).ToArray(); }
        catch { return; }
        string signature = string.Join('|', paths); if (!force && signature == iconSignature) return; iconSignature = signature;
        icons.Children.Clear();
        Add("Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        foreach (string path in paths) Add(Path.GetFileNameWithoutExtension(path), path);
        void Add(string name, string path)
        {
            var content = new StackPanel { Width = 80 }; var icon = Theme.AppIcon(path, name, 34); icon.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(icon); content.Children.Add(new TextBlock { Text = name, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, FontSize = 11, MaxHeight = 34, Margin = new Thickness(0, 6, 0, 0), Foreground = Brushes.White });
            var button = Theme.Button(content, "Open " + name, () => shell.Launch(path)); button.Width = 94; button.Height = 92; button.Padding = new Thickness(6); button.Margin = new Thickness(0, 0, 4, 4);
            var context = new ContextMenu(); context.Items.Add(Theme.Item("Open", () => shell.Launch(path))); context.Items.Add(Theme.Item("Show in Harbor Files", () => shell.OpenFiles(Directory.Exists(path) ? path : Path.GetDirectoryName(path)))); button.ContextMenu = context; icons.Children.Add(button);
        }
    }
}
