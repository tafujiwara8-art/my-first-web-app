using ClipFlow.Core.Abstractions;
using ClipFlow.Core.IO;
using ClipFlow.Core.Settings;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ClipFlowTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Defaults_MatchSpecification()
    {
        var s = new ClipFlowSettings();

        s.CaptureAndMaskHotkey.Should().Be(new HotkeyBinding(Modifiers.Control | Modifiers.Shift, 'M'));
        s.CutCopyHotkey.Should().Be(new HotkeyBinding(Modifiers.Control | Modifiers.Shift, 'X'));
        s.ToggleSecretHotkey.Should().Be(new HotkeyBinding(Modifiers.Control | Modifiers.Shift, 'S'));
        s.ExcludeFromWindowsHistoryInNormalMode.Should().BeTrue();
        s.RoundSolidFillsForJpeg.Should().BeTrue();
        s.CaptureAndMaskHotkey.ToString().Should().Be("Ctrl+Shift+M");
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var store = new SettingsStore(Path.Combine(_dir, "settings.json"));
        var settings = new ClipFlowSettings
        {
            CutCopyHotkey = new HotkeyBinding(Modifiers.Control | Modifiers.Alt, 'Q'),
            BadgePosition = new BadgePosition(100.5, 200),
            ExcludeFromWindowsHistoryInNormalMode = false,
        };

        store.Save(settings);
        var loaded = store.Load();

        loaded.Should().Be(settings);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ not json");

        new SettingsStore(path).Load().Should().Be(new ClipFlowSettings());
    }

    [Fact]
    public void CleanTempDirectory_RemovesEverything()
    {
        var paths = new AppPaths(_dir);
        Directory.CreateDirectory(Path.Combine(paths.TempDirectory, "nested"));
        File.WriteAllBytes(Path.Combine(paths.TempDirectory, "nested", "leftover.png"), [1, 2, 3]);

        paths.CleanTempDirectory().Should().BeNull();

        Directory.Exists(paths.TempDirectory).Should().BeFalse();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
