using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Annotations;

namespace ClipFlow.Core.Settings;

public sealed record HotkeyBinding(Modifiers Modifiers, uint VirtualKey)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(Modifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(Modifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(Modifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(Modifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(KeyName(VirtualKey));
        return string.Join("+", parts);
    }

    private static string KeyName(uint vk) => vk switch
    {
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => $"F{vk - 0x6F}",
        _ => $"0x{vk:X2}",
    };
}

public sealed record BadgePosition(double Left, double Top);

/// <summary>ユーザー設定。%LOCALAPPDATA%\ClipFlow\settings.json に保存する。</summary>
public sealed record ClipFlowSettings
{
    public HotkeyBinding CaptureAndMaskHotkey { get; init; } = new(Modifiers.Control | Modifiers.Shift, 'M');

    public HotkeyBinding CutCopyHotkey { get; init; } = new(Modifiers.Control | Modifiers.Shift, 'X');

    public HotkeyBinding ToggleSecretHotkey { get; init; } = new(Modifiers.Control | Modifiers.Shift, 'S');

    /// <summary>通常モードでも Windows のクリップボード履歴・クラウド同期から除外する（既定 ON）。シークレットモード中は常に除外。</summary>
    public bool ExcludeFromWindowsHistoryInNormalMode { get; init; } = true;

    public bool BadgeVisible { get; init; } = true;

    /// <summary>バッジ位置（WPF 論理座標）。null は既定位置（プライマリモニター右下）。</summary>
    public BadgePosition? BadgePosition { get; init; }

    public string AnnotationColor { get; init; } = Annotation.DefaultColorHex;

    public int AnnotationLineWidth { get; init; } = Annotation.DefaultLineWidth;

    public int JpegQuality { get; init; } = 90;

    /// <summary>JPEG 出力時に黒塗りを 16 画素境界へ外側に丸める（既定 ON）。</summary>
    public bool RoundSolidFillsForJpeg { get; init; } = true;

    public HotkeyBinding GetHotkey(HotkeyId id) => id switch
    {
        HotkeyId.CaptureAndMask => CaptureAndMaskHotkey,
        HotkeyId.CutCopy => CutCopyHotkey,
        HotkeyId.ToggleSecretMode => ToggleSecretHotkey,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };
}
