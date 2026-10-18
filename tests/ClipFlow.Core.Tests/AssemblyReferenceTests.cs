using System.Reflection;
using ClipFlow.Core.Imaging;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class AssemblyReferenceTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "System.Net",          // System.Net.Http / Sockets / Requests / WebClient 等
        "System.Drawing",
        "PresentationCore",
        "PresentationFramework",
        "WindowsBase",
        "System.Windows",
        "Microsoft.Windows",
        "Microsoft.Win32",
    ];

    [Fact]
    public void Core_DoesNotReferenceNetworkDrawingOrWpf()
    {
        var core = typeof(RawImage).Assembly;

        var references = core.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        references.Should().NotBeEmpty();
        references.Where(n => ForbiddenPrefixes.Any(p => n.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            .Should().BeEmpty("Core は OS 非依存でネットワークを使わない");
    }

    [Fact]
    public void Core_DoesNotUseNetworkTypes()
    {
        // 型転送で System.Runtime 経由になる型（例: System.Uri は許可）を除き、System.Net 名前空間の型を使っていないこと。
        var core = typeof(RawImage).Assembly;
        var usedNetTypes = core.GetTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(MemberTypes)
            .Where(t => t.Namespace?.StartsWith("System.Net", StringComparison.Ordinal) == true)
            .Distinct()
            .ToList();

        usedNetTypes.Should().BeEmpty();
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo m) => m switch
    {
        FieldInfo f => [f.FieldType],
        PropertyInfo p => [p.PropertyType],
        MethodInfo mi => [mi.ReturnType, .. mi.GetParameters().Select(p => p.ParameterType)],
        ConstructorInfo c => c.GetParameters().Select(p => p.ParameterType),
        _ => [],
    };
}
