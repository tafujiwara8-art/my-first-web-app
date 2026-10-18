using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;

namespace ClipFlow.Windows;

/// <summary>WPF の論理座標（1/96 インチ）。</summary>
public readonly record struct LogicalRect(double X, double Y, double Width, double Height);

public readonly record struct LogicalPoint(double X, double Y);

/// <summary>
/// 物理ピクセル ⇄ 論理座標の変換。Per-Monitor V2 では倍率がモニターごとに異なるため、
/// 変換は常に「どのモニターの倍率で」「どの原点から」かを明示して行う。
/// </summary>
public static class DpiConverter
{
    public static LogicalPoint ToLogical(PixelPoint physical, double scale, PixelPoint origin = default) =>
        new((physical.X - origin.X) / scale, (physical.Y - origin.Y) / scale);

    public static PixelPoint ToPhysical(LogicalPoint logical, double scale, PixelPoint origin = default) =>
        new((int)Math.Round(logical.X * scale) + origin.X, (int)Math.Round(logical.Y * scale) + origin.Y);

    public static LogicalRect ToLogical(PixelRect physical, double scale, PixelPoint origin = default)
    {
        ValidateScale(scale);
        var topLeft = ToLogical(new PixelPoint(physical.X, physical.Y), scale, origin);
        return new LogicalRect(topLeft.X, topLeft.Y, physical.Width / scale, physical.Height / scale);
    }

    /// <summary>辺ごとに丸めるため、隣接する矩形の間に隙間・重なりができない。</summary>
    public static PixelRect ToPhysical(LogicalRect logical, double scale, PixelPoint origin = default)
    {
        ValidateScale(scale);
        var left = (int)Math.Round(logical.X * scale) + origin.X;
        var top = (int)Math.Round(logical.Y * scale) + origin.Y;
        var right = (int)Math.Round((logical.X + logical.Width) * scale) + origin.X;
        var bottom = (int)Math.Round((logical.Y + logical.Height) * scale) + origin.Y;
        return PixelRect.FromEdges(left, top, right, bottom);
    }

    /// <summary>モニター内ローカルの論理座標へ（オーバーレイ描画用）。</summary>
    public static LogicalRect ToMonitorLocal(PixelRect physical, MonitorInfo monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        return ToLogical(physical, monitor.Scale, new PixelPoint(monitor.Bounds.X, monitor.Bounds.Y));
    }

    private static void ValidateScale(double scale)
    {
        if (!(scale > 0) || double.IsInfinity(scale))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), scale, "DPI 倍率は正の有限値である必要があります。");
        }
    }
}
