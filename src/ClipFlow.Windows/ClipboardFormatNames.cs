namespace ClipFlow.Windows;

/// <summary>ClipFlow が書き込む登録クリップボード形式の名前。</summary>
public static class ClipboardFormatNames
{
    public const string Png = "PNG";

    /// <summary>クリップボード監視アプリ（Windows の履歴を含む）に処理させない。データ内容は参照されない。</summary>
    public const string ExcludeFromMonitorProcessing = "ExcludeClipboardContentFromMonitorProcessing";

    /// <summary>DWORD 0 で Windows のクリップボード履歴（Win+V）に含めない。</summary>
    public const string CanIncludeInClipboardHistory = "CanIncludeInClipboardHistory";

    /// <summary>DWORD 0 でクラウドクリップボード（デバイス間同期）にアップロードしない。</summary>
    public const string CanUploadToCloudClipboard = "CanUploadToCloudClipboard";
}
