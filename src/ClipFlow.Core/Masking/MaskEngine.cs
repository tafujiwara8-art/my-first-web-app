using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Masking;

/// <summary>
/// マスク処理。すべての操作は入力画像を変更せず、新しい <see cref="RawImage"/> を返す。
/// 矩形は画像範囲に切り詰められ、空矩形は何もしない（例外を出さない）。
/// </summary>
public static class MaskEngine
{
    /// <summary>モザイク・ぼかしについて UI に常時表示する警告文。</summary>
    public const string WeakMaskWarning = "ぼかし・モザイクは推測・復元のリスクがあり、秘匿性を保証しません。秘匿には黒塗りを使ってください。";

    public static RawImage ApplySolidFill(RawImage source, PixelRect region, Bgra color)
    {
        var result = CloneChecked(source);
        SolidFillInPlace(result, region, color);
        return result;
    }

    public static RawImage ApplyMosaic(RawImage source, PixelRect region, int blockSize)
    {
        var result = CloneChecked(source);
        MosaicInPlace(result, region, blockSize);
        return result;
    }

    public static RawImage ApplyBlur(RawImage source, PixelRect region, int radius)
    {
        var result = CloneChecked(source);
        BlurInPlace(result, region, radius);
        return result;
    }

    /// <summary>複数のマスクを列挙順に適用した新しい画像を返す（後のマスクが前のマスクに重なる）。</summary>
    public static RawImage Apply(RawImage source, IEnumerable<MaskRegion> masks)
    {
        ArgumentNullException.ThrowIfNull(masks);
        var result = CloneChecked(source);
        foreach (var mask in masks)
        {
            ApplyInPlace(result, mask);
        }

        return result;
    }

    internal static void ApplyInPlace(RawImage image, MaskRegion mask)
    {
        switch (mask.Kind)
        {
            case MaskKind.SolidFill:
                SolidFillInPlace(image, mask.Rect, mask.FillColor);
                break;
            case MaskKind.Mosaic:
                MosaicInPlace(image, mask.Rect, mask.Strength);
                break;
            case MaskKind.Blur:
                BlurInPlace(image, mask.Rect, mask.Strength);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mask), mask.Kind, "未知のマスク種別です。");
        }
    }

    internal static void SolidFillInPlace(RawImage image, PixelRect region, Bgra color)
    {
        var r = region.ClipTo(image.Width, image.Height);
        if (r.IsEmpty)
        {
            return;
        }

        var px = image.Pixels;
        Span<byte> pattern = [color.B, color.G, color.R, color.A];
        var rowBytes = r.Width * RawImage.BytesPerPixel;
        var firstRow = px.AsSpan(Offset(image, r.X, r.Y), rowBytes);
        for (var i = 0; i < rowBytes; i += RawImage.BytesPerPixel)
        {
            pattern.CopyTo(firstRow[i..]);
        }

        for (var y = 1; y < r.Height; y++)
        {
            firstRow.CopyTo(px.AsSpan(Offset(image, r.X, r.Y + y), rowBytes));
        }
    }

    internal static void MosaicInPlace(RawImage image, PixelRect region, int blockSize)
    {
        var r = region.ClipTo(image.Width, image.Height);
        if (r.IsEmpty)
        {
            return;
        }

        var block = Math.Max(1, blockSize);
        var px = image.Pixels;
        for (var by = r.Y; by < r.Bottom; by += block)
        {
            var bh = Math.Min(block, r.Bottom - by);
            for (var bx = r.X; bx < r.Right; bx += block)
            {
                var bw = Math.Min(block, r.Right - bx);
                long sb = 0, sg = 0, sr = 0, sa = 0;
                for (var y = by; y < by + bh; y++)
                {
                    var i = Offset(image, bx, y);
                    for (var x = 0; x < bw; x++, i += 4)
                    {
                        sb += px[i];
                        sg += px[i + 1];
                        sr += px[i + 2];
                        sa += px[i + 3];
                    }
                }

                var n = bw * bh;
                var c = new Bgra((byte)(sb / n), (byte)(sg / n), (byte)(sr / n), (byte)(sa / n));
                SolidFillInPlace(image, new PixelRect(bx, by, bw, bh), c);
            }
        }
    }

    /// <summary>
    /// 領域内の画素だけを参照する 3 回反復のボックスブラー（ガウスぼかしの近似）。
    /// 領域外の画素は読み取りも書き換えもしない。
    /// </summary>
    internal static void BlurInPlace(RawImage image, PixelRect region, int radius)
    {
        var r = region.ClipTo(image.Width, image.Height);
        if (r.IsEmpty || radius <= 0)
        {
            return;
        }

        var w = r.Width;
        var h = r.Height;
        var buffer = new int[w * h * 4];
        var temp = new int[w * h * 4];
        var px = image.Pixels;
        for (var y = 0; y < h; y++)
        {
            var src = Offset(image, r.X, r.Y + y);
            for (var x = 0; x < w * 4; x++)
            {
                buffer[(y * w * 4) + x] = px[src + x];
            }
        }

        for (var pass = 0; pass < 3; pass++)
        {
            BoxBlurHorizontal(buffer, temp, w, h, radius);
            BoxBlurVertical(temp, buffer, w, h, radius);
        }

        for (var y = 0; y < h; y++)
        {
            var dst = Offset(image, r.X, r.Y + y);
            for (var x = 0; x < w * 4; x++)
            {
                px[dst + x] = (byte)buffer[(y * w * 4) + x];
            }
        }

        Array.Clear(buffer);
        Array.Clear(temp);
    }

    // 移動和によるボックスブラー（端は領域内で窓を縮める）。計算量は半径に依存しない。
    private static void BoxBlurHorizontal(int[] src, int[] dst, int w, int h, int radius)
    {
        for (var y = 0; y < h; y++)
        {
            BoxBlurLine(src, dst, y * w * 4, 4, w, radius);
        }
    }

    private static void BoxBlurVertical(int[] src, int[] dst, int w, int h, int radius)
    {
        for (var x = 0; x < w; x++)
        {
            BoxBlurLine(src, dst, x * 4, w * 4, h, radius);
        }
    }

    private static void BoxBlurLine(int[] src, int[] dst, int start, int step, int length, int radius)
    {
        for (var c = 0; c < 4; c++)
        {
            var origin = start + c;
            var sum = 0;
            var to = Math.Min(length - 1, radius);
            for (var k = 0; k <= to; k++)
            {
                sum += src[origin + (k * step)];
            }

            for (var i = 0; i < length; i++)
            {
                var from = Math.Max(0, i - radius);
                to = Math.Min(length - 1, i + radius);
                dst[origin + (i * step)] = sum / (to - from + 1);

                var leaving = i - radius;
                if (leaving >= 0)
                {
                    sum -= src[origin + (leaving * step)];
                }

                var entering = i + radius + 1;
                if (entering < length)
                {
                    sum += src[origin + (entering * step)];
                }
            }
        }
    }

    private static int Offset(RawImage image, int x, int y) => ((y * image.Width) + x) * RawImage.BytesPerPixel;

    private static RawImage CloneChecked(RawImage source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Clone();
    }
}
