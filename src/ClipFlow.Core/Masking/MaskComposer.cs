using ClipFlow.Core.Annotations;
using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Masking;

/// <summary>マスクと注釈を 1 枚に焼き込む。順序はマスク → 注釈（注釈はマスクの上に描く）。</summary>
public static class MaskComposer
{
    public static RawImage Compose(RawImage source, IReadOnlyList<MaskRegion> masks, IReadOnlyList<Annotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(masks);
        ArgumentNullException.ThrowIfNull(annotations);
        var result = source.Clone();
        foreach (var m in masks)
        {
            MaskEngine.ApplyInPlace(result, m);
        }

        foreach (var a in annotations)
        {
            AnnotationRenderer.RenderInPlace(result, a);
        }

        return result;
    }

    /// <summary>
    /// JPEG 出力前の補強。黒塗り矩形を <see cref="JpegSafety.ChromaMargin"/> 画素広げてから
    /// <paramref name="grid"/> 画素境界へ外側に丸めて塗り直した新しい画像を返す。
    /// 焼き込み済み画像に対して塗りを広げるだけなので、元画像がなくても適用できる。
    /// </summary>
    public static RawImage HardenSolidFillsForJpeg(RawImage baked, IEnumerable<MaskRegion> masks, int grid = JpegSafety.McuSize)
    {
        ArgumentNullException.ThrowIfNull(baked);
        ArgumentNullException.ThrowIfNull(masks);
        var result = baked.Clone();
        foreach (var m in masks.Where(m => m.Kind == MaskKind.SolidFill))
        {
            MaskEngine.SolidFillInPlace(result, JpegSafety.HardenedRect(m.Rect, grid), m.FillColor);
        }

        return result;
    }
}

public static class JpegSafety
{
    /// <summary>4:2:0 サブサンプリング時の MCU の一辺（画素）。</summary>
    public const int McuSize = 16;

    /// <summary>
    /// 丸める前に広げる画素数。元の辺がちょうど MCU 境界に重なると、デコーダの色差アップサンプリング
    /// （隣接する色差サンプルとの補間）で境界の 1〜2 画素に隣の MCU の色が混入する。
    /// 色差 1 サンプル分（4:2:0 で 2 画素）の余白を取ることでこれを防ぐ。
    /// </summary>
    public const int ChromaMargin = 2;

    public const string LossyWarning = "JPEG は非可逆圧縮のため、黒塗り境界の画素が変化する可能性があります。秘匿目的では PNG を推奨します。";

    /// <summary>JPEG 出力時に実際に塗る矩形（余白を足して MCU 境界へ外側に丸めたもの）。</summary>
    public static PixelRect HardenedRect(PixelRect rect, int grid = McuSize) =>
        rect.Normalize().IsEmpty ? rect.Normalize() : rect.Normalize().Inflate(ChromaMargin).RoundOutward(grid);
}
