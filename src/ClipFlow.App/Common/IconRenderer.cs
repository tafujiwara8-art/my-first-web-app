using ClipFlow.Core.Imaging;

namespace ClipFlow.App.Common;

/// <summary>トレイアイコン用の画像を描く（アイコンファイルを同梱しないため）。</summary>
internal static class IconRenderer
{
    public static readonly Bgra NormalColor = Bgra.Parse("#1E88E5");
    public static readonly Bgra SecretColor = Bgra.Parse("#6A1B9A");

    /// <summary>円の中に白い四角枠（切り取り範囲）を描く。シークレット時は色を変え、中央に点を足す。</summary>
    public static RawImage Render(bool secret, int size = 32)
    {
        var image = RawImage.Create(size, size, new Bgra(0, 0, 0, 0));
        var fill = secret ? SecretColor : NormalColor;
        var c = (size - 1) / 2.0;
        var radius = size / 2.0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Math.Sqrt(((x - c) * (x - c)) + ((y - c) * (y - c)));
                var coverage = Math.Clamp(radius - d, 0, 1);
                if (coverage <= 0)
                {
                    continue;
                }

                var color = fill;
                var inset = size * 0.28;
                var lw = Math.Max(2, size / 12);
                var inSquare = x >= inset && x < size - inset && y >= inset && y < size - inset;
                var inHole = x >= inset + lw && x < size - inset - lw && y >= inset + lw && y < size - inset - lw;
                if (inSquare && !inHole)
                {
                    color = Bgra.White;
                }

                if (secret && Math.Abs(x - c) <= lw / 2.0 + 0.5 && Math.Abs(y - c) <= lw / 2.0 + 0.5)
                {
                    color = Bgra.White;
                }

                image.SetPixel(x, y, color with { A = (byte)(255 * coverage) });
            }
        }

        return image;
    }
}
