using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClipFlow.Core.Imaging;

namespace ClipFlow.App.Common;

internal static class ImageInterop
{
    /// <summary>96 DPI の WriteableBitmap（1 DIP = 1 画素）を作る。</summary>
    public static WriteableBitmap ToBitmap(RawImage image)
    {
        var bitmap = new WriteableBitmap(image.Width, image.Height, 96, 96, PixelFormats.Bgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Pixels, image.Stride, 0);
        return bitmap;
    }

    /// <summary>
    /// 表示用ビットマップの画素をゼロで上書きする。WPF 内部のキャッシュやグラフィックスドライバー側の
    /// コピーまで消える保証はない（ベストエフォート）。
    /// </summary>
    public static void Clear(WriteableBitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        var stride = bitmap.PixelWidth * 4;
        bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), new byte[stride * bitmap.PixelHeight], stride, 0);
    }
}
