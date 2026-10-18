using System.Collections.Immutable;
using ClipFlow.Core.Annotations;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;

namespace ClipFlow.Core.Editing;

/// <summary>
/// マスクエディタの編集状態と Undo/Redo。1 回のドラッグ操作は
/// <see cref="BeginChange"/> → <see cref="Set"/>（何度でも）→ <see cref="EndChange"/> で 1 手として記録する。
/// </summary>
public sealed class EditorDocument
{
    private readonly Stack<ImmutableList<EditorItem>> _undo = new();
    private readonly Stack<ImmutableList<EditorItem>> _redo = new();
    private ImmutableList<EditorItem>? _changeStart;

    public event Action? Changed;

    public ImmutableList<EditorItem> Items { get; private set; } = [];

    public bool CanUndo => _undo.Count > 0 && _changeStart is null;

    public bool CanRedo => _redo.Count > 0 && _changeStart is null;

    public bool IsLocked { get; private set; }

    public bool HasWeakMasks => Items.OfType<MaskItem>().Any(m => m.Kind != MaskKind.SolidFill);

    public EditorItem? Find(Guid id) => Items.FirstOrDefault(i => i.Id == id);

    /// <summary>上（後から追加したもの）から順に当たり判定する。</summary>
    public EditorItem? HitTest(PixelPoint point, int tolerance) =>
        Items.Reverse().FirstOrDefault(i => i.HitTest(point, tolerance));

    public void BeginChange()
    {
        ThrowIfLocked();
        _changeStart ??= Items;
    }

    /// <summary>同じ Id の要素を置き換える。なければ末尾に追加する。</summary>
    public void Set(EditorItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ThrowIfLocked();
        var index = Items.FindIndex(i => i.Id == item.Id);
        Items = index >= 0 ? Items.SetItem(index, item) : Items.Add(item);
        Changed?.Invoke();
    }

    public void Remove(Guid id)
    {
        ThrowIfLocked();
        Items = Items.RemoveAll(i => i.Id == id);
        Changed?.Invoke();
    }

    /// <summary>変更を確定する。内容が変わっていなければ履歴に積まない。</summary>
    public void EndChange()
    {
        if (_changeStart is null)
        {
            return;
        }

        if (!ReferenceEquals(_changeStart, Items) && !_changeStart.SequenceEqual(Items))
        {
            _undo.Push(_changeStart);
            _redo.Clear();
        }

        _changeStart = null;
    }

    /// <summary>進行中の変更を取り消して開始前に戻す。</summary>
    public void CancelChange()
    {
        if (_changeStart is null)
        {
            return;
        }

        Items = _changeStart;
        _changeStart = null;
        Changed?.Invoke();
    }

    /// <summary>1 回で完結する変更（削除・種別変更など）。</summary>
    public void Apply(Action<EditorDocument> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        BeginChange();
        change(this);
        EndChange();
    }

    public bool Undo()
    {
        if (!CanUndo || IsLocked)
        {
            return false;
        }

        _redo.Push(Items);
        Items = _undo.Pop();
        Changed?.Invoke();
        return true;
    }

    public bool Redo()
    {
        if (!CanRedo || IsLocked)
        {
            return false;
        }

        _undo.Push(Items);
        Items = _redo.Pop();
        Changed?.Invoke();
        return true;
    }

    /// <summary>確定後に呼ぶ。履歴を破棄し、以後の編集を禁止する。</summary>
    public void Lock()
    {
        CancelChange();
        _undo.Clear();
        _redo.Clear();
        IsLocked = true;
        Changed?.Invoke();
    }

    public IReadOnlyList<MaskRegion> BuildMasks() => [.. Items.OfType<MaskItem>().Select(m => m.ToRegion())];

    public IReadOnlyList<Annotation> BuildAnnotations() =>
        [.. Items.Select(i => i switch
        {
            FrameItem f => f.ToAnnotation(),
            ArrowItem a => a.ToAnnotation(),
            _ => null,
        }).OfType<Annotation>()];

    /// <summary>焼き込み。マスク → 注釈の順に適用した新しい画像を返す（元画像は変更しない）。</summary>
    public RawImage Compose(RawImage source) => MaskComposer.Compose(source, BuildMasks(), BuildAnnotations());

    private void ThrowIfLocked()
    {
        if (IsLocked)
        {
            throw new InvalidOperationException("確定済みのため編集できません。");
        }
    }
}
