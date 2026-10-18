using ClipFlow.Core.History;
using ClipFlow.Core.Tests.Fakes;
using FluentAssertions;

namespace ClipFlow.Core.Tests;

public class GatedHistoryWriterTests
{
    private static HistoryEntry Entry() => new(DateTimeOffset.UnixEpoch, HistoryEntryKind.MaskedImageCopied, 10, 10);

    [Fact]
    public async Task SecretModeOn_NeverCallsRepository_AndOff_CallsAgain()
    {
        var gate = new SecretModeState();
        var repo = new FakeHistoryRepository();
        var writer = new GatedHistoryWriter(gate, repo);

        gate.IsSecret = true;
        for (var i = 0; i < 5; i++)
        {
            (await writer.WriteAsync(Entry())).Should().BeFalse();
        }

        repo.SaveCallCount.Should().Be(0);

        gate.IsSecret = false;
        (await writer.WriteAsync(Entry())).Should().BeTrue();

        repo.SaveCallCount.Should().Be(1);
    }

    [Fact]
    public void SecretModeState_RaisesChanged()
    {
        var gate = new SecretModeState();
        var events = new List<bool>();
        ((IHistoryGate)gate).Changed += events.Add;

        gate.Toggle();
        gate.Toggle();
        gate.IsSecret = false; // 変化なしでは通知しない

        events.Should().Equal(false, true);
    }

    [Fact]
    public async Task NoOpRepository_Completes()
    {
        var writer = new GatedHistoryWriter(new SecretModeState(), new NoOpHistoryRepository());

        (await writer.WriteAsync(Entry())).Should().BeTrue();
    }
}
