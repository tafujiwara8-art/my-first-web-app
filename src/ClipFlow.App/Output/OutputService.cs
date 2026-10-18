using System.IO;
using System.Windows;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.History;
using ClipFlow.Core.Imaging;
using ClipFlow.Core.Masking;
using ClipFlow.Core.Settings;
using Microsoft.Win32;

namespace ClipFlow.App.Output;

/// <summary>
/// クリップボードへのコピーとファイル保存。マスク済み画像・切り取り画像の両方がこの経路を通る。
/// 画像データを一時ファイルには書かない。
/// </summary>
internal sealed class OutputService(IClipboardService clipboard, GatedHistoryWriter history, SecretModeState secret, Func<ClipFlowSettings> settings)
{
    /// <summary>Windows のクリップボード履歴・クラウド同期から除外するか。シークレットモード中は常に除外。</summary>
    public bool ExcludeFromWindowsHistory => secret.IsSecret || settings().ExcludeFromWindowsHistoryInNormalMode;

    public void CopyToClipboard(RawImage image, HistoryEntryKind kind)
    {
        clipboard.WriteImage(image, new ClipboardWriteOptions(ExcludeFromWindowsHistory));
        RecordHistory(kind, image);
    }

    /// <param name="masks">JPEG の 16 画素丸めに使うマスク（形状のみ。元画像は不要）。</param>
    /// <returns>保存したパス。キャンセル時は null。</returns>
    public string? SaveToFile(Window owner, RawImage image, IReadOnlyList<MaskRegion> masks, OutputFormat format, bool roundForJpeg)
    {
        var current = settings();
        var extension = format == OutputFormat.Jpeg ? ".jpg" : ".png";
        var dialog = new SaveFileDialog
        {
            Title = "画像を保存",
            FileName = $"ClipFlow_{DateTime.Now:yyyyMMdd_HHmmss}{extension}",
            DefaultExt = extension,
            Filter = format == OutputFormat.Jpeg ? "JPEG 画像 (*.jpg)|*.jpg;*.jpeg" : "PNG 画像 (*.png)|*.png",
            OverwritePrompt = true,
            // シークレットモード中は「最近使ったファイル」に登録しない（SHAddToRecentDocs も呼ばない）。
            AddToRecent = !secret.IsSecret,
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return null;
        }

        byte[] bytes;
        if (format == OutputFormat.Jpeg)
        {
            using var hardened = roundForJpeg ? MaskComposer.HardenSolidFillsForJpeg(image, masks) : image.Clone();
            bytes = ImageCodec.EncodeJpeg(hardened, current.JpegQuality);
        }
        else
        {
            bytes = ImageCodec.EncodePng(image);
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, bytes);
        }
        finally
        {
            Array.Clear(bytes);
        }

        RecordHistory(HistoryEntryKind.MaskedImageSaved, image);
        return dialog.FileName;
    }

    private void RecordHistory(HistoryEntryKind kind, RawImage image) =>
        // 判定は GatedHistoryWriter に一元化している（UI 側では分岐しない）。
        _ = history.WriteAsync(new HistoryEntry(DateTimeOffset.Now, kind, image.Width, image.Height));
}
