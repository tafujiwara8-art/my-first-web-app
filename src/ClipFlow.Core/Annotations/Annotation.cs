using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Annotations;

public enum AnnotationKind
{
    /// <summary>矩形枠。<see cref="Annotation.Start"/> と <see cref="Annotation.End"/> は対角の 2 点。</summary>
    RectangleFrame,

    /// <summary>矢印。<see cref="Annotation.Start"/> から <see cref="Annotation.End"/>（矢じり側）へ。</summary>
    Arrow,
}

/// <summary>画像座標（物理ピクセル）の注釈 1 件。文字注釈は P5 で追加予定。</summary>
public sealed record Annotation(AnnotationKind Kind, PixelPoint Start, PixelPoint End, Bgra Color, int LineWidth)
{
    public const string DefaultColorHex = "#E53935";
    public const int DefaultLineWidth = 3;

    public static Annotation Frame(PixelRect rect, Bgra color, int lineWidth) =>
        new(AnnotationKind.RectangleFrame, new PixelPoint(rect.X, rect.Y), new PixelPoint(rect.Right, rect.Bottom), color, lineWidth);

    public static Annotation CreateArrow(PixelPoint from, PixelPoint to, Bgra color, int lineWidth) =>
        new(AnnotationKind.Arrow, from, to, color, lineWidth);
}
