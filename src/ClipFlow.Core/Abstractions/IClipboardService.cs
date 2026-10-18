using ClipFlow.Core.Imaging;

namespace ClipFlow.Core.Abstractions;

/// <param name="ExcludeFromHistory">
/// true のとき Windows のクリップボード履歴（Win+V）・クラウド同期・監視アプリから除外する形式を同時にセットする。
/// </param>
public sealed record ClipboardWriteOptions(bool ExcludeFromHistory);

/// <summary>
/// クリップボード書き込み。P2 で読み取り（Read）と変更監視イベントを追加する予定のため、
/// 書き込み専用の操作だけをここに置き、読み取り系は別メンバーとして追加できる形にしている。
/// </summary>
public interface IClipboardService
{
    void WriteImage(RawImage image, ClipboardWriteOptions options);
}
