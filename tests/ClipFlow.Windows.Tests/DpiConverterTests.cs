using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;
using FluentAssertions;

namespace ClipFlow.Windows.Tests;

public class DpiConverterTests
{
    public static TheoryData<double> Scales => [1.0, 1.25, 1.5, 2.0];

    [Theory]
    [MemberData(nameof(Scales))]
    public void PhysicalToLogicalToPhysical_RoundTrips(double scale)
    {
        var origins = new[] { new PixelPoint(0, 0), new PixelPoint(1920, 0), new PixelPoint(-2560, -300) };
        var random = new Random(1234);
        foreach (var origin in origins)
        {
            for (var i = 0; i < 500; i++)
            {
                var rect = new PixelRect(origin.X + random.Next(0, 3840), origin.Y + random.Next(0, 2160), random.Next(0, 1000), random.Next(0, 1000));

                var logical = DpiConverter.ToLogical(rect, scale, origin);
                var back = DpiConverter.ToPhysical(logical, scale, origin);

                back.Should().Be(rect, $"scale={scale} origin={origin} rect={rect}");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Scales))]
    public void Points_RoundTrip(double scale)
    {
        for (var x = -50; x < 50; x++)
        {
            var p = new PixelPoint(x * 7, x * 13);

            DpiConverter.ToPhysical(DpiConverter.ToLogical(p, scale), scale).Should().Be(p);
        }
    }

    [Fact]
    public void MonitorLocal_UsesMonitorOriginAndScale()
    {
        var monitor = new MonitorInfo("M2", new PixelRect(1920, 0, 3840, 2160), new PixelRect(1920, 0, 3840, 2100), 2.0, false);

        var local = DpiConverter.ToMonitorLocal(new PixelRect(2120, 100, 400, 200), monitor);

        local.Should().Be(new LogicalRect(100, 50, 200, 100));
    }

    [Fact]
    public void InvalidScale_Throws()
    {
        var act = () => DpiConverter.ToLogical(new PixelRect(0, 0, 1, 1), 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
