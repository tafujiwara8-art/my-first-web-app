using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Tests.Fakes;

/// <summary>仮想スクリーン全体を表す画像から切り出して返す Fake。</summary>
public sealed class FakeScreenCapture(RawImage virtualScreen, IReadOnlyList<MonitorInfo>? monitors = null) : IScreenCapture
{
    public List<PixelRect> Requests { get; } = [];

    public IReadOnlyList<MonitorInfo> GetMonitors() => monitors ??
    [
        new MonitorInfo("FAKE1", new PixelRect(0, 0, virtualScreen.Width, virtualScreen.Height), new PixelRect(0, 0, virtualScreen.Width, virtualScreen.Height), 1.0, true),
    ];

    public RawImage Capture(PixelRect regionInPhysicalPixels)
    {
        Requests.Add(regionInPhysicalPixels);
        return virtualScreen.Crop(regionInPhysicalPixels);
    }
}
