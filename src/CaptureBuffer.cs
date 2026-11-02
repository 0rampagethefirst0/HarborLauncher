using System;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Harbor;

// A native RGB surface avoids GDI+ temporary-DC and alpha conversions.
internal sealed class CaptureBuffer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct BitmapHeader
    {
        internal uint Size; internal int Width, Height; internal ushort Planes, Bits;
        internal uint Compression, ImageBytes; internal int XResolution, YResolution; internal uint Colors, ImportantColors;
    }
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr source);
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateDIBSection(IntPtr device, ref BitmapHeader info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr SelectObject(IntPtr device, IntPtr value);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr device);
    internal IntPtr Device { get; private set; }
    internal IntPtr Pixels { get; }
    readonly IntPtr bitmap, previous;
    readonly int width, height;
    internal CaptureBuffer(IntPtr compatible, int width, int height)
    {
        this.width = width; this.height = height;
        Device = CreateCompatibleDC(compatible); if (Device == IntPtr.Zero) throw Error();
        var header = new BitmapHeader { Size = 40, Width = width, Height = -height, Planes = 1, Bits = 32, ImageBytes = checked((uint)(width * height * 4)) };
        bitmap = CreateDIBSection(Device, ref header, 0, out var pixels, IntPtr.Zero, 0); Pixels = pixels;
        if (bitmap == IntPtr.Zero || Pixels == IntPtr.Zero) { var error = Error(); Dispose(); throw error; }
        previous = SelectObject(Device, bitmap); if (previous == IntPtr.Zero || previous == new IntPtr(-1)) { var error = Error(); Dispose(); throw error; }
    }
    internal BitmapSource Image()
    {
        int stride = checked(width * 4); var bytes = new byte[checked(stride * height)]; Marshal.Copy(Pixels, bytes, 0, bytes.Length);
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, bytes, stride); image.Freeze(); return image;
    }
    static Exception Error() => new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    public void Dispose()
    {
        if (Device == IntPtr.Zero) return;
        if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(Device, previous);
        if (bitmap != IntPtr.Zero) DeleteObject(bitmap); DeleteDC(Device); Device = IntPtr.Zero;
    }
}
