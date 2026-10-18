namespace ClipFlow.Core.History;

/// <summary>シークレットモードの状態。false の間、履歴への書き込みは行われない。</summary>
public interface IHistoryGate
{
    bool IsRecordingEnabled { get; }

    event Action<bool> Changed;
}

public enum HistoryEntryKind
{
    MaskedImageCopied,
    MaskedImageSaved,
    CutImageCopied,
}

/// <summary>履歴 1 件。P1 では画素データを持たない（P2 の SQLite 実装で拡張する）。</summary>
public sealed record HistoryEntry(DateTimeOffset Timestamp, HistoryEntryKind Kind, int Width, int Height);

/// <summary>履歴の永続化。P1 は <see cref="NoOpHistoryRepository"/> のみ。</summary>
public interface IHistoryRepository
{
    Task SaveAsync(HistoryEntry entry, CancellationToken ct);
}

public sealed class NoOpHistoryRepository : IHistoryRepository
{
    public Task SaveAsync(HistoryEntry entry, CancellationToken ct) => Task.CompletedTask;
}
