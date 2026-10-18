using ClipFlow.Core.Imaging;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class RawImageTests
{
    [Fact]
    public void Dispose_ZeroesPixels()
    {
        var image = TestImages.Pattern(16, 16);
        var pixels = image.Pixels;
        pixels.Should().Contain(b => b != 0);

        image.Dispose();

        pixels.Should().OnlyContain(b => b == 0);
        image.Pixels.Should().OnlyContain(b => b == 0);
        image.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Clone_AfterDispose_Throws()
    {
        var image = TestImages.Pattern(4, 4);
        image.Dispose();

        var act = () => image.Clone();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Constructor_RejectsWrongLength()
    {
        var act = () => new RawImage(new byte[10], 2, 2);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Crop_ClipsToImage()
    {
        using var image = TestImages.Pattern(10, 10);

        using var crop = image.Crop(new PixelRect(8, 8, 5, 5));

        crop.Width.Should().Be(2);
        crop.Height.Should().Be(2);
        crop.GetPixel(1, 1).Should().Be(image.GetPixel(9, 9));
    }

    [Fact]
    public void Bgra_ParseAndFormat_RoundTrip()
    {
        var c = Bgra.Parse("#E53935");

        c.Should().Be(Bgra.FromRgb(0xE5, 0x39, 0x35));
        c.ToHex().Should().Be("#E53935");
    }
}
