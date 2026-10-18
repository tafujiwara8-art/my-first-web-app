using ClipFlow.Core.Annotations;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;

namespace ClipFlow.Core.Editing;

/// <summary>マスクエディタ上の 1 要素（画像座標）。不変オブジェクトとして扱い、変更は新しいインスタンスで表す。</summary>
public abstract record EditorItem(Guid Id)
{
    /// <summary>リサイズ用ハンドルの位置。</summary>
    public abstract IReadOnlyList<PixelPoint> HandlePoints { get; }

    public abstract PixelRect Bounds { get; }

    public abstract EditorItem Translate(int dx, int dy);

    /// <summary>ハンドル <paramref name="index"/> を <paramref name="position"/> へ動かした要素を返す。</summary>
    public abstract EditorItem MoveHandle(int index, PixelPoint position);

    public abstract bool HitTest(PixelPoint point, int tolerance);

    /// <summary>ドラッグ作成中に終点を更新した要素を返す。</summary>
    public EditorItem WithDragEnd(PixelPoint end) => MoveHandle(HandlePoints.Count - 1, end);

    /// <summary>作成を確定してよい大きさがあるか（クリックだけの誤操作を除く）。</summary>
    public abstract bool IsMeaningful { get; }

    protected static IReadOnlyList<PixelPoint> Corners(PixelRect r) =>
    [
        new(r.X, r.Y), new(r.Right, r.Y), new(r.X, r.Bottom), new(r.Right, r.Bottom),
    ];

    // 角 index を動かし、対角を固定した矩形を返す（0:左上 1:右上 2:左下 3:右下）。
    protected static PixelRect MoveCorner(PixelRect r, int index, PixelPoint p)
    {
        var opposite = index switch
        {
            0 => new PixelPoint(r.Right, r.Bottom),
            1 => new PixelPoint(r.X, r.Bottom),
            2 => new PixelPoint(r.Right, r.Y),
            3 => new PixelPoint(r.X, r.Y),
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
        return PixelRect.FromPoints(opposite.X, opposite.Y, p.X, p.Y);
    }
}

public sealed record MaskItem(Guid Id, PixelRect Rect, MaskKind Kind) : EditorItem(Id)
{
    public static MaskItem Begin(PixelPoint start, MaskKind kind) => new(Guid.NewGuid(), new PixelRect(start.X, start.Y, 0, 0), kind);

    public override IReadOnlyList<PixelPoint> HandlePoints => Corners(Rect);

    public override PixelRect Bounds => Rect;

    public override bool IsMeaningful => Rect.Width >= 2 && Rect.Height >= 2;

    public override EditorItem Translate(int dx, int dy) => this with { Rect = Rect.Offset(dx, dy) };

    public override EditorItem MoveHandle(int index, PixelPoint position) => this with { Rect = MoveCorner(Rect, index, position) };

    public override bool HitTest(PixelPoint point, int tolerance) => Rect.Inflate(tolerance).Contains(point.X, point.Y);

    public MaskRegion ToRegion() => new(Rect, Kind, Bgra.Black, MaskRegion.DefaultStrengthFor(Kind));
}

public sealed record FrameItem(Guid Id, PixelRect Rect, Bgra Color, int LineWidth) : EditorItem(Id)
{
    public static FrameItem Begin(PixelPoint start, Bgra color, int lineWidth) => new(Guid.NewGuid(), new PixelRect(start.X, start.Y, 0, 0), color, lineWidth);

    public override IReadOnlyList<PixelPoint> HandlePoints => Corners(Rect);

    public override PixelRect Bounds => Rect;

    public override bool IsMeaningful => Rect.Width >= 4 && Rect.Height >= 4;

    public override EditorItem Translate(int dx, int dy) => this with { Rect = Rect.Offset(dx, dy) };

    public override EditorItem MoveHandle(int index, PixelPoint position) => this with { Rect = MoveCorner(Rect, index, position) };

    // 枠線の付近だけを当たり判定にする（内側をクリックしても下のマスクを選べるように）。
    public override bool HitTest(PixelPoint point, int tolerance)
    {
        var outer = Rect.Inflate(tolerance);
        var inner = Rect.Inflate(-(LineWidth + tolerance));
        return outer.Contains(point.X, point.Y) && (inner.IsEmpty || !inner.Contains(point.X, point.Y));
    }

    public Annotation ToAnnotation() => Annotation.Frame(Rect, Color, LineWidth);
}

public sealed record ArrowItem(Guid Id, PixelPoint Start, PixelPoint End, Bgra Color, int LineWidth) : EditorItem(Id)
{
    public static ArrowItem Begin(PixelPoint start, Bgra color, int lineWidth) => new(Guid.NewGuid(), start, start, color, lineWidth);

    public override IReadOnlyList<PixelPoint> HandlePoints => [Start, End];

    public override PixelRect Bounds => AnnotationRenderer.GetBounds(ToAnnotation());

    public override bool IsMeaningful => Math.Abs(End.X - Start.X) + Math.Abs(End.Y - Start.Y) >= 6;

    public override EditorItem Translate(int dx, int dy) =>
        this with { Start = new(Start.X + dx, Start.Y + dy), End = new(End.X + dx, End.Y + dy) };

    public override EditorItem MoveHandle(int index, PixelPoint position) => index switch
    {
        0 => this with { Start = position },
        1 => this with { End = position },
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    public override bool HitTest(PixelPoint point, int tolerance)
    {
        double vx = End.X - Start.X, vy = End.Y - Start.Y;
        var lenSq = (vx * vx) + (vy * vy);
        var t = lenSq == 0 ? 0 : Math.Clamp((((point.X - Start.X) * vx) + ((point.Y - Start.Y) * vy)) / lenSq, 0, 1);
        var dx = Start.X + (t * vx) - point.X;
        var dy = Start.Y + (t * vy) - point.Y;
        return Math.Sqrt((dx * dx) + (dy * dy)) <= tolerance + (LineWidth / 2.0) + 2;
    }

    public Annotation ToAnnotation() => Annotation.CreateArrow(Start, End, Color, LineWidth);
}
