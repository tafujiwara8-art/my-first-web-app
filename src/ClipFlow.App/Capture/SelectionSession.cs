using ClipFlow.Core.Abstractions;
using ClipFlow.Core.History;
using ClipFlow.Core.Imaging;
using ClipFlow.Windows;

namespace ClipFlow.App.Capture;

/// <summary>
/// 全モニターに 1 枚ずつオーバーレイを出して矩形選択を受け付ける。
/// 座標はカーソル位置（物理ピクセル）で追跡するため、DPI の異なるモニターをまたいでも選択がずれない。
/// </summary>
internal sealed class SelectionSession
{
    private readonly List<SelectionOverlayWindow> _windows = [];
    private readonly TaskCompletionSource<PixelRect?> _result = new();
    private readonly SecretModeState _secret;
    private PixelPoint? _anchor;
    private PixelPoint _current;

    private SelectionSession(SecretModeState secret) => _secret = secret;

    public PixelRect? Selection => _anchor is { } a ? PixelRect.FromPoints(a.X, a.Y, _current.X, _current.Y) : null;

    public PixelPoint Cursor => _current;

    /// <returns>選択した矩形（仮想スクリーンの物理ピクセル）。中止した場合は null。</returns>
    public static Task<PixelRect?> RunAsync(IReadOnlyList<MonitorInfo> monitors, SecretModeState secret, string hint)
    {
        var session = new SelectionSession(secret);
        session.Start(monitors, hint);
        return session._result.Task;
    }

    internal void OnMouseDown(SelectionOverlayWindow window)
    {
        _current = ScreenUtilities.GetCursorPosition();
        _anchor = _current;
        window.CaptureMouse();
        Render();
    }

    internal void OnMouseMove()
    {
        _current = ScreenUtilities.GetCursorPosition();
        Render();
    }

    internal void OnMouseUp(SelectionOverlayWindow window)
    {
        if (_anchor is null)
        {
            return;
        }

        _current = ScreenUtilities.GetCursorPosition();
        window.ReleaseMouseCapture();
        var selection = Selection!.Value;
        if (selection.Width < 2 || selection.Height < 2)
        {
            // クリックだけの場合は選択をやり直す
            _anchor = null;
            Render();
            return;
        }

        Finish(selection);
    }

    internal void Cancel() => Finish(null);

    private void Start(IReadOnlyList<MonitorInfo> monitors, string hint)
    {
        _current = ScreenUtilities.GetCursorPosition();
        foreach (var monitor in monitors)
        {
            var window = new SelectionOverlayWindow(monitor, this, hint, _secret.IsSecret);
            window.Closed += (_, _) => Cancel();
            _windows.Add(window);
            window.Show();
        }

        _secret.Changed += OnSecretChanged;
        Render();

        // キーボード（Esc）を受け取るため、カーソルのあるモニターのオーバーレイをアクティブにする
        var underCursor = _windows.FirstOrDefault(w => w.Monitor.Bounds.Contains(_current.X, _current.Y)) ?? _windows.FirstOrDefault();
        underCursor?.Activate();
    }

    private void OnSecretChanged(bool recordingEnabled)
    {
        foreach (var w in _windows)
        {
            w.SetSecretBanner(!recordingEnabled);
        }
    }

    private void Render()
    {
        foreach (var w in _windows)
        {
            w.Render(Selection, _current);
        }
    }

    private void Finish(PixelRect? selection)
    {
        if (_result.Task.IsCompleted)
        {
            return;
        }

        _secret.Changed -= OnSecretChanged;
        _result.SetResult(selection);
        foreach (var w in _windows)
        {
            w.Close();
        }

        _windows.Clear();
    }
}
