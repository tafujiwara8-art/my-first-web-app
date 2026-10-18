using System.Windows;
using System.Windows.Controls.Primitives;
using ClipFlow.App.Common;
using ClipFlow.Core.Settings;

namespace ClipFlow.App.Launcher;

/// <summary>フローティングバッジによるランチャー（P1 の簡易版）。</summary>
internal sealed class BadgeLauncher(Func<ClipFlowSettings> settings, Func<bool> isSecret, Action<BadgePosition> savePosition) : ILauncher
{
    private const double Margin = 16;
    private BadgeWindow? _window;

    public event Action<AppCommand>? CommandRequested;

    public bool IsVisible => _window?.IsVisible == true;

    public void Show()
    {
        if (_window is null)
        {
            _window = new BadgeWindow();
            _window.Clicked += ShowMenu;
            _window.Moved += () => savePosition(new BadgePosition(_window.Left, _window.Top));
            var (left, top) = InitialPosition(settings().BadgePosition);
            _window.Left = left;
            _window.Top = top;
        }

        _window.SetSecretMode(isSecret());
        _window.Show();
    }

    public void Hide() => _window?.Hide();

    public void SetSecretMode(bool secret) => _window?.SetSecretMode(secret);

    public void Dispose()
    {
        _window?.Close();
        _window = null;
    }

    // 保存位置が現在の仮想スクリーン外（モニター構成の変更など）なら既定位置（プライマリモニター右下、タスクバーの上）に戻す。
    private static (double Left, double Top) InitialPosition(BadgePosition? saved)
    {
        var vLeft = SystemParameters.VirtualScreenLeft;
        var vTop = SystemParameters.VirtualScreenTop;
        if (saved is { } p &&
            p.Left >= vLeft && p.Top >= vTop &&
            p.Left + BadgeWindow.BadgeSize <= vLeft + SystemParameters.VirtualScreenWidth &&
            p.Top + BadgeWindow.BadgeSize <= vTop + SystemParameters.VirtualScreenHeight)
        {
            return (p.Left, p.Top);
        }

        var work = SystemParameters.WorkArea;
        return (work.Right - BadgeWindow.BadgeSize - Margin, work.Bottom - BadgeWindow.BadgeSize - Margin);
    }

    private void ShowMenu()
    {
        if (_window is null)
        {
            return;
        }

        var menu = MenuFactory.Create(c => CommandRequested?.Invoke(c), settings(), isSecret(), badgeVisible: null);
        menu.PlacementTarget = _window;
        menu.Placement = PlacementMode.Top;
        menu.IsOpen = true;
    }
}
