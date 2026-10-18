using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Annotations;

/// <summary>
/// 注釈を画像へ焼き込む。アンチエイリアスなしの純粋な配列操作で、
/// <see cref="GetBounds"/> が返す矩形の外側の画素は変更しない。
/// </summary>
public static class AnnotationRenderer
{
    public static RawImage Render(RawImage source, IEnumerable<Annotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(annotations);
        var result = source.Clone();
        foreach (var a in annotations)
        {
            RenderInPlace(result, a);
        }

        return result;
    }

    /// <summary>注釈が描画し得る画素の外接矩形（画像で切り詰める前）。</summary>
    public static PixelRect GetBounds(Annotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var lw = LineWidthOf(annotation);
        if (annotation.Kind == AnnotationKind.RectangleFrame)
        {
            var r = PixelRect.FromPoints(annotation.Start.X, annotation.Start.Y, annotation.End.X, annotation.End.Y);
            return r;
        }

        var head = HeadSize(lw);
        var pad = (lw / 2) + head + 1;
        return PixelRect.FromPoints(annotation.Start.X, annotation.Start.Y, annotation.End.X, annotation.End.Y).Inflate(pad);
    }

    internal static void RenderInPlace(RawImage image, Annotation annotation)
    {
        switch (annotation.Kind)
        {
            case AnnotationKind.RectangleFrame:
                RenderFrame(image, annotation);
                break;
            case AnnotationKind.Arrow:
                RenderArrow(image, annotation);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(annotation), annotation.Kind, "未知の注釈種別です。");
        }
    }

    // 枠線は矩形の内側に向かって線幅ぶん描く（外接矩形 = 指定矩形）。
    private static void RenderFrame(RawImage image, Annotation a)
    {
        var r = PixelRect.FromPoints(a.Start.X, a.Start.Y, a.End.X, a.End.Y);
        if (r.IsEmpty)
        {
            return;
        }

        var lw = Math.Min(LineWidthOf(a), Math.Max(1, Math.Min(r.Width, r.Height) / 2));
        Fill(image, new PixelRect(r.X, r.Y, r.Width, lw), a.Color);
        Fill(image, new PixelRect(r.X, r.Bottom - lw, r.Width, lw), a.Color);
        Fill(image, new PixelRect(r.X, r.Y, lw, r.Height), a.Color);
        Fill(image, new PixelRect(r.Right - lw, r.Y, lw, r.Height), a.Color);
    }

    private static void RenderArrow(RawImage image, Annotation a)
    {
        var lw = LineWidthOf(a);
        double x0 = a.Start.X, y0 = a.Start.Y, x1 = a.End.X, y1 = a.End.Y;
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = Math.Sqrt((dx * dx) + (dy * dy));
        if (len < 1)
        {
            return;
        }

        var ux = dx / len;
        var uy = dy / len;
        var head = Math.Min(HeadSize(lw), len);
        var baseX = x1 - (ux * head);
        var baseY = y1 - (uy * head);
        var halfWidth = head * 0.6;
        var lx = baseX - (uy * halfWidth);
        var ly = baseY + (ux * halfWidth);
        var rx = baseX + (uy * halfWidth);
        var ry = baseY - (ux * halfWidth);
        var halfLine = lw / 2.0;

        var bounds = GetBounds(a).ClipTo(image.Width, image.Height);
        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                // 画素中心で判定する
                var px = x + 0.5;
                var py = y + 0.5;
                if (DistanceToSegment(px, py, x0, y0, baseX, baseY) <= halfLine ||
                    InTriangle(px, py, x1, y1, lx, ly, rx, ry))
                {
                    image.SetPixel(x, y, a.Color);
                }
            }
        }
    }

    private static int LineWidthOf(Annotation a) => Math.Clamp(a.LineWidth, 1, 64);

    private static int HeadSize(int lineWidth) => Math.Max(10, lineWidth * 4);

    private static void Fill(RawImage image, PixelRect rect, Bgra color)
    {
        var r = rect.ClipTo(image.Width, image.Height);
        for (var y = r.Y; y < r.Bottom; y++)
        {
            for (var x = r.X; x < r.Right; x++)
            {
                image.SetPixel(x, y, color);
            }
        }
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var vx = bx - ax;
        var vy = by - ay;
        var lenSq = (vx * vx) + (vy * vy);
        var t = lenSq <= 0 ? 0 : Math.Clamp((((px - ax) * vx) + ((py - ay) * vy)) / lenSq, 0, 1);
        var cx = ax + (t * vx) - px;
        var cy = ay + (t * vy) - py;
        return Math.Sqrt((cx * cx) + (cy * cy));
    }

    private static bool InTriangle(double px, double py, double ax, double ay, double bx, double by, double cx, double cy)
    {
        var d1 = Cross(px, py, ax, ay, bx, by);
        var d2 = Cross(px, py, bx, by, cx, cy);
        var d3 = Cross(px, py, cx, cy, ax, ay);
        var hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        var hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static double Cross(double px, double py, double ax, double ay, double bx, double by) =>
        ((px - bx) * (ay - by)) - ((ax - bx) * (py - by));
}
