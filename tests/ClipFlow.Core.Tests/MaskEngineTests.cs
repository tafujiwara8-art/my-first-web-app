using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class MaskEngineTests
{
    private static readonly Bgra Red = Bgra.FromRgb(255, 0, 0);

    [Fact]
    public void SolidFill_FillsRegionExactly_AndLeavesOutsideUntouched()
    {
        using var src = TestImages.Pattern(40, 30);
        var rect = new PixelRect(5, 4, 10, 7);

        using var result = MaskEngine.ApplySolidFill(src, rect, Bgra.Black);

        for (var y = 0; y < src.Height; y++)
        {
            for (var x = 0; x < src.Width; x++)
            {
                var expected = rect.Contains(x, y) ? Bgra.Black : src.GetPixel(x, y);
                result.GetPixel(x, y).Should().Be(expected, $"({x},{y})");
            }
        }
    }

    [Fact]
    public void SolidFill_DoesNotModifySource()
    {
        using var src = TestImages.Pattern(20, 20);
        var copy = (byte[])src.Pixels.Clone();

        using var _ = MaskEngine.ApplySolidFill(src, new PixelRect(0, 0, 10, 10), Bgra.Black);

        src.Pixels.Should().Equal(copy);
    }

    [Fact]
    public void Apply_MultipleOverlappingRegions_LaterWinsAndUnionIsFilled()
    {
        using var src = TestImages.Pattern(50, 50);
        var a = new PixelRect(5, 5, 20, 20);
        var b = new PixelRect(15, 15, 20, 20);

        using var result = MaskEngine.Apply(src, [MaskRegion.Solid(a), new MaskRegion(b, MaskKind.SolidFill, Red, 0)]);

        for (var y = 0; y < 50; y++)
        {
            for (var x = 0; x < 50; x++)
            {
                var expected = b.Contains(x, y) ? Red : a.Contains(x, y) ? Bgra.Black : src.GetPixel(x, y);
                result.GetPixel(x, y).Should().Be(expected, $"({x},{y})");
            }
        }
    }

    [Theory]
    [InlineData(-10, -10, 25, 25)]   // 左上へはみ出し
    [InlineData(30, 20, 100, 100)]   // 右下へはみ出し
    [InlineData(-5, 10, 200, 3)]     // 横に貫通
    [InlineData(-100, -100, 400, 400)] // 画像全体を包含
    public void SolidFill_RegionOutsideImage_IsClippedWithoutException(int x, int y, int w, int h)
    {
        using var src = TestImages.Pattern(40, 30);
        var rect = new PixelRect(x, y, w, h);
        var clipped = rect.ClipTo(40, 30);

        using var result = MaskEngine.ApplySolidFill(src, rect, Bgra.Black);

        for (var py = 0; py < 30; py++)
        {
            for (var px = 0; px < 40; px++)
            {
                var expected = clipped.Contains(px, py) ? Bgra.Black : src.GetPixel(px, py);
                result.GetPixel(px, py).Should().Be(expected);
            }
        }
    }

    [Theory]
    [InlineData(5, 5, 0, 10)]
    [InlineData(5, 5, 10, 0)]
    [InlineData(5, 5, 0, 0)]
    [InlineData(500, 500, 10, 10)] // 完全に画像外
    [InlineData(10, 10, -5, -5)]   // 負の幅・高さ（正規化されると (5,5) 5×5）
    public void ZeroSizeOrOutsideRegion_DoesNotThrow(int x, int y, int w, int h)
    {
        using var src = TestImages.Pattern(40, 30);
        var rect = new PixelRect(x, y, w, h);

        var act = () =>
        {
            MaskEngine.ApplySolidFill(src, rect, Bgra.Black).Dispose();
            MaskEngine.ApplyMosaic(src, rect, 8).Dispose();
            MaskEngine.ApplyBlur(src, rect, 4).Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void NegativeSizeRect_IsNormalized()
    {
        using var src = TestImages.Pattern(20, 20);

        using var result = MaskEngine.ApplySolidFill(src, new PixelRect(10, 10, -5, -5), Bgra.Black);

        result.GetPixel(5, 5).Should().Be(Bgra.Black);
        result.GetPixel(9, 9).Should().Be(Bgra.Black);
        result.GetPixel(10, 10).Should().Be(src.GetPixel(10, 10));
        result.GetPixel(4, 4).Should().Be(src.GetPixel(4, 4));
    }

    [Theory]
    [InlineData(MaskKind.Mosaic, 6)]
    [InlineData(MaskKind.Blur, 5)]
    public void MosaicAndBlur_LeaveOutsideUntouched(MaskKind kind, int strength)
    {
        using var src = TestImages.Pattern(60, 40);
        var rect = new PixelRect(7, 9, 23, 17);

        using var result = MaskEngine.Apply(src, [new MaskRegion(rect, kind, Bgra.Black, strength)]);

        TestImages.ChangedPixelsOutside(src, result, rect.Contains).Should().BeEmpty();
        result.Pixels.Should().NotEqual(src.Pixels, "領域内は変化しているはず");
    }

    [Theory]
    [InlineData(MaskKind.Mosaic)]
    [InlineData(MaskKind.Blur)]
    public void MosaicAndBlur_OverlappingAndOverflowingRegions_LeaveOutsideUntouched(MaskKind kind)
    {
        using var src = TestImages.Pattern(60, 40);
        var a = new PixelRect(-5, -5, 20, 20);
        var b = new PixelRect(10, 10, 80, 10);

        using var result = MaskEngine.Apply(src, [new MaskRegion(a, kind, Bgra.Black, 4), new MaskRegion(b, kind, Bgra.Black, 4)]);

        TestImages.ChangedPixelsOutside(src, result, (x, y) => a.Contains(x, y) || b.Contains(x, y)).Should().BeEmpty();
    }

    [Fact]
    public void Mosaic_ProducesUniformBlocks()
    {
        using var src = TestImages.Pattern(32, 32);

        using var result = MaskEngine.ApplyMosaic(src, new PixelRect(0, 0, 16, 16), 8);

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                result.GetPixel(x, y).Should().Be(result.GetPixel(0, 0));
            }
        }
    }

    [Fact]
    public void Blur_UniformRegion_StaysUniform()
    {
        using var src = RawImage.Create(30, 30, Bgra.FromRgb(10, 200, 30));

        using var result = MaskEngine.ApplyBlur(src, new PixelRect(3, 3, 20, 20), 6);

        result.Pixels.Should().Equal(src.Pixels);
    }
}
