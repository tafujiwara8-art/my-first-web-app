using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class ImageCodecTests
{
    private static readonly string[] PermittedChunks = ["IHDR", "sRGB", "gAMA", "IDAT", "IEND"];

    [Fact]
    public void Png_ContainsOnlyPermittedChunks()
    {
        using var image = TestImages.Pattern(64, 48);

        var png = ImageCodec.EncodePng(image);

        var chunks = PngChunkFilter.ListChunkTypes(png);
        chunks.Should().OnlyContain(c => PermittedChunks.Contains(c));
        chunks[0].Should().Be("IHDR");
        chunks[^1].Should().Be("IEND");
        chunks.Should().Contain("IDAT");
    }

    [Fact]
    public void ChunkFilter_RemovesMetadataChunks()
    {
        using var image = TestImages.Pattern(8, 8);
        var clean = ImageCodec.EncodePng(image);
        var dirty = InsertChunksAfterIhdr(clean, "tEXt", "iTXt", "zTXt", "eXIf", "tIME", "pHYs", "iCCP");
        PngChunkFilter.ListChunkTypes(dirty).Should().Contain(["tEXt", "eXIf", "tIME"]);

        var stripped = PngChunkFilter.Strip(dirty);

        PngChunkFilter.ListChunkTypes(stripped).Should().OnlyContain(c => PermittedChunks.Contains(c));
        using var decoded = ImageCodec.Decode(stripped);
        decoded.Pixels.Should().Equal(image.Pixels);
    }

    [Fact]
    public void Png_RoundTrip_SolidFillIsPixelExact()
    {
        using var src = TestImages.Pattern(70, 50);
        var rect = new PixelRect(11, 7, 33, 21);
        using var masked = MaskEngine.ApplySolidFill(src, rect, Bgra.Black);

        using var decoded = ImageCodec.Decode(ImageCodec.EncodePng(masked));

        decoded.Width.Should().Be(70);
        decoded.Height.Should().Be(50);
        decoded.Pixels.Should().Equal(masked.Pixels, "PNG は可逆");
        for (var y = rect.Y; y < rect.Bottom; y++)
        {
            for (var x = rect.X; x < rect.Right; x++)
            {
                decoded.GetPixel(x, y).Should().Be(Bgra.Black);
            }
        }
    }

    [Theory]
    [InlineData(21, 19, 37, 29)] // 16 画素境界に揃っていない（下端 48 だけ境界に一致）
    [InlineData(16, 16, 32, 32)] // 全辺が境界に一致（色差アップサンプリングの混入が最も起きやすい）
    [InlineData(0, 0, 40, 30)]   // 画像端に接する
    [InlineData(70, 60, 26, 20)] // 右下端に接する
    public void Jpeg_WithMcuRounding_KeepsSpecifiedRegionNearBlack(int x, int y, int w, int h)
    {
        using var src = TestImages.HighContrast(96, 80);
        var rect = new PixelRect(x, y, w, h);
        var masks = new[] { MaskRegion.Solid(rect) };
        using var baked = MaskEngine.Apply(src, masks);
        using var hardened = MaskComposer.HardenSolidFillsForJpeg(baked, masks);

        using var decoded = ImageCodec.Decode(ImageCodec.EncodeJpeg(hardened, 90));

        MaxDeviationFromBlack(decoded, rect).Should().BeLessThanOrEqualTo(8);
    }

    [Fact]
    public void Jpeg_WithoutMcuRounding_CanLeakBoundaryPixels()
    {
        // 警告表示の根拠: 丸めなしでは、指定領域内の画素が黒から閾値を超えて変化し得る。
        using var src = TestImages.HighContrast(96, 80);
        var rect = new PixelRect(21, 19, 37, 29);
        using var baked = MaskEngine.Apply(src, [MaskRegion.Solid(rect)]);

        using var decoded = ImageCodec.Decode(ImageCodec.EncodeJpeg(baked, 90));

        MaxDeviationFromBlack(decoded, rect).Should().BeGreaterThan(8);
    }

    [Fact]
    public void RoundOutward_AlignsToGrid()
    {
        new PixelRect(21, 19, 37, 29).RoundOutward(16).Should().Be(new PixelRect(16, 16, 48, 32));
        new PixelRect(16, 32, 16, 16).RoundOutward(16).Should().Be(new PixelRect(16, 32, 16, 16));
        new PixelRect(-3, -3, 4, 4).RoundOutward(16).Should().Be(new PixelRect(-16, -16, 32, 32));
    }

    [Fact]
    public void HardenedRect_AddsChromaMarginBeforeRounding()
    {
        JpegSafety.HardenedRect(new PixelRect(16, 16, 32, 32)).Should().Be(new PixelRect(0, 0, 64, 64));
        JpegSafety.HardenedRect(new PixelRect(21, 19, 37, 29)).Should().Be(new PixelRect(16, 16, 48, 48));
        JpegSafety.HardenedRect(new PixelRect(5, 5, 0, 0)).IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void Jpeg_RoundingWithoutChromaMargin_CanStillLeakAtAlignedEdge()
    {
        // 16 画素丸めだけでは不十分な根拠: 辺が MCU 境界に一致すると境界行に隣接 MCU の色が混入する。
        using var src = TestImages.HighContrast(96, 80);
        var rect = new PixelRect(21, 19, 37, 29);
        using var baked = MaskEngine.Apply(src, [MaskRegion.Solid(rect)]);
        using var roundedOnly = MaskEngine.ApplySolidFill(baked, rect.RoundOutward(16), Bgra.Black);

        using var decoded = ImageCodec.Decode(ImageCodec.EncodeJpeg(roundedOnly, 90));

        MaxDeviationFromBlack(decoded, rect).Should().BeGreaterThan(8);
    }

    [Fact]
    public void Jpeg_HasNoExifSegment()
    {
        using var image = TestImages.Pattern(32, 32);

        var jpeg = ImageCodec.EncodeJpeg(image);

        // APP1 (FF E1) は EXIF / XMP を格納する。
        ContainsMarker(jpeg, 0xE1).Should().BeFalse();
    }

    private static int MaxDeviationFromBlack(RawImage image, PixelRect rect)
    {
        var max = 0;
        for (var y = rect.Y; y < rect.Bottom; y++)
        {
            for (var x = rect.X; x < rect.Right; x++)
            {
                var p = image.GetPixel(x, y);
                max = Math.Max(max, Math.Max(p.R, Math.Max(p.G, p.B)));
            }
        }

        return max;
    }

    private static bool ContainsMarker(byte[] jpeg, byte marker)
    {
        // SOS (FF DA) までのヘッダ部分だけを走査する。
        var pos = 2;
        while (pos + 4 <= jpeg.Length && jpeg[pos] == 0xFF)
        {
            var m = jpeg[pos + 1];
            if (m == marker)
            {
                return true;
            }

            if (m == 0xDA)
            {
                return false;
            }

            pos += 2 + ((jpeg[pos + 2] << 8) | jpeg[pos + 3]);
        }

        return false;
    }

    private static byte[] InsertChunksAfterIhdr(byte[] png, params string[] types)
    {
        const int ihdrEnd = 8 + 12 + 13;
        using var ms = new MemoryStream();
        ms.Write(png, 0, ihdrEnd);
        foreach (var t in types)
        {
            var data = System.Text.Encoding.ASCII.GetBytes("secret");
            ms.Write([0, 0, 0, (byte)data.Length]);
            ms.Write(System.Text.Encoding.ASCII.GetBytes(t));
            ms.Write(data);
            ms.Write([0, 0, 0, 0]); // CRC はチャンク解析では検証しない
        }

        ms.Write(png, ihdrEnd, png.Length - ihdrEnd);
        return ms.ToArray();
    }
}
