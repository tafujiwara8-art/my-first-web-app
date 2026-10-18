namespace ClipFlow.Core.Imaging;

/// <summary>
/// BGRA32（ストライド = Width × 4、上から下）の非圧縮画像。
/// <para>
/// <see cref="Dispose"/> は画素配列を <see cref="Array.Clear(Array)"/> でゼロ埋めする。
/// これは「ClipFlow が保持するこの配列の内容を消す」措置であり、GC による過去のコピー、
/// OS のページング、他プロセス（クリップボード等）に渡ったデータまでメモリから消えることは保証しない。
/// </para>
/// </summary>
public sealed class RawImage : IDisposable
{
    public const int BytesPerPixel = 4;

    public RawImage(byte[] pixels, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (pixels.Length != (long)width * height * BytesPerPixel)
        {
            throw new ArgumentException($"画素配列の長さ {pixels.Length} が {width}×{height}×4 と一致しません。", nameof(pixels));
        }

        Pixels = pixels;
        Width = width;
        Height = height;
    }

    public byte[] Pixels { get; }

    public int Width { get; }

    public int Height { get; }

    public int Stride => Width * BytesPerPixel;

    public bool IsDisposed { get; private set; }

    public static RawImage Create(int width, int height, Bgra fill)
    {
        var image = new RawImage(new byte[checked(width * height * BytesPerPixel)], width, height);
        image.Fill(fill);
        return image;
    }

    public Bgra GetPixel(int x, int y)
    {
        var i = IndexOf(x, y);
        return new Bgra(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    public void SetPixel(int x, int y, Bgra color)
    {
        var i = IndexOf(x, y);
        Pixels[i] = color.B;
        Pixels[i + 1] = color.G;
        Pixels[i + 2] = color.R;
        Pixels[i + 3] = color.A;
    }

    public RawImage Clone()
    {
        ThrowIfDisposed();
        return new RawImage((byte[])Pixels.Clone(), Width, Height);
    }

    /// <summary>指定矩形（画像外は切り詰め）を切り出した新しい画像を返す。</summary>
    public RawImage Crop(PixelRect rect)
    {
        ThrowIfDisposed();
        var r = rect.ClipTo(Width, Height);
        if (r.IsEmpty)
        {
            return new RawImage([], 0, 0);
        }

        var result = new byte[r.Width * r.Height * BytesPerPixel];
        var rowBytes = r.Width * BytesPerPixel;
        for (var y = 0; y < r.Height; y++)
        {
            Buffer.BlockCopy(Pixels, IndexOf(r.X, r.Y + y), result, y * rowBytes, rowBytes);
        }

        return new RawImage(result, r.Width, r.Height);
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        // 保持している画素配列をゼロ埋めする。完全な消去の保証ではない（クラスのコメント参照）。
        Array.Clear(Pixels);
        IsDisposed = true;
    }

    internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    private void Fill(Bgra color)
    {
        for (var i = 0; i < Pixels.Length; i += BytesPerPixel)
        {
            Pixels[i] = color.B;
            Pixels[i + 1] = color.G;
            Pixels[i + 2] = color.R;
            Pixels[i + 3] = color.A;
        }
    }

    private int IndexOf(int x, int y)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) は画像 {Width}×{Height} の範囲外です。");
        }

        return (y * Width + x) * BytesPerPixel;
    }
}
