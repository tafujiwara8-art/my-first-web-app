using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Tests.Fakes;

public sealed class FakeClipboardService : IClipboardService
{
    public List<(RawImage Image, ClipboardWriteOptions Options)> Writes { get; } = [];

    public void WriteImage(RawImage image, ClipboardWriteOptions options) => Writes.Add((image.Clone(), options));
}
