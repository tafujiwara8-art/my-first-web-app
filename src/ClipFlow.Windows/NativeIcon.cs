using System.ComponentModel;
using System.Runtime.InteropServices;
using ClipFlow.Core.Imaging;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

/// <summary>BGRA 画像から作る HICON。</summary>
public sealed class NativeIcon : IDisposable
{
    private NativeIcon(nint handle) => Handle = handle;

    public nint Handle { get; private set; }

    public static NativeIcon FromImage(RawImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = image.Width,
            biHeight = -image.Height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = BI_RGB,
        };
        var color = CreateDIBSection(0, ref header, DIB_RGB_COLORS, out var bits, 0, 0);
        if (color == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var mask = CreateBitmap(image.Width, image.Height, 1, 1, 0);
        try
        {
            Marshal.Copy(image.Pixels, 0, bits, image.Pixels.Length);
            var info = new ICONINFO { fIcon = 1, hbmMask = mask, hbmColor = color };
            var icon = CreateIconIndirect(ref info);
            return icon != 0 ? new NativeIcon(icon) : throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        finally
        {
            DeleteObject(color);
            DeleteObject(mask);
        }
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            DestroyIcon(Handle);
            Handle = 0;
        }
    }
}
