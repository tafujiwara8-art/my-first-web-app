using System.Buffers.Binary;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;
using FluentAssertions;

namespace ClipFlow.Windows.Tests;

// クリップボードはプロセス外と共有されるため、このクラスのテストは順に実行する（xUnit は同一クラス内を逐次実行）。
public sealed class ClipboardServiceTests : IDisposable
{
    private readonly Win32ClipboardService _service = new();

    [Fact]
    public void WriteImage_SetsPngDibV5AndHistoryExclusionFormats()
    {
        using var image = Pattern(37, 23);

        _service.WriteImage(image, new ClipboardWriteOptions(ExcludeFromHistory: true));

        ClipboardInspector.HasFormat(ClipboardFormatNames.Png).Should().BeTrue();
        ClipboardInspector.HasDibV5().Should().BeTrue();
        ClipboardInspector.HasFormat(ClipboardFormatNames.ExcludeFromMonitorProcessing).Should().BeTrue();
        ClipboardInspector.HasFormat(ClipboardFormatNames.CanIncludeInClipboardHistory).Should().BeTrue();
        ClipboardInspector.HasFormat(ClipboardFormatNames.CanUploadToCloudClipboard).Should().BeTrue();

        ReadDword(ClipboardFormatNames.CanIncludeInClipboardHistory).Should().Be(0u);
        ReadDword(ClipboardFormatNames.CanUploadToCloudClipboard).Should().Be(0u);
    }

    [Fact]
    public void WriteImage_PngAndDibContainSamePixels()
    {
        using var image = Pattern(16, 9);

        _service.WriteImage(image, new ClipboardWriteOptions(ExcludeFromHistory: true));

        using var fromPng = ImageCodec.Decode(ClipboardInspector.ReadFormat(ClipboardFormatNames.Png)!);
        fromPng.Pixels.Should().Equal(image.Pixels);

        var dib = ClipboardInspector.ReadDibV5()!;
        BinaryPrimitives.ReadUInt32LittleEndian(dib).Should().Be(124u, "BITMAPV5HEADER");
        BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(4)).Should().Be(16);
        BinaryPrimitives.ReadInt32LittleEndian(dib.AsSpan(8)).Should().Be(9);
        BinaryPrimitives.ReadUInt16LittleEndian(dib.AsSpan(14)).Should().Be(32);
    }

    [Fact]
    public void WriteImage_WithoutExclusion_DoesNotSetExclusionFormats()
    {
        using var image = Pattern(4, 4);

        _service.WriteImage(image, new ClipboardWriteOptions(ExcludeFromHistory: false));

        ClipboardInspector.HasFormat(ClipboardFormatNames.Png).Should().BeTrue();
        ClipboardInspector.HasDibV5().Should().BeTrue();
        ClipboardInspector.HasFormat(ClipboardFormatNames.ExcludeFromMonitorProcessing).Should().BeFalse();
        ClipboardInspector.HasFormat(ClipboardFormatNames.CanIncludeInClipboardHistory).Should().BeFalse();
        ClipboardInspector.HasFormat(ClipboardFormatNames.CanUploadToCloudClipboard).Should().BeFalse();
    }

    public void Dispose() => _service.Dispose();

    private static uint ReadDword(string format)
    {
        var data = ClipboardInspector.ReadFormat(format);
        data.Should().NotBeNull();
        data!.Length.Should().BeGreaterThanOrEqualTo(4);
        return BinaryPrimitives.ReadUInt32LittleEndian(data);
    }

    private static RawImage Pattern(int w, int h)
    {
        var image = RawImage.Create(w, h, Bgra.White);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                image.SetPixel(x, y, new Bgra((byte)(x * 9), (byte)(y * 11), (byte)((x + y) * 3), 255));
            }
        }

        return image;
    }
}
