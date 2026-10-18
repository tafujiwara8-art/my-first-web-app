using System.Runtime.InteropServices;
using SkiaSharp;

namespace ClipFlow.Core.Imaging;

/// <summary>SkiaSharp による PNG / JPEG のエンコード・デコード。</summary>
public static class ImageCodec
{
    public const int DefaultJpegQuality = 90;

    /// <summary>
    /// PNG にエンコードする。出力は <see cref="PngChunkFilter"/> で
    /// IHDR / sRGB / gAMA / IDAT / IEND 以外のチャンク（tEXt・iTXt・zTXt・eXIf・tIME 等）を除去したもの。
    /// </summary>
    public static byte[] EncodePng(RawImage image)
    {
        var encoded = Encode(image, pixmap => pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, 6)));
        return PngChunkFilter.Strip(encoded);
    }

    /// <summary>JPEG（4:2:0、アルファ無視）にエンコードする。</summary>
    public static byte[] EncodeJpeg(RawImage image, int quality = DefaultJpegQuality)
    {
        var q = Math.Clamp(quality, 1, 100);
        return Encode(image, pixmap => pixmap.Encode(new SKJpegEncoderOptions(q, SKJpegEncoderDownsample.Downsample420, SKJpegEncoderAlphaOption.Ignore)));
    }

    public static byte[] Encode(RawImage image, OutputFormat format, int jpegQuality = DefaultJpegQuality) => format switch
    {
        OutputFormat.Png => EncodePng(image),
        OutputFormat.Jpeg => EncodeJpeg(image, jpegQuality),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
    };

    /// <summary>PNG / JPEG をデコードして BGRA32 にする。</summary>
    public static RawImage Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var decoded = SKImage.FromEncodedData(data) ?? throw new InvalidDataException("画像をデコードできませんでした。");
        var info = new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var pixels = new byte[info.BytesSize];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), info.RowBytes);
            if (!decoded.ReadPixels(pixmap))
            {
                throw new InvalidDataException("画素を読み出せませんでした。");
            }
        }
        finally
        {
            handle.Free();
        }

        return new RawImage(pixels, decoded.Width, decoded.Height);
    }

    private static byte[] Encode(RawImage image, Func<SKPixmap, SKData?> encode)
    {
        ArgumentNullException.ThrowIfNull(image);
        image.ThrowIfDisposed();
        if (image.Width == 0 || image.Height == 0)
        {
            throw new ArgumentException("空の画像はエンコードできません。", nameof(image));
        }

        // 色空間を付けない（iCCP 等の埋め込みを避ける）。
        var info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var handle = GCHandle.Alloc(image.Pixels, GCHandleType.Pinned);
        try
        {
            using var pixmap = new SKPixmap(info, handle.AddrOfPinnedObject(), image.Stride);
            using var data = encode(pixmap) ?? throw new InvalidOperationException("エンコードに失敗しました。");
            return data.ToArray();
        }
        finally
        {
            handle.Free();
        }
    }
}
