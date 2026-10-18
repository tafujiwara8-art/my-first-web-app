using ClipFlow.Core.Abstractions;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

/// <summary><c>RegisterHotKey</c> によるグローバルホットキー。UI スレッドで作成すること。</summary>
public sealed class Win32HotkeyService : IHotkeyService, IDisposable
{
    private readonly MessageWindow _window = new(messageOnly: true);
    private readonly Dictionary<int, Action> _handlers = [];

    public Win32HotkeyService()
    {
        _window.MessageReceived += OnMessage;
    }

    public bool TryRegister(HotkeyId id, Modifiers mods, uint vk, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        Unregister(id);
        // Modifiers の値は MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN と一致させている。
        if (!RegisterHotKey(_window.Handle, (int)id, (uint)mods | MOD_NOREPEAT, vk))
        {
            return false;
        }

        _handlers[(int)id] = handler;
        return true;
    }

    public void Unregister(HotkeyId id)
    {
        if (_handlers.Remove((int)id))
        {
            UnregisterHotKey(_window.Handle, (int)id);
        }
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys.ToList())
        {
            UnregisterHotKey(_window.Handle, id);
        }

        _handlers.Clear();
        _window.Dispose();
    }

    private bool OnMessage(uint message, nint wParam, nint lParam)
    {
        if (message == WM_HOTKEY && _handlers.TryGetValue((int)wParam, out var handler))
        {
            handler();
            return true;
        }

        return false;
    }
}
