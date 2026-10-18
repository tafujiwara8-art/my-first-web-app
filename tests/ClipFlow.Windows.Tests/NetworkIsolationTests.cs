using System.Reflection;
using FluentAssertions;

namespace ClipFlow.Windows.Tests;

public class NetworkIsolationTests
{
    public static TheoryData<string> Assemblies => ["ClipFlow.Windows", "ClipFlow"];

    [Theory]
    [MemberData(nameof(Assemblies))]
    public void WindowsAndAppAssemblies_DoNotReferenceNetworkingOrSystemDrawing(string name)
    {
        var assembly = Assembly.Load(name);

        var references = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.Should().NotContain(n => n.StartsWith("System.Net", StringComparison.OrdinalIgnoreCase), "外部通信を行うコードを含めない");
        references.Should().NotContain(n => n.StartsWith("System.Drawing", StringComparison.OrdinalIgnoreCase), "System.Drawing.Common は使わない");
        references.Should().NotContain(n => n.StartsWith("System.Windows.Forms", StringComparison.OrdinalIgnoreCase));
    }
}
