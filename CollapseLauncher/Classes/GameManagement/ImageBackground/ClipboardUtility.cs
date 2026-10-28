using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Storage.Streams;

// ReSharper disable CommentTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public static class ClipboardUtility
{
    public static async void CopyCurrentFrameToClipboard(
        ImageBackgroundManager? instance,
        FrameToCopyType         frameToCopyType)
    {
        try
        {
            if (instance == null)
            {
                return;
            }

            bool hasBackground = frameToCopyType.HasFlag(FrameToCopyType.Background);
            bool hasForeground = frameToCopyType.HasFlag(FrameToCopyType.Foreground);

            // Try to get video frame buffer first (if the background is video).
            LayeredBackgroundImage? currentElement = instance.CurrentBackgroundElement;
            if (currentElement == null)
                return;

            using CanvasRenderTarget? sourceFrameCanvas = await currentElement.CopyCurrentFrame(hasBackground, hasForeground);
            await CopyCanvasTargetBufferToClipboard(sourceFrameCanvas); // Copy to clipboard
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[ImageBackgroundManager::CopyCurrentFrameToClipboard] An error occurred while trying to copy current background frame to clipboard\r\n{ex}",
                                LogType.Error,
                                true);
        }
    }

    private static double GetBitmapBitSize(DirectXPixelFormat pixelFormat)
    {
        const string r4Chan  = "R4";
        const string r8Chan  = "R8";
        const string r10Chan = "R10";
        const string r16Chan = "R16";
        const string r24Chan = "R24";
        const string r32Chan = "R32";

        ReadOnlySpan<char> enumStr = GetPixelFormatString(pixelFormat);
        
        int  bitSize = 8;
        bool isSkip  = enumStr.IndexOf(r8Chan, StringComparison.Ordinal) > 0;

        if (!isSkip && enumStr.IndexOf(r4Chan, StringComparison.Ordinal) > 0)
        {
            isSkip  = true;
            bitSize = 4;
        }

        if (!isSkip && enumStr.IndexOf(r10Chan, StringComparison.Ordinal) > 0)
        {
            isSkip  = true;
            bitSize = 10;
        }

        if (!isSkip && enumStr.IndexOf(r16Chan, StringComparison.Ordinal) > 0)
        {
            isSkip  = true;
            bitSize = 16;
        }

        if (!isSkip && enumStr.IndexOf(r24Chan, StringComparison.Ordinal) > 0)
        {
            isSkip  = true;
            bitSize = 24;
        }

        // ReSharper disable once InvertIf
        if (!isSkip && enumStr.IndexOf(r32Chan, StringComparison.Ordinal) > 0)
        {
            isSkip  = true;
            bitSize = 32;
        }

        return !isSkip
            ? throw new InvalidOperationException($"Pixel format enum {pixelFormat} is not supported!")
            : bitSize;
    }

    private static ReadOnlySpan<char> GetPixelFormatString(DirectXPixelFormat pixelFormat)
    {
        if (pixelFormat == DirectXPixelFormat.Unknown)
        {
            throw new InvalidOperationException("Unknown pixel format is not supported!");
        }

        string? enumS = Enum.GetName(pixelFormat);
        return string.IsNullOrEmpty(enumS)
            ? throw new InvalidOperationException($"Pixel format enum {pixelFormat} is not supported!")
            : enumS;
    }

    private static int GetBitmapChannelCount(DirectXPixelFormat pixelFormat)
    {
        ReadOnlySpan<char> enumStr = GetPixelFormatString(pixelFormat);

        int indexOfAlphaSign = enumStr.IndexOf('A');
        int channelNum       = 3;
        if (indexOfAlphaSign != -1 &&
            enumStr.Length > indexOfAlphaSign &&
            int.TryParse(enumStr.Slice(indexOfAlphaSign + 1, 1), out _))
        {
            channelNum = 4;
        }

        return channelNum;
    }

    private static int GetBitmapStrideSize(Direct3DSurfaceDescription desc)
    {
        DirectXPixelFormat pixelFormat = desc.Format;
        double             bitSize     = GetBitmapBitSize(pixelFormat);
        int                channelNum  = GetBitmapChannelCount(pixelFormat);

        double stride = desc.Width * desc.Height * channelNum * (bitSize / 8d);
        return (int)Math.Round(stride);
    }

    private static async Task<int> WriteCanvasTargetBufferType(
        CanvasRenderTarget     sourceCanvasTarget,
        byte[]                 buffer,
        CanvasBitmapFileFormat format)
    {
        using MemoryStream        bufferStream       = new(buffer);
        using IRandomAccessStream bufferRandomStream = bufferStream.AsRandomAccessStream(true);
        await sourceCanvasTarget.SaveAsync(bufferRandomStream, format, 1f);

        return (int)bufferStream.Position;
    }

    private static async Task CopyCanvasTargetBufferToClipboard(CanvasRenderTarget? sourceCanvasTarget)
    {
        if (sourceCanvasTarget == null)
            return;

        bool    isClipboardOpened = true;
        byte[]? buffer            = null;
        try
        {
            ILogger logger = ILoggerHelper.GetILogger("ClipboardUtility::CopyCanvasTargetBufferToClipboard");

            Direct3DSurfaceDescription desc          = sourceCanvasTarget.Description;
            int                        bmpHeaderSize = sizeof(BITMAPHEADER);
            int                        strideSize    = GetBitmapStrideSize(desc);
            int                        bufferSize    = strideSize + bmpHeaderSize;
            buffer = BigArrayPool<byte>.Shared.Rent(bufferSize);

            // Try open the Clipboard
            if (!PInvoke.OpenClipboard(nint.Zero))
            {
                isClipboardOpened = false;
                logger.LogError("[ClipboardUtility::CopyCanvasTargetBufferToClipboard] Cannot open the clipboard! Error: {}", Win32Error.GetLastWin32ErrorMessage());
                return;
            }

            // Make sure to empty the clipboard before setting and allocate the content
            if (!PInvoke.EmptyClipboard())
            {
                logger.LogError("[ClipboardUtility::CopyCanvasTargetBufferToClipboard] Cannot clear the clipboard! Error: {}", Win32Error.GetLastWin32ErrorMessage());
            }

            // Write and load the "load" ( ͡° ͜ʖ ͡°)
            // -- Copy as PNG
            StandardClipboardFormats pngFormat = (StandardClipboardFormats)PInvoke.RegisterClipboardFormat("PNG");
            int writtenPng = await WriteCanvasTargetBufferType(sourceCanvasTarget, buffer, CanvasBitmapFileFormat.Png);
            await Task.Run(() => Clipboard.CopyDataToClipboard(buffer.AsSpan(0, writtenPng), pngFormat, logger));

            // -- Copy as BMP
            // Prepare BITMAPV5HEADER
            BITMAPHEADER header = default;
            header.Init();

            ref BITMAPV5HEADER dibHeader = ref header.TryReadAs<BITMAPV5HEADER>();

            int    bmpChannelCount  = GetBitmapChannelCount(desc.Format);
            double bmpBitPerChannel = GetBitmapBitSize(desc.Format);
            ushort bmpBitCount      = (ushort)(bmpBitPerChannel * bmpChannelCount);

            dibHeader.V5Width     = desc.Width;
            dibHeader.V5Height    = desc.Height;
            dibHeader.V5Planes    = 1;
            dibHeader.V5BitCount  = bmpBitCount;
            dibHeader.V5SizeImage = (uint)strideSize;
            dibHeader.V5RedMask   = 0x00FF0000;
            dibHeader.V5GreenMask = 0x0000FF00;
            dibHeader.V5BlueMask  = 0x000000FF;
            dibHeader.V5AlphaMask = bmpChannelCount == 4 ? 0xFF000000 : 0;
            dibHeader.V5CSType    = 0x73524742; // 'sRGB'

            await Task.Run(ImplWriteBmp);
            void ImplWriteBmp()
            {
                IBuffer windowsBuffer = buffer.AsBuffer(bmpHeaderSize, strideSize, buffer.Length - bmpHeaderSize);
                sourceCanvasTarget.GetPixelBytes(windowsBuffer);

                int dataSize = (int)(sizeof(BITMAPHEADER) + windowsBuffer.Length);
                header.File.Size = (uint)dataSize;

                Clipboard.CopyDataToClipboard(buffer.AsSpan(0, dataSize), StandardClipboardFormats.CF_DIBV5, logger);
            }
        }
        finally
        {
            if (isClipboardOpened)
            {
                // Close the clipboard
                PInvoke.CloseClipboard();
            }

            if (buffer != null)
            {
                BigArrayPool<byte>.Shared.Return(buffer);
            }

            // Dispose the source canvas
            sourceCanvasTarget.Dispose();
        }
    }
    private static unsafe ref T AsRef<T>(Span<byte> span) => ref Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.AsRef<byte>(span)));
}
