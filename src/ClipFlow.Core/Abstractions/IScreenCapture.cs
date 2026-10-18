using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Abstractions;

/// <summary>モニター情報。座標はすべて仮想スクリーン上の物理ピクセル。</summary>
/// <param name="Bounds">モニター全体。</param>
/// <param name="WorkArea">タスクバーを除いた作業領域。</param>
/// <param name="Scale">DPI 倍率（1.0 = 96 DPI = 100%）。</param>
public sealed record MonitorInfo(string DeviceName, PixelRect Bounds, PixelRect WorkArea, double Scale, bool IsPrimary);

/// <summary>画面取得。P1 は GDI 実装、P5 で Windows.Graphics.Capture 実装に差し替え可能にする。</summary>
public interface IScreenCapture
{
    IReadOnlyList<MonitorInfo> GetMonitors();

    /// <summary>仮想スクリーン座標（物理ピクセル）の矩形を取得する。複数モニターにまたがってよい。</summary>
    RawImage Capture(PixelRect regionInPhysicalPixels);
}
