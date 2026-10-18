using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ClipFlow.Core.Abstractions;
using ClipFlow.Core.Imaging;
using static ClipFlow.Windows.Interop.NativeMethods;

namespace ClipFlow.Windows;

/// <summary>
/// Win32 クリップボードへの画像書き込み。<c>PNG</c>（登録形式）と <c>CF_DIBV5</c> の両方をセットし、
/// <see cref="ClipboardWriteOptions.ExcludeFromHistory"/> が true なら履歴・クラウド同期の除外形式も同時にセットする。
/// </summary>
public sealed class Win32ClipboardService : IClipboardService, IDisposable
{
    private const int OpenRetryCount = 10;
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromMilliseconds(30);

    // EmptyClipboard 後に SetClipboardData するにはオーナーウィンドウが必要。
    private readonly MessageWindow _owner = new(messageOnly: true);

    public void WriteImage(RawImage image, ClipboardWriteOptions options)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        var png = ImageCodec.EncodePng(image);
        var dib = BuildDibV5(image);
        try
        {
            using var session = ClipboardSession.Open(_owner.Handle);
            if (!EmptyClipboard())
            {
                throw new Win32Exception(Marshal.GetLastPInvokeError());
            }

            SetData(RegisterFormat(ClipboardFormatNames.Png), png);
            SetData(CF_DIBV5, dib);
            if (options.ExcludeFromHistory)
            {
                byte[] zero = [0, 0, 0, 0];
                SetData(RegisterFormat(ClipboardFormatNames.ExcludeFromMonitorProcessing), zero);
                SetData(RegisterFormat(ClipboardFormatNames.CanIncludeInClipboardHistory), zero);
                SetData(RegisterFormat(ClipboardFormatNames.CanUploadToCloudClipboard), zero);
            }
        }
        finally
        {
            // マネージド側の一時バッファを消す（クリップボードに渡したコピーは OS の管理下）。
            Array.Clear(dib);
            Array.Clear(png);
        }
    }

    public void Dispose() => _owner.Dispose();

    internal static uint RegisterFormat(string name)
    {
        var id = RegisterClipboardFormat(name);
        return id != 0 ? id : throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>BITMAPV5HEADER（BI_RGB、32bpp、ボトムアップ）+ 画素。</summary>
    internal static byte[] BuildDibV5(RawImage image)
    {
        const int headerSize = 124;
        var stride = image.Stride;
        var data = new byte[headerSize + (stride * image.Height)];
        var h = data.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(h[0..], headerSize);
        BinaryPrimitives.WriteInt32LittleEndian(h[4..], image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(h[8..], image.Height); // 正 = ボトムアップ（互換性が高い）
        BinaryPrimitives.WriteUInt16LittleEndian(h[12..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(h[14..], 32);
        BinaryPrimitives.WriteUInt32LittleEndian(h[16..], BI_RGB);
        BinaryPrimitives.WriteUInt32LittleEndian(h[20..], (uint)(stride * image.Height));
        BinaryPrimitives.WriteUInt32LittleEndian(h[40..], 0x00FF0000); // R mask
        BinaryPrimitives.WriteUInt32LittleEndian(h[44..], 0x0000FF00); // G mask
        BinaryPrimitives.WriteUInt32LittleEndian(h[48..], 0x000000FF); // B mask
        BinaryPrimitives.WriteUInt32LittleEndian(h[52..], 0xFF000000); // A mask
        BinaryPrimitives.WriteUInt32LittleEndian(h[56..], 0x73524742); // LCS_sRGB 'sRGB'
        BinaryPrimitives.WriteUInt32LittleEndian(h[108..], 4);         // LCS_GM_IMAGES
        for (var y = 0; y < image.Height; y++)
        {
            Buffer.BlockCopy(image.Pixels, y * stride, data, headerSize + ((image.Height - 1 - y) * stride), stride);
        }

        return data;
    }

    private static void SetData(uint format, byte[] bytes)
    {
        var hGlobal = GlobalAlloc(GMEM_MOVEABLE, (nuint)bytes.Length);
        if (hGlobal == 0)
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        var ptr = GlobalLock(hGlobal);
        if (ptr == 0)
        {
            GlobalFree(hGlobal);
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        GlobalUnlock(hGlobal);
        if (SetClipboardData(format, hGlobal) == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            GlobalFree(hGlobal);
            throw new Win32Exception(error);
        }

        // 成功後は所有権がシステムに移るため解放しない。
    }

    internal readonly struct ClipboardSession : IDisposable
    {
        public static ClipboardSession Open(nint owner)
        {
            for (var i = 0; ; i++)
            {
                if (OpenClipboard(owner))
                {
                    return default;
                }

                if (i >= OpenRetryCount)
                {
                    throw new Win32Exception(Marshal.GetLastPInvokeError(), "クリップボードを開けませんでした（他のアプリが使用中の可能性があります）。");
                }

                Thread.Sleep(OpenRetryDelay);
            }
        }

        public void Dispose() => CloseClipboard();
    }
}
