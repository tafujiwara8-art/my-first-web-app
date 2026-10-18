namespace ClipFlow.Core.Imaging;

/// <summary>BGRA32 の 1 画素。</summary>
public readonly record struct Bgra(byte B, byte G, byte R, byte A)
{
    public static Bgra Black { get; } = new(0, 0, 0, 255);

    public static Bgra White { get; } = new(255, 255, 255, 255);

    public static Bgra FromRgb(byte r, byte g, byte b) => new(b, g, r, 255);

    /// <summary>"#RRGGBB" または "#AARRGGBB" を解釈する。</summary>
    public static Bgra Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var s = hex.TrimStart('#');
        var value = Convert.ToUInt32(s, 16);
        return s.Length switch
        {
            6 => new((byte)value, (byte)(value >> 8), (byte)(value >> 16), 255),
            8 => new((byte)value, (byte)(value >> 8), (byte)(value >> 16), (byte)(value >> 24)),
            _ => throw new FormatException($"色の形式が不正です: {hex}"),
        };
    }

    public string ToHex() => A == 255 ? $"#{R:X2}{G:X2}{B:X2}" : $"#{A:X2}{R:X2}{G:X2}{B:X2}";
}
