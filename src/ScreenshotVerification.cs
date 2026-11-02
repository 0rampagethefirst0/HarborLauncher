using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

internal static class ScreenshotVerification
{
    internal static void Run(string directory, Action<string, bool, object?> check)
    {
        check("Win+Shift+S opens Harbor region capture without Explorer", Screenshots.Shortcut(0x53, true, true, false, false, out var region) && region == ScreenshotKind.Region, null);
        check("Print Screen selects a region and Win+Print Screen captures all monitors", Screenshots.Shortcut(0x2C, false, false, false, false, out var print) && print == ScreenshotKind.Region && Screenshots.Shortcut(0x2C, true, false, false, false, out var all) && all == ScreenshotKind.AllScreens, null);
        check("Alt+Print Screen captures the focused window and other app shortcuts pass through", Screenshots.Shortcut(0x2C, false, false, false, true, out var window) && window == ScreenshotKind.ActiveWindow && !Screenshots.Shortcut(0x53, true, false, false, false, out _) && !Screenshots.Shortcut(0x53, true, true, true, false, out _), null);
        var intersection = Screenshots.Intersect(new Int32Rect(-1920, 0, 4480, 1440), new Int32Rect(-2000, -20, 500, 400));
        check("Screenshot bounds include negative monitor positions and clip at the desktop edge", intersection == new Int32Rect(-1920, 0, 420, 380), intersection);
        byte[] pixels = new byte[32 * 24 * 4]; for (int y = 0; y < 24; y++) for (int x = 0; x < 32; x++) { int p = (y * 32 + x) * 4; pixels[p] = (byte)(x * 7); pixels[p + 1] = (byte)(y * 9); pixels[p + 2] = 100; pixels[p + 3] = 255; }
        var fixture = BitmapSource.Create(32, 24, 96, 96, PixelFormats.Bgra32, null, pixels, 32 * 4); fixture.Freeze(); var cropped = Screenshots.Crop(fixture, new Int32Rect(8, 6, 12, 10));
        string path = Screenshots.Save(cropped, Path.Combine(directory, "screenshot-fixture")); var reopened = new BitmapImage(new Uri(path)); byte[] first = new byte[4]; reopened.CopyPixels(new Int32Rect(0, 0, 1, 1), first, 4, 0);
        check("A captured region saves as a real PNG with the selected pixels and dimensions", reopened.PixelWidth == 12 && reopened.PixelHeight == 10 && first[0] == 56 && first[1] == 54 && first[2] == 100 && File.ReadAllBytes(path)[1] == 80, path);
        var clamped = Screenshots.Crop(fixture, new Int32Rect(25, 19, 20, 20)); check("Selections extending past a monitor are cropped safely", clamped.PixelWidth == 7 && clamped.PixelHeight == 5, null);
        using var nativeFixture = new CaptureBuffer(IntPtr.Zero, 32, 24); byte[] nativePixels = new byte[32 * 24 * 4];
        for (int p = 0; p < nativePixels.Length; p += 4) { nativePixels[p] = 143; nativePixels[p + 1] = 72; nativePixels[p + 2] = 19; nativePixels[p + 3] = 255; }
        System.Runtime.InteropServices.Marshal.Copy(nativePixels, 0, nativeFixture.Pixels, nativePixels.Length);
        var captured = Screenshots.CaptureDevice(nativeFixture.Device, new Int32Rect(2, 3, 12, 10)); byte[] sample = new byte[4]; captured.CopyPixels(new Int32Rect(0, 0, 1, 1), sample, 4, 0);
        check("The real native capture call accepts layered-window flags and copies pixels", captured.PixelWidth == 12 && captured.PixelHeight == 10 && sample[0] == 143 && sample[1] == 72 && sample[2] == 19, new { captured.PixelWidth, captured.PixelHeight, Format = captured.Format.ToString(), Pixels = sample });
    }
}
