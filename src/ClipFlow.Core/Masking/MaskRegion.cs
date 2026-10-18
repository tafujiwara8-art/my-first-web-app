using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Masking;

/// <summary>画像に適用するマスク 1 件。</summary>
/// <param name="Rect">画像座標（物理ピクセル）の矩形。画像外へのはみ出し・空矩形を許容する。</param>
/// <param name="Kind">マスク種別。</param>
/// <param name="FillColor"><see cref="MaskKind.SolidFill"/> の塗り色。</param>
/// <param name="Strength">モザイクのブロックサイズ、またはぼかし半径（画素）。</param>
public sealed record MaskRegion(PixelRect Rect, MaskKind Kind, Bgra FillColor, int Strength)
{
    public const int DefaultMosaicBlockSize = 12;
    public const int DefaultBlurRadius = 8;

    public static MaskRegion Solid(PixelRect rect) => new(rect, MaskKind.SolidFill, Bgra.Black, 0);

    public static MaskRegion Mosaic(PixelRect rect, int blockSize = DefaultMosaicBlockSize) =>
        new(rect, MaskKind.Mosaic, Bgra.Black, blockSize);

    public static MaskRegion Blur(PixelRect rect, int radius = DefaultBlurRadius) =>
        new(rect, MaskKind.Blur, Bgra.Black, radius);

    /// <summary>秘匿性を保証しない種別（モザイク・ぼかし）か。</summary>
    public bool IsWeak => Kind != MaskKind.SolidFill;

    public static int DefaultStrengthFor(MaskKind kind) => kind switch
    {
        MaskKind.Mosaic => DefaultMosaicBlockSize,
        MaskKind.Blur => DefaultBlurRadius,
        _ => 0,
    };
}
