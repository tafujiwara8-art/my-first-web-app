namespace ClipFlow.Core.History;

/// <summary>
/// 履歴書き込みの唯一の入口。<see cref="IHistoryGate.IsRecordingEnabled"/> が false のときは
/// リポジトリを呼ばない。UI 側はこのクラスだけを使い、個別にシークレット判定をしない。
/// </summary>
public sealed class GatedHistoryWriter(IHistoryGate gate, IHistoryRepository repository)
{
    private readonly IHistoryGate _gate = gate ?? throw new ArgumentNullException(nameof(gate));
    private readonly IHistoryRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    /// <returns>リポジトリに渡した場合 true、ゲートで止めた場合 false。</returns>
    public async Task<bool> WriteAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!_gate.IsRecordingEnabled)
        {
            return false;
        }

        await _repository.SaveAsync(entry, ct).ConfigureAwait(false);
        return true;
    }
}
