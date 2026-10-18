using ClipFlow.Core.Annotations;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class AnnotationRendererTests
{
    private static readonly Bgra Red = Bgra.Parse(Annotation.DefaultColorHex);

    [Fact]
    public void Frame_DrawsOnlyInsideBounds()
    {
        using var src = TestImages.Pattern(80, 60);
        var a = Annotation.Frame(new PixelRect(10, 8, 40, 30), Red, 3);
        var bounds = AnnotationRenderer.GetBounds(a);

        using var result = AnnotationRenderer.Render(src, [a]);

        TestImages.ChangedPixelsOutside(src, result, bounds.Contains).Should().BeEmpty();
        result.GetPixel(10, 8).Should().Be(Red);
        result.GetPixel(49, 37).Should().Be(Red);
        result.GetPixel(12, 20).Should().Be(Red, "線幅 3 の左辺");
        result.GetPixel(13, 20).Should().Be(src.GetPixel(13, 20), "枠の内側は不変");
    }

    [Theory]
    [InlineData(5, 5, 70, 50, 3)]
    [InlineData(70, 50, 5, 5, 6)]
    [InlineData(40, 5, 40, 55, 2)]   // 垂直
    [InlineData(-20, 30, 100, 30, 4)] // 画像をはみ出す水平線
    public void Arrow_DrawsOnlyInsideBounds(int x0, int y0, int x1, int y1, int lw)
    {
        using var src = TestImages.Pattern(80, 60);
        var a = Annotation.CreateArrow(new PixelPoint(x0, y0), new PixelPoint(x1, y1), Red, lw);
        var bounds = AnnotationRenderer.GetBounds(a);

        using var result = AnnotationRenderer.Render(src, [a]);

        TestImages.ChangedPixelsOutside(src, result, bounds.Contains).Should().BeEmpty();
        result.Pixels.Should().NotEqual(src.Pixels);
    }

    [Fact]
    public void Arrow_FarFromBounds_IsUnchanged()
    {
        using var src = TestImages.Pattern(100, 100);
        var a = Annotation.CreateArrow(new PixelPoint(10, 10), new PixelPoint(40, 10), Red, 3);

        using var result = AnnotationRenderer.Render(src, [a]);

        // 線から十分離れた領域（下半分）は不変
        TestImages.ChangedPixelsOutside(src, result, (_, y) => y < 50).Should().BeEmpty();
    }

    [Fact]
    public void ZeroLengthOrEmpty_DoesNotThrow()
    {
        using var src = TestImages.Pattern(20, 20);

        var act = () => AnnotationRenderer.Render(src,
        [
            Annotation.CreateArrow(new PixelPoint(5, 5), new PixelPoint(5, 5), Red, 3),
            Annotation.Frame(new PixelRect(5, 5, 0, 0), Red, 3),
            Annotation.Frame(new PixelRect(500, 500, 10, 10), Red, 3),
        ]).Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Compose_DrawsAnnotationsOverMasks()
    {
        using var src = TestImages.Pattern(50, 50);
        var mask = MaskRegion.Solid(new PixelRect(0, 0, 50, 50));
        var frame = Annotation.Frame(new PixelRect(10, 10, 20, 20), Red, 2);

        using var result = MaskComposer.Compose(src, [mask], [frame]);

        result.GetPixel(10, 10).Should().Be(Red);
        result.GetPixel(20, 20).Should().Be(Bgra.Black);
    }
}
