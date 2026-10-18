using ClipFlow.App.Common;

namespace ClipFlow.App.Launcher;

/// <summary>
/// ランチャー。P1 はフローティングバッジ（クリックでメニュー）。P5 のジェスチャー版はこのインターフェースの差し替えで追加する。
/// </summary>
internal interface ILauncher : IDisposable
{
    event Action<AppCommand>? CommandRequested;

    bool IsVisible { get; }

    void Show();

    void Hide();

    void SetSecretMode(bool isSecret);
}
