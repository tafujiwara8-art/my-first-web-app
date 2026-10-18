using System.Buffers.Binary;
using ClipFlow.Core.Imaging;
using FluentAssertions;

namespace ClipFlow.Windows.Tests;

public class DibV5Tests
{
    [Fact]
    public void BuildDibV5_IsBottomUpWithSrgbHeader()
    {
        using var image = RawImage.Create(2, 2, Bgra.Black);
        image.SetPixel(0, 0, Bgra.FromRgb(255, 0, 0)); // 左上

        var dib = Win32ClipboardService.BuildDibV5(image);

        dib.Length.Should().Be(124 + (2 * 2 * 4));
        BinaryPrimitives.ReadUInt32LittleEndian(dib.AsSpan(56)).Should().Be(0x73524742u, "LCS_sRGB");
        // ボトムアップなので、左上の画素は最終行（2 行目）の先頭にある
        var topLeft = dib.AsSpan(124 + 8, 4).ToArray();
        topLeft.Should().Equal(0, 0, 255, 255);
    }
}
