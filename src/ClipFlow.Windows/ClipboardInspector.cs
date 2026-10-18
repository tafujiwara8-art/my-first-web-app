using System.Runtime.InteropServices;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

/// <summary>クリップボードの現在の内容を検査する（テスト・診断用。P2 の読み取り機能の土台）。</summary>
public static class ClipboardInspector
{
    public static bool HasFormat(string registeredName) =>
        IsClipboardFormatAvailable(Win32ClipboardService.RegisterFormat(registeredName));

    public static bool HasDibV5() => IsClipboardFormatAvailable(CF_DIBV5);

    /// <summary>登録形式のデータを取り出す。存在しなければ null。</summary>
    public static byte[]? ReadFormat(string registeredName) => Read(Win32ClipboardService.RegisterFormat(registeredName));

    public static byte[]? ReadDibV5() => Read(CF_DIBV5);

    private static byte[]? Read(uint format)
    {
        using var session = Win32ClipboardService.ClipboardSession.Open(0);
        var handle = GetClipboardData(format);
        if (handle == 0)
        {
            return null;
        }

        var size = (int)GlobalSize(handle);
        var ptr = GlobalLock(handle);
        if (ptr == 0)
        {
            return null;
        }

        try
        {
            var bytes = new byte[size];
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }
}
