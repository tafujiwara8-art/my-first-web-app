using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Tests.Fakes;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class FakeAdapterTests
{
    [Fact]
    public void FakeScreenCapture_CropsVirtualScreen()
    {
        using var screen = TestImages.Pattern(100, 80);
        var capture = new FakeScreenCapture(screen);

        using var image = capture.Capture(new PixelRect(10, 20, 30, 40));

        image.Width.Should().Be(30);
        image.GetPixel(0, 0).Should().Be(screen.GetPixel(10, 20));
        capture.GetMonitors().Should().ContainSingle(m => m.IsPrimary);
    }

    [Fact]
    public void FakeClipboard_RecordsOptions()
    {
        var clipboard = new FakeClipboardService();
        using var image = TestImages.Pattern(4, 4);

        clipboard.WriteImage(image, new ClipboardWriteOptions(ExcludeFromHistory: true));

        clipboard.Writes.Should().ContainSingle(w => w.Options.ExcludeFromHistory);
    }
}
