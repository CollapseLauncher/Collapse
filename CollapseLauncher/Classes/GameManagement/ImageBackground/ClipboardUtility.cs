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
using System.Threading.Tasks;
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

    private static async Task WriteCanvasTargetBufferType(
        CanvasRenderTarget     sourceCanvasTarget,
        Stream                 targetStream,
        CanvasBitmapFileFormat format)
    {
        using IRandomAccessStream bufferRandomStream = targetStream.AsRandomAccessStream(true, true);
        await sourceCanvasTarget.SaveAsync(bufferRandomStream, format, 1f);
    }

    private static async Task CopyCanvasTargetBufferToClipboard(CanvasRenderTarget? sourceCanvasTarget)
    {
        if (sourceCanvasTarget == null)
            return;

        bool isClipboardOpened = true;
        try
        {
            ILogger logger = ILoggerHelper.GetILogger("ClipboardUtility::CopyCanvasTargetBufferToClipboard");

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
            StandardClipboardFormats pngFormat = (StandardClipboardFormats)PInvoke.RegisterClipboardFormat("PNG");
            using MemoryStream       pngStream = new();
            using MemoryStream       bmpStream = new();

            await WriteCanvasTargetBufferType(sourceCanvasTarget, pngStream, CanvasBitmapFileFormat.Png);
            await WriteCanvasTargetBufferType(sourceCanvasTarget, bmpStream, CanvasBitmapFileFormat.Bmp);

            byte[]           bmpBuffer      = bmpStream.GetBuffer();
            ref BITMAPHEADER bmpHeader      = ref BmpImageUtil.TryGetHeader(bmpBuffer);
            int              bmpDibOffset   = sizeof(BITMAPFILEHEADER);
            StandardClipboardFormats bmpTargetFormat = bmpHeader.DibHeaderSize != sizeof(BITMAPV5HEADER)
                ? StandardClipboardFormats.CF_DIB
                : StandardClipboardFormats.CF_DIBV5;

            if (!bmpHeader.IsValid)
                return;

            Clipboard.CopyDataToClipboard(pngStream.GetBuffer(), pngFormat, logger);
            Clipboard.CopyDataToClipboard(bmpBuffer.AsSpan(bmpDibOffset, bmpBuffer.Length - bmpDibOffset), bmpTargetFormat, logger);
        }
        finally
        {
            if (isClipboardOpened)
            {
                // Close the clipboard
                PInvoke.CloseClipboard();
            }

            // Dispose the source canvas
            sourceCanvasTarget.Dispose();
        }
    }
}
