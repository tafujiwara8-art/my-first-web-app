using System.Buffers.Binary;

namespace ClipFlow.Core.Imaging;

/// <summary>PNG のチャンク列を解析し、許可したチャンク以外を除去する。</summary>
public static class PngChunkFilter
{
    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>出力に残してよいチャンク。テキスト・EXIF・時刻・物理寸法・ICC 等は含めない。</summary>
    public static IReadOnlySet<string> AllowedChunks { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "IHDR", "sRGB", "gAMA", "IDAT", "IEND",
    };

    public static byte[] Strip(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        using var output = new MemoryStream(png.Length);
        output.Write(Signature);
        foreach (var chunk in ReadChunks(png))
        {
            if (AllowedChunks.Contains(chunk.Type))
            {
                output.Write(png, chunk.Offset, chunk.TotalLength);
            }
        }

        return output.ToArray();
    }

    /// <summary>チャンク種別を出現順に列挙する。</summary>
    public static IReadOnlyList<string> ListChunkTypes(byte[] png) => [.. ReadChunks(png).Select(c => c.Type)];

    private static List<Chunk> ReadChunks(byte[] png)
    {
        if (png.Length < Signature.Length || !png.AsSpan(0, Signature.Length).SequenceEqual(Signature))
        {
            throw new InvalidDataException("PNG シグネチャがありません。");
        }

        var chunks = new List<Chunk>();
        var pos = Signature.Length;
        while (pos + 12 <= png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos, 4));
            if (length < 0 || pos + 12L + length > png.Length)
            {
                throw new InvalidDataException("PNG チャンク長が不正です。");
            }

            var type = System.Text.Encoding.ASCII.GetString(png, pos + 4, 4);
            chunks.Add(new Chunk(type, pos, length + 12));
            pos += length + 12;
            if (type == "IEND")
            {
                break;
            }
        }

        return chunks;
    }

    private readonly record struct Chunk(string Type, int Offset, int TotalLength);
}
