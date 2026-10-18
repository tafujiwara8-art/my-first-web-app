namespace ClipFlow.Core.IO;

/// <summary>ClipFlow が使うローカルフォルダ。</summary>
public sealed class AppPaths(string root)
{
    public string Root { get; } = root ?? throw new ArgumentNullException(nameof(root));

    public string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>
    /// 一時フォルダ。P1 は画像データを一時ファイルに書かない。将来やむを得ず書く場合もここに限定し、
    /// 起動時に <see cref="CleanTempDirectory"/> で無条件に削除する。
    /// </summary>
    public string TempDirectory => Path.Combine(Root, "tmp");

    public static AppPaths ForCurrentUser() =>
        new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClipFlow"));

    /// <returns>削除に失敗した場合はその例外。成功・フォルダなしは null。</returns>
    public Exception? CleanTempDirectory()
    {
        try
        {
            if (Directory.Exists(TempDirectory))
            {
                Directory.Delete(TempDirectory, recursive: true);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex;
        }
    }
}
