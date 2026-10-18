using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClipFlow.Core.Settings;

/// <summary>設定の JSON 読み書き。ファイルが壊れていれば既定値で起動する。</summary>
public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Path { get; } = path ?? throw new ArgumentNullException(nameof(path));

    public ClipFlowSettings Load()
    {
        try
        {
            if (!File.Exists(Path))
            {
                return new ClipFlowSettings();
            }

            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<ClipFlowSettings>(json, JsonOptions) ?? new ClipFlowSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ClipFlowSettings();
        }
    }

    public void Save(ClipFlowSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 書きかけのファイルを残さないよう、同じフォルダの一時ファイルに書いてから置き換える（画像は含まない）。
        var tempPath = Path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(tempPath, Path, overwrite: true);
    }
}
