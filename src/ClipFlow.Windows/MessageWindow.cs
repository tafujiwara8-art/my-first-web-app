using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ClipFlow.Windows.Interop;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

/// <summary>
/// メッセージ受信専用の非表示ウィンドウ。作成したスレッドのメッセージループ（WPF の Dispatcher 等）で
/// メッセージが配送されるため、UI スレッドで作成すること。
/// </summary>
public sealed class MessageWindow : IDisposable
{
    public delegate bool MessageHandler(uint message, nint wParam, nint lParam);

    private const string ClassName = "ClipFlow.MessageWindow";
    private static readonly ConcurrentDictionary<nint, MessageWindow> Windows = new();
    private static readonly Lock ClassLock = new();
    private static bool _classRegistered;

    /// <param name="messageOnly">
    /// true なら HWND_MESSAGE 配下に作る。ブロードキャスト（TaskbarCreated 等）を受け取る必要がある場合は false。
    /// </param>
    public MessageWindow(bool messageOnly)
    {
        EnsureClassRegistered();
        Handle = CreateWindowEx(0, ClassName, "ClipFlow", 0, 0, 0, 0, 0, messageOnly ? HWND_MESSAGE : 0, 0, GetModuleHandle(null), 0);
        if (Handle == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        Windows[Handle] = this;
    }

    public event MessageHandler? MessageReceived;

    public nint Handle { get; private set; }

    public void Dispose()
    {
        if (Handle == 0)
        {
            return;
        }

        Windows.TryRemove(Handle, out _);
        DestroyWindow(Handle);
        Handle = 0;
    }

    private static unsafe void EnsureClassRegistered()
    {
        lock (ClassLock)
        {
            if (_classRegistered)
            {
                return;
            }

            // クラス名はプロセス終了まで解放しない。
            var name = (char*)Marshal.StringToHGlobalUni(ClassName);
            var wc = new WNDCLASSEXW
            {
                cbSize = (uint)sizeof(WNDCLASSEXW),
                lpfnWndProc = &WndProc,
                hInstance = GetModuleHandle(null),
                lpszClassName = name,
            };
            if (RegisterClassEx(ref wc) == 0)
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            _classRegistered = true;
        }
    }

    [UnmanagedCallersOnly]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        if (Windows.TryGetValue(hwnd, out var window))
        {
            try
            {
                if (window.MessageReceived?.Invoke(msg, wParam, lParam) == true)
                {
                    return 0;
                }
            }
#pragma warning disable CA1031 // ネイティブ境界を例外が越えるとプロセスが落ちるため、ここで止める
            catch (Exception ex)
#pragma warning restore CA1031
            {
                UnhandledCallbackException?.Invoke(ex);
            }
        }

        return DefWindowProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>メッセージハンドラ内で発生した例外の通知先（App がログ・通知に使う）。</summary>
    public static event Action<Exception>? UnhandledCallbackException;
}
