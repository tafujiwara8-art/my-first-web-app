using ClipFlow.Core.Imaging;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

public enum TrayNotificationKind
{
    Info,
    Warning,
}

/// <summary>
/// <c>Shell_NotifyIcon</c> の直接実装による通知領域アイコン。UI スレッドで作成すること。
/// エクスプローラー再起動（TaskbarCreated）時は自動で再登録する。
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const uint CallbackMessage = WM_APP + 1;

    private readonly MessageWindow _window = new(messageOnly: false);
    private readonly uint _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    private NativeIcon? _icon;
    private string _tooltip;
    private bool _added;

    public TrayIcon(RawImage icon, string tooltip)
    {
        _tooltip = tooltip;
        _icon = NativeIcon.FromImage(icon);
        _window.MessageReceived += OnMessage;
        Add();
    }

    /// <summary>左クリック（または選択キー）。</summary>
    public event Action? Clicked;

    /// <summary>右クリック等でコンテキストメニューが要求された。</summary>
    public event Action? ContextMenuRequested;

    /// <summary>メニュー表示前にフォアグラウンドにするウィンドウ（外側クリックでメニューを閉じるため）。</summary>
    public nint WindowHandle => _window.Handle;

    public void SetIcon(RawImage icon)
    {
        var old = _icon;
        _icon = NativeIcon.FromImage(icon);
        Modify(NIF_ICON);
        old?.Dispose();
    }

    public void SetTooltip(string tooltip)
    {
        _tooltip = tooltip;
        Modify(NIF_TIP | NIF_SHOWTIP);
    }

    public unsafe void ShowNotification(string title, string text, TrayNotificationKind kind = TrayNotificationKind.Info)
    {
        var data = CreateData(NIF_INFO);
        Copy(title, data.szInfoTitle, 64);
        Copy(text, data.szInfo, 256);
        data.dwInfoFlags = kind == TrayNotificationKind.Warning ? NIIF_WARNING : NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    /// <summary>メニュー表示の直前に呼ぶ。</summary>
    public void ActivateForMenu() => SetForegroundWindow(_window.Handle);

    public void Dispose()
    {
        if (_added)
        {
            var data = CreateData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }

        _icon?.Dispose();
        _icon = null;
        _window.Dispose();
    }

    private static unsafe void Copy(string value, char* destination, int capacity)
    {
        var span = new Span<char>(destination, capacity);
        span.Clear();
        value.AsSpan(0, Math.Min(value.Length, capacity - 1)).CopyTo(span);
    }

    private void Add()
    {
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
        if (_added)
        {
            data.uVersion = NOTIFYICON_VERSION_4;
            Shell_NotifyIcon(NIM_SETVERSION, ref data);
        }
    }

    private void Modify(uint flags)
    {
        var data = CreateData(flags);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private unsafe NOTIFYICONDATAW CreateData(uint flags)
    {
        var data = new NOTIFYICONDATAW
        {
            cbSize = (uint)sizeof(NOTIFYICONDATAW),
            hWnd = _window.Handle,
            uID = IconId,
            uFlags = flags,
            uCallbackMessage = CallbackMessage,
            hIcon = _icon?.Handle ?? 0,
        };
        Copy(_tooltip, data.szTip, 128);
        return data;
    }

    private bool OnMessage(uint message, nint wParam, nint lParam)
    {
        if (message == _taskbarCreated)
        {
            Add();
            return true;
        }

        if (message != CallbackMessage)
        {
            return false;
        }

        // NOTIFYICON_VERSION_4: LOWORD(lParam) がイベント。
        switch ((uint)(lParam & 0xFFFF))
        {
            case NIN_SELECT:
            case NIN_KEYSELECT:
                Clicked?.Invoke();
                break;
            case WM_CONTEXTMENU:
                ContextMenuRequested?.Invoke();
                break;
        }

        return true;
    }
}
