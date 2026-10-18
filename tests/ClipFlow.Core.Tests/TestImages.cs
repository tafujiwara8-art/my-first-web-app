using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Tests;

internal static class TestImages
{
    /// <summary>座標から決まる非一様なパターン（黒・単色にならない）。</summary>
    public static RawImage Pattern(int width, int height, int seed = 0)
    {
        var image = RawImage.Create(width, height, Bgra.White);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image.SetPixel(x, y, new Bgra(
                    (byte)(((x * 7) + seed + 40) % 256 | 0x40),
                    (byte)(((y * 13) + seed + 80) % 256 | 0x40),
                    (byte)((((x + y) * 5) + seed + 120) % 256 | 0x40),
                    255));
            }
        }

        return image;
    }

    /// <summary>高周波の白黒チェック柄（JPEG のリンギングを起こしやすい）。</summary>
    public static RawImage HighContrast(int width, int height)
    {
        var image = RawImage.Create(width, height, Bgra.White);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (((x / 2) + (y / 2)) % 2 == 0)
                {
                    image.SetPixel(x, y, Bgra.FromRgb(255, 255, 0));
                }
                else
                {
                    image.SetPixel(x, y, Bgra.FromRgb(255, 0, 255));
                }
            }
        }

        return image;
    }

    /// <summary>領域外の画素が元画像と一致するか。</summary>
    public static IEnumerable<(int X, int Y)> ChangedPixelsOutside(RawImage before, RawImage after, Func<int, int, bool> inside)
    {
        for (var y = 0; y < before.Height; y++)
        {
            for (var x = 0; x < before.Width; x++)
            {
                if (!inside(x, y) && before.GetPixel(x, y) != after.GetPixel(x, y))
                {
                    yield return (x, y);
                }
            }
        }
    }
}
