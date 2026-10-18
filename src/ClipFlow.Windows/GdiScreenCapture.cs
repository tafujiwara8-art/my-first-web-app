using System.ComponentModel;
using System.Runtime.InteropServices;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

/// <summary>
/// GDI（<c>GetDC(NULL)</c> + <c>BitBlt</c>）による画面取得。プロセスが Per-Monitor V2 DPI 対応であることを前提に、
/// 座標は仮想スクリーンの物理ピクセルで扱う。DRM 保護コンテンツは黒く写ることがある。
/// </summary>
public sealed class GdiScreenCapture : IScreenCapture
{
    [ThreadStatic]
    private static List<MonitorInfo>? _enumTarget;

    public unsafe IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();
        _enumTarget = list;
        try
        {
            EnumDisplayMonitors(0, 0, &MonitorEnumProc, 0);
        }
        finally
        {
            _enumTarget = null;
        }

        return list;
    }

    public RawImage Capture(PixelRect regionInPhysicalPixels)
    {
        var r = regionInPhysicalPixels.Normalize();
        if (r.IsEmpty)
        {
            return new RawImage([], 0, 0);
        }

        var screenDc = GetDC(0);
        if (screenDc == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        nint memDc = 0, bitmap = 0, old = 0;
        try
        {
            memDc = CreateCompatibleDC(screenDc);
            var header = new BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = r.Width,
                biHeight = -r.Height, // 負 = トップダウン
                biPlanes = 1,
                biBitCount = 32,
                biCompression = BI_RGB,
            };
            bitmap = CreateDIBSection(screenDc, ref header, DIB_RGB_COLORS, out var bits, 0, 0);
            if (bitmap == 0 || bits == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            old = SelectObject(memDc, bitmap);
            if (!BitBlt(memDc, 0, 0, r.Width, r.Height, screenDc, r.X, r.Y, SRCCOPY | CAPTUREBLT))
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            var pixels = new byte[r.Width * r.Height * RawImage.BytesPerPixel];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255; // GDI のアルファは未定義なので不透明にする
            }

            return new RawImage(pixels, r.Width, r.Height);
        }
        finally
        {
            if (old != 0)
            {
                SelectObject(memDc, old);
            }

            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            if (memDc != 0)
            {
                DeleteDC(memDc);
            }

            _ = ReleaseDC(0, screenDc);
        }
    }

    [UnmanagedCallersOnly]
    private static unsafe int MonitorEnumProc(nint hMonitor, nint hdc, RECT* rect, nint data)
    {
        var info = new MONITORINFOEXW { cbSize = (uint)sizeof(MONITORINFOEXW) };
        if (!GetMonitorInfo(hMonitor, ref info))
        {
            return 1;
        }

        var scale = GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 && dpiX > 0 ? dpiX / 96.0 : 1.0;
        var name = new string(info.szDevice);
        _enumTarget?.Add(new MonitorInfo(
            name.TrimEnd('\0'),
            ToPixelRect(info.rcMonitor),
            ToPixelRect(info.rcWork),
            scale,
            (info.dwFlags & MONITORINFOF_PRIMARY) != 0));
        return 1;
    }

    private static PixelRect ToPixelRect(RECT r) => PixelRect.FromEdges(r.left, r.top, r.right, r.bottom);
}
