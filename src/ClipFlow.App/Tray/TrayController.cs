using System.Windows.Controls.Primitives;
using ClipFlow.App.Common;
using ClipFlow.Core.Settings;
using ClipFlow.Windows;

namespace ClipFlow.App.Tray;

internal sealed class TrayController : IDisposable
{
    private readonly TrayIcon _icon;
    private readonly Action<AppCommand> _execute;
    private readonly Func<(ClipFlowSettings Settings, bool IsSecret, bool BadgeVisible)> _state;

    public TrayController(Action<AppCommand> execute, Func<(ClipFlowSettings Settings, bool IsSecret, bool BadgeVisible)> state)
    {
        _execute = execute;
        _state = state;
        using var image = IconRenderer.Render(secret: false);
        _icon = new TrayIcon(image, TooltipFor(false));
        _icon.Clicked += ShowMenu;
        _icon.ContextMenuRequested += ShowMenu;
    }

    public void SetSecretMode(bool isSecret)
    {
        using var image = IconRenderer.Render(isSecret);
        _icon.SetIcon(image);
        _icon.SetTooltip(TooltipFor(isSecret));
    }

    public void Notify(string title, string text, TrayNotificationKind kind = TrayNotificationKind.Info) =>
        _icon.ShowNotification(title, text, kind);

    public void Dispose() => _icon.Dispose();

    private static string TooltipFor(bool isSecret) => isSecret ? "ClipFlow（シークレットモード）" : "ClipFlow";

    private void ShowMenu()
    {
        var (settings, isSecret, badgeVisible) = _state();
        var menu = MenuFactory.Create(_execute, settings, isSecret, badgeVisible);
        menu.Placement = PlacementMode.MousePoint;
        // 外側をクリックしたときにメニューが閉じるよう、先にフォアグラウンドを取る
        _icon.ActivateForMenu();
        menu.IsOpen = true;
    }
}
