namespace ClipFlow.Core.Imaging;

/// <summary>物理ピクセル単位の矩形。幅・高さが 0 以下の矩形は空として扱う。</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>2 点から正規化された矩形を作る（ドラッグ方向に依存しない）。</summary>
    public static PixelRect FromPoints(int x1, int y1, int x2, int y2)
    {
        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        return new PixelRect(left, top, Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    }

    public static PixelRect FromEdges(int left, int top, int right, int bottom) =>
        new(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));

    /// <summary>負の幅・高さを持つ矩形を正規化する。</summary>
    public PixelRect Normalize() => FromPoints(X, Y, X + Width, Y + Height);

    public PixelRect Intersect(PixelRect other)
    {
        var a = Normalize();
        var b = other.Normalize();
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        return right <= left || bottom <= top ? new PixelRect(left, top, 0, 0) : FromEdges(left, top, right, bottom);
    }

    /// <summary>画像 (0,0)-(width,height) の範囲に切り詰める。</summary>
    public PixelRect ClipTo(int width, int height) => Intersect(new PixelRect(0, 0, width, height));

    public PixelRect Union(PixelRect other)
    {
        if (IsEmpty)
        {
            return other;
        }

        if (other.IsEmpty)
        {
            return this;
        }

        return FromEdges(Math.Min(X, other.X), Math.Min(Y, other.Y), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
    }

    public PixelRect Inflate(int amount) => FromEdges(X - amount, Y - amount, Right + amount, Bottom + amount);

    public PixelRect Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    /// <summary>
    /// 原点 (0,0) 基準の <paramref name="grid"/> 画素境界へ外側に丸める。
    /// JPEG の MCU（4:2:0 では 16×16）境界に揃え、境界ブロックへの元画素混入を防ぐために使う。
    /// </summary>
    public PixelRect RoundOutward(int grid)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(grid, 1);
        var r = Normalize();
        if (r.IsEmpty)
        {
            return r;
        }

        var left = FloorTo(r.X, grid);
        var top = FloorTo(r.Y, grid);
        var right = CeilTo(r.Right, grid);
        var bottom = CeilTo(r.Bottom, grid);
        return FromEdges(left, top, right, bottom);
    }

    public override string ToString() => $"({X},{Y}) {Width}×{Height}";

    private static int FloorTo(int v, int grid) => (int)Math.Floor(v / (double)grid) * grid;

    private static int CeilTo(int v, int grid) => (int)Math.Ceiling(v / (double)grid) * grid;
}
