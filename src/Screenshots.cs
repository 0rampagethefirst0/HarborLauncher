using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Harbor;

internal enum ScreenshotKind { Region, AllScreens, ActiveWindow }

// Shell-owned capture shortcuts continue to work with Explorer fully replaced.
internal sealed class Screenshots : IDisposable
{
    readonly Shell shell;
    CaptureSelection? selection;
    bool busy;
    internal string? LastSavedPath { get; private set; }
    internal string Status { get; private set; } = "Ready";
    internal bool Selecting => selection != null;
    internal Screenshots(Shell shell) => this.shell = shell;
    internal static bool Shortcut(uint key, bool windows, bool shift, bool control, bool alt, out ScreenshotKind mode)
    {
        mode = ScreenshotKind.Region;
        if (key == 0x53 && windows && shift && !control && !alt) return true;
        if (key != 0x2C || control) return false;
        mode = alt ? ScreenshotKind.ActiveWindow : windows ? ScreenshotKind.AllScreens : ScreenshotKind.Region; return true;
    }
    internal void Take(ScreenshotKind kind)
    {
        if (busy || selection != null) return;
        shell.CancelHomePreparation();
        try
        {
            var desktop = System.Windows.Forms.SystemInformation.VirtualScreen;
            var bounds = new Int32Rect(desktop.Left, desktop.Top, desktop.Width, desktop.Height);
            if (kind == ScreenshotKind.ActiveWindow)
            {
                Native.GetWindowRect(Native.GetForegroundWindow(), out var window);
                bounds = Intersect(bounds, new Int32Rect(window.Left, window.Top, Math.Max(0, window.Width), Math.Max(0, window.Height)));
            }
            if (bounds.Width < 1 || bounds.Height < 1) throw new InvalidOperationException("The selected window has no visible area.");
            var image = Capture(bounds);
            if (kind != ScreenshotKind.Region) { Complete(image); return; }
            Status = "Select an area · Esc cancels";
            selection = new CaptureSelection(image, bounds, crop => { selection = null; if (crop != null) Complete(crop); else Status = "Cancelled"; }); selection.Show();
        }
        catch (Exception ex) { Status = "Capture failed: " + ex.Message; shell.Notifications.Add("Screenshot unavailable", ex.Message); Log.Write("Screenshot: " + ex); }
    }
    [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] static extern bool BitBlt(IntPtr destination, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    static BitmapSource Capture(Int32Rect rect)
    {
        var desktop = GetDC(IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try { return CaptureDevice(desktop, rect); }
        finally { ReleaseDC(IntPtr.Zero, desktop); }
    }
    internal static BitmapSource CaptureDevice(IntPtr source, Int32Rect rect)
    {
        using var buffer = new CaptureBuffer(source, rect.Width, rect.Height);
        // Native BitBlt accepts SRCCOPY | CAPTUREBLT. The GDI+ enum validator
        // rejects that combination before any screen pixels are captured.
        if (!BitBlt(buffer.Device, 0, 0, rect.Width, rect.Height, source, rect.X, rect.Y, 0x40CC0020)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return buffer.Image();
    }
    internal static Int32Rect Intersect(Int32Rect a, Int32Rect b)
    {
        int x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y), right = Math.Min(a.X + a.Width, b.X + b.Width), bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return new Int32Rect(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }
    internal static BitmapSource Crop(BitmapSource source, Int32Rect area)
    {
        area = Intersect(new Int32Rect(0, 0, source.PixelWidth, source.PixelHeight), area);
        if (area.Width < 1 || area.Height < 1) throw new ArgumentException("Choose an area at least one pixel wide and high.");
        var crop = new CroppedBitmap(source, area); crop.Freeze(); return crop;
    }
    internal static string Save(BitmapSource image, string folder)
    {
        Directory.CreateDirectory(folder); string path = Path.Combine(folder, "Harbor " + DateTime.Now.ToString("yyyy-MM-dd HHmmss-fff") + ".png");
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); encoder.Save(stream); return path;
    }
    void Complete(BitmapSource image)
    {
        busy = true; bool saved = false; string? error = null;
        try { LastSavedPath = Save(image, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots")); saved = true; }
        catch (Exception ex) { error = ex.Message; Log.Write("Screenshot save: " + error); }
        int attempts = 0; var retry = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        retry.Tick += (_, _) =>
        {
            bool copied = false; try { Clipboard.SetImage(image); copied = true; } catch (ExternalException) { }
            if (!copied && ++attempts < 8) return;
            retry.Stop(); busy = false;
            Status = copied ? saved ? "Copied and saved" : "Copied · could not save: " + error : saved ? "Saved · clipboard is busy" : "Could not copy or save";
            shell.Notifications.Add("Screenshot", Status + (saved ? "\n" + LastSavedPath : ""));
            CaptureToast.Show(Status);
        };
        retry.Start();
    }
    public void Dispose() { selection?.Close(); selection = null; }
}

internal sealed class CaptureSelection : Window
{
    readonly BitmapSource image;
    readonly Int32Rect screen;
    readonly Action<BitmapSource?> finished;
    readonly Canvas canvas = new();
    readonly Border border = new() { BorderBrush = Theme.Brush("#68BDFF"), BorderThickness = new Thickness(2), Background = Theme.Brush("#14519AFF"), IsHitTestVisible = false };
    readonly RectangleGeometry hole = new();
    readonly GeometryDrawing dim;
    Point start;
    bool dragging, submitted;
    internal CaptureSelection(BitmapSource image, Int32Rect screen, Action<BitmapSource?> finished)
    {
        this.image = image; this.screen = screen; this.finished = finished; Title = "Harbor — screenshot";
        var monitor = Native.MonitorFromPoint(new Native.Point { X = screen.X + 1, Y = screen.Y + 1 }, 2); double dpi = Native.Scale(monitor);
        Width = screen.Width / dpi; Height = screen.Height / dpi; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false; Background = Brushes.Black; Cursor = Cursors.Cross;
        var root = new Grid(); root.Children.Add(new Image { Source = image, Stretch = Stretch.Fill });
        var outside = new RectangleGeometry(new Rect(0, 0, Width, Height)); dim = new GeometryDrawing(Theme.Brush("#82030913"), null, new CombinedGeometry(GeometryCombineMode.Exclude, outside, hole));
        var shade = new Border { Background = new DrawingBrush(dim) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top }, IsHitTestVisible = false }; root.Children.Add(shade);
        canvas.Children.Add(border); root.Children.Add(canvas);
        var hint = new Border { Background = Theme.Brush("#F0152438"), BorderBrush = Theme.Brush("#365776"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Padding = new Thickness(20, 13, 20, 13), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(20) };
        hint.Child = Theme.Label("Drag to capture an area   ·   Esc or right-click to cancel", 16); root.Children.Add(hint); Content = root;
        SourceInitialized += (_, _) => { Native.ToolWindow(this); Native.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), screen.X, screen.Y, screen.Width, screen.Height, 0x40); };
        PreviewMouseLeftButtonDown += (_, e) => { start = e.GetPosition(root); dragging = true; CaptureMouse(); Draw(start); e.Handled = true; };
        PreviewMouseMove += (_, e) => { if (dragging) Draw(e.GetPosition(root)); };
        PreviewMouseLeftButtonUp += (_, e) =>
        {
            if (!dragging) return; dragging = false; ReleaseMouseCapture(); var end = e.GetPosition(root); var rect = new Rect(start, end);
            if (rect.Width < 3 || rect.Height < 3) { hole.Rect = Rect.Empty; border.Width = border.Height = 0; return; }
            // Convert through the complete virtual desktop, including monitors
            // to the left of the primary display and mixed scaling factors.
            var crop = new Int32Rect((int)Math.Floor(rect.Left / root.ActualWidth * image.PixelWidth), (int)Math.Floor(rect.Top / root.ActualHeight * image.PixelHeight), Math.Max(1, (int)Math.Round(rect.Width / root.ActualWidth * image.PixelWidth)), Math.Max(1, (int)Math.Round(rect.Height / root.ActualHeight * image.PixelHeight)));
            Submit(Screenshots.Crop(image, crop)); e.Handled = true;
        };
        PreviewMouseRightButtonDown += (_, e) => { Submit(null); e.Handled = true; };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Submit(null); e.Handled = true; } };
        Closed += (_, _) => { ReleaseMouseCapture(); if (!submitted) { submitted = true; finished(null); } };
        void Draw(Point end) { var rect = new Rect(start, end); hole.Rect = rect; Canvas.SetLeft(border, rect.Left); Canvas.SetTop(border, rect.Top); border.Width = rect.Width; border.Height = rect.Height; }
    }
    void Submit(BitmapSource? crop) { if (submitted) return; submitted = true; Close(); finished(crop); }
}

internal static class CaptureToast
{
    internal static void Show(string text)
    {
        var work = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var monitor = Native.MonitorFromPoint(new Native.Point { X = work.Left + 1, Y = work.Top + 1 }, 2); double scale = Native.Scale(monitor);
        var window = new Window { Title = "Harbor — screenshot saved", WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Background = Brushes.Transparent, Topmost = true, ShowActivated = false, ShowInTaskbar = false, Width = 360, Height = 94 };
        var body = new StackPanel(); body.Children.Add(Theme.Label("Screenshot", 14, Theme.Accent)); var label = Theme.Label(text, 13); label.Margin = new Thickness(0, 8, 0, 0); body.Children.Add(label);
        window.Content = new Border { Background = Theme.Brush("#F0152438"), CornerRadius = new CornerRadius(12), BorderBrush = Theme.Brush("#365776"), BorderThickness = new Thickness(1), Padding = new Thickness(20, 15, 20, 15), Child = body };
        window.SourceInitialized += (_, _) => { Native.ToolWindow(window); Native.SetWindowPos(new WindowInteropHelper(window).Handle, new IntPtr(-1), work.Right - (int)(384 * scale), work.Bottom - (int)(118 * scale), (int)(360 * scale), (int)(94 * scale), 0x50); };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) }; timer.Tick += (_, _) => { timer.Stop(); window.Close(); }; window.Closed += (_, _) => timer.Stop(); window.Loaded += (_, _) => Motion.Enter((FrameworkElement)window.Content); window.Show(); timer.Start();
    }
}
