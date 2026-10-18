namespace ClipFlow.Core.Abstractions;

public enum HotkeyId
{
    CaptureAndMask = 1,
    CutCopy = 2,
    ToggleSecretMode = 3,
}

[Flags]
public enum Modifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Win = 0x8,
}

public interface IHotkeyService
{
    /// <summary>グローバルホットキーを登録する。他アプリと衝突した場合は false を返す。</summary>
    bool TryRegister(HotkeyId id, Modifiers mods, uint vk, Action handler);

    void Unregister(HotkeyId id);
}
