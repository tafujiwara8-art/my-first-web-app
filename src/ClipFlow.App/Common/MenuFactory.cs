using System.Windows.Controls;
using System.Windows.Threading;
using ClipFlow.Core.Settings;

namespace ClipFlow.App.Common;

/// <summary>トレイとバッジで共通のメニュー。</summary>
internal static class MenuFactory
{
    /// <param name="badgeVisible">null の場合は「バッジを表示」項目を出さない（バッジ自身のメニュー）。</param>
    public static ContextMenu Create(Action<AppCommand> execute, ClipFlowSettings settings, bool isSecret, bool? badgeVisible)
    {
        var menu = new ContextMenu();
        menu.Items.Add(Item("範囲キャプチャ → マスキング", settings.CaptureAndMaskHotkey.ToString(), AppCommand.CaptureAndMask));
        menu.Items.Add(Item("切り取りコピー（マスクなし）", settings.CutCopyHotkey.ToString(), AppCommand.CutCopy));
        var secret = Item("シークレットモード", settings.ToggleSecretHotkey.ToString(), AppCommand.ToggleSecretMode);
        secret.IsCheckable = true;
        secret.IsChecked = isSecret;
        menu.Items.Add(secret);
        menu.Items.Add(new Separator());
        if (badgeVisible is { } visible)
        {
            var badge = Item("ランチャーバッジを表示", null, AppCommand.ToggleBadge);
            badge.IsCheckable = true;
            badge.IsChecked = visible;
            menu.Items.Add(badge);
        }

        menu.Items.Add(Item("設定...", null, AppCommand.OpenSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("終了", null, AppCommand.Exit));
        return menu;

        MenuItem Item(string header, string? gesture, AppCommand command)
        {
            var item = new MenuItem { Header = header, InputGestureText = gesture ?? string.Empty };
            // メニューが閉じてから実行する（範囲選択オーバーレイにメニューが重ならないように）
            item.Click += (_, _) => menu.Dispatcher.BeginInvoke(DispatcherPriority.Background, () => execute(command));
            return item;
        }
    }
}
