using System.Windows.Threading;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.History;
using ClipFlow.Core.Imaging;
using ClipFlow.Windows;

namespace ClipFlow.App.Capture;

/// <summary>範囲選択 → 画面取得。同時に 1 つだけ実行する。</summary>
internal sealed class CaptureWorkflow(IScreenCapture capture, SecretModeState secret)
{
    private bool _busy;

    public async Task<RawImage?> SelectAndCaptureAsync(string hint)
    {
        if (_busy)
        {
            return null;
        }

        _busy = true;
        try
        {
            var monitors = capture.GetMonitors();
            if (monitors.Count == 0)
            {
                return null;
            }

            var selection = await SelectionSession.RunAsync(monitors, secret, hint);
            if (selection is not { } rect)
            {
                return null;
            }

            // オーバーレイ自身が写り込まないよう、閉じた後の再合成を待ってから取得する。
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            await Task.Delay(60);
            ScreenUtilities.WaitForComposition();
            return capture.Capture(rect);
        }
        finally
        {
            _busy = false;
        }
    }
}
