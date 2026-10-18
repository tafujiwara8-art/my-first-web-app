using ClipFlow.Core.History;

namespace ClipFlow.Core.Tests.Fakes;

public sealed class FakeHistoryRepository : IHistoryRepository
{
    public List<HistoryEntry> Saved { get; } = [];

    public int SaveCallCount => Saved.Count;

    public Task SaveAsync(HistoryEntry entry, CancellationToken ct)
    {
        Saved.Add(entry);
        return Task.CompletedTask;
    }
}
