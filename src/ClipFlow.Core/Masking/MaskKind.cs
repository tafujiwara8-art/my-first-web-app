namespace ClipFlow.Core.Masking;

public enum MaskKind
{
    /// <summary>不透明な単色での画素置換。ClipFlow が推奨する唯一の秘匿手段。</summary>
    SolidFill,

    /// <summary>モザイク。推測・復元のリスクがあり、秘匿性を保証しない。</summary>
    Mosaic,

    /// <summary>ぼかし。推測・復元のリスクがあり、秘匿性を保証しない。</summary>
    Blur,
}
