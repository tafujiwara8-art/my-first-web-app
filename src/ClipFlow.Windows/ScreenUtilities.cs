using ClipFlow.Core.Imaging;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

public static class ScreenUtilities
{
    /// <summary>カーソル位置（仮想スクリーンの物理ピクセル）。</summary>
    public static PixelPoint GetCursorPosition() =>
        GetCursorPos(out var p) ? new PixelPoint(p.x, p.y) : default;

    /// <summary>ウィンドウを物理ピクセルの矩形へ配置する（Z オーダー・アクティブ状態は変えない）。</summary>
    public static void SetWindowBoundsPhysical(nint hwnd, PixelRect bounds) =>
        SetWindowPos(hwnd, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, SWP_NOZORDER | SWP_NOACTIVATE);

    /// <summary>DWM の次の合成が終わるまで待つ（ウィンドウを隠した直後の画面取得に使う）。</summary>
    public static bool WaitForComposition() => DwmFlush() == 0;
}
