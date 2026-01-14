using CollapseLauncher.Extension;
using Hi3Helper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.DirectX;
using Windows.Storage.Streams;

// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public static class ClipboardUtility
{
    public static async void CopyCurrentFrameToClipboard(
        ImageBackgroundManager? instance,
        FrameToCopyType frameToCopyType)
    {
        bool isBackgroundVideo = false;

        try
        {
            if (instance == null)
            {
                return;
            }

            // Try to get video frame buffer first (if the background is video).
            CanvasRenderTarget? backgroundCanvas = frameToCopyType.HasFlag(FrameToCopyType.Background)
                ? instance.CurrentBackgroundElement?.LockCanvasRenderTarget()
                : null;

            ICanvasResourceCreator? canvasDevice = backgroundCanvas;
            canvasDevice ??= CanvasDevice.GetSharedDevice();

            if (backgroundCanvas != null)
            {
                // Copy bitmap to avoid arbitrary write to video frame buffer.
                CanvasRenderTarget oldCanvas = backgroundCanvas;
                backgroundCanvas = new CanvasRenderTarget(canvasDevice, backgroundCanvas.Size._width, backgroundCanvas.Size._height, 96f);
                backgroundCanvas.CopyPixelsFromBitmap(oldCanvas);

                isBackgroundVideo = true; // Mark as video so we can unlock the frame later.
            }

            // If it's still null (which most likely an image), get the canvas.
            backgroundCanvas ??= frameToCopyType.HasFlag(FrameToCopyType.Background)
                ? await GetImageCanvasTargetBuffer(canvasDevice,
                                                   instance.CurrentSelectedBackgroundContext?
                                                      .OriginBackgroundImagePath)
                : await GetImageCanvasTargetBuffer(canvasDevice,
                                                   instance.CurrentSelectedBackgroundContext?
                                                      .OriginOverlayImagePath);

            // If background canvas is still null, abort.
            if (backgroundCanvas == null)
            {
                return;
            }

            // Get foreground canvas if available.
            CanvasRenderTarget? foregroundCanvas =
                frameToCopyType == FrameToCopyType.Both && frameToCopyType.HasFlag(FrameToCopyType.Foreground)
                    ? await GetImageCanvasTargetBuffer(canvasDevice,
                                                       instance.CurrentSelectedBackgroundContext?
                                                          .OriginOverlayImagePath)
                    : null;

            int widthOfForeground = (int)(foregroundCanvas?.SizeInPixels.Width ?? 0u);
            int heightOfForeground = (int)(foregroundCanvas?.SizeInPixels.Height ?? 0u);
            if (foregroundCanvas != null &&
                (backgroundCanvas.SizeInPixels.Width != widthOfForeground ||
                 backgroundCanvas.SizeInPixels.Height != heightOfForeground))
            {
                backgroundCanvas = ResizeCanvasToSizeOf(backgroundCanvas, widthOfForeground, heightOfForeground);
            }

            MergeCanvasTargetWith(backgroundCanvas, foregroundCanvas); // Merge overlay (foreground) to background.
            await CopyCanvasTargetBufferToClipboard(backgroundCanvas); // Copy to clipboard
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[ImageBackgroundManager::CopyCurrentFrameToClipboard] An error occurred while trying to copy current background frame to clipboard\r\n{ex}",
                                LogType.Error,
                                true);
        }
        finally
        {
            if (isBackgroundVideo)
            {
                instance?.CurrentBackgroundElement?.UnlockCanvasRenderTarget();
            }
        }
    }

    private static CanvasRenderTarget ResizeCanvasToSizeOf(CanvasRenderTarget canvasToResize, int width, int height)
    {
        CanvasRenderTarget resizedCanvas = new(canvasToResize,
                                               width,
                                               height,
                                               canvasToResize.Dpi);
        using CanvasDrawingSession ds = resizedCanvas.CreateDrawingSession();
        ds.DrawImage(canvasToResize,
                     new Windows.Foundation.Rect(0, 0, width, height),
                     new Windows.Foundation.Rect(0, 0,
                                                 canvasToResize.SizeInPixels.Width,
                                                 canvasToResize.SizeInPixels.Height),
                     1f,
                     CanvasImageInterpolation.HighQualityCubic);

        canvasToResize.Dispose();
        return resizedCanvas;
    }

    private static async Task<CanvasRenderTarget?> GetImageCanvasTargetBuffer(ICanvasResourceCreator canvasDevice, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null; // Ignore if path is null or empty.
        }

        // Get source image as native or decoded image (if it requires external codec decoding).
        Uri sourceUri = new(path);
        (sourceUri, _) = await ImageBackgroundManager
           .GetNativeOrDecodedImagePath(sourceUri, CancellationToken.None);

        using CanvasBitmap bitmap = sourceUri.IsFile ?
            await CanvasBitmap.LoadAsync(canvasDevice,
                                         sourceUri.LocalPath,
                                         96f, CanvasAlphaMode.Premultiplied) :
            await CanvasBitmap.LoadAsync(canvasDevice,
                                         sourceUri,
                                         96f, CanvasAlphaMode.Premultiplied);

        // Load to render target
        return await Task.Run(() =>
        {
            CanvasRenderTarget renderTarget = new(canvasDevice,
                                                  bitmap.SizeInPixels.Width,
                                                  bitmap.SizeInPixels.Height, bitmap.Dpi);
            using CanvasDrawingSession ds = renderTarget.CreateDrawingSession();
            ds.DrawImage(bitmap);
            return renderTarget;
        });
    }

    private static void MergeCanvasTargetWith(CanvasRenderTarget sourceCanvasTarget,
                                              CanvasRenderTarget? withCanvasTarget)
    {
        if (withCanvasTarget == null)
        {
            return; // Abort
        }

        CanvasDrawingSession? ds = sourceCanvasTarget.CreateDrawingSession();
        ds.DrawImage(withCanvasTarget);

        // Dispose the overlay canvas
        withCanvasTarget.Dispose();

        if (DispatcherQueueExtensions.CurrentDispatcherQueue.HasThreadAccess)
        {
            ds.Dispose();
            return;
        }

        DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () => ds.Dispose());
    }

    private static async Task CopyCanvasTargetBufferToClipboard(CanvasRenderTarget? sourceCanvasTarget)
    {
        const uint biBitfields = 3;
        if (sourceCanvasTarget == null)
        {
            return;
        }

        // BGRA
        // TODO: Add a way to assign the color size and channels based on CanvasRenderTarget.Format
        const int colorSizePerChannel = 1; // 8-bit / 1 byte per color
        const int channels = 4;

        DirectXPixelFormat format    = sourceCanvasTarget.Format;
        CanvasAlphaMode    alphaMode = sourceCanvasTarget.AlphaMode;

        int width  = (int)sourceCanvasTarget.SizeInPixels.Width;
        int height = (int)sourceCanvasTarget.SizeInPixels.Height;
        int stride = width * height * channels * colorSizePerChannel;

        bool isClipboardOpened = false;

        int    sizeOfHeader = Marshal.SizeOf<BITMAPV5HEADER>();
        byte[] buffer       = ArrayPool<byte>.Shared.Rent(stride + sizeOfHeader);
        byte[] bufferPng    = ArrayPool<byte>.Shared.Rent(stride + sizeOfHeader);
        try
        {
            IBuffer windowsBuffer = buffer.AsBuffer(sizeOfHeader, stride, buffer.Length - sizeOfHeader);
            sourceCanvasTarget.GetPixelBytes(windowsBuffer);

            using MemoryStream        pngBufferStream       = new(bufferPng);
            using IRandomAccessStream pngBufferRandomStream = pngBufferStream.AsRandomAccessStream();
            await sourceCanvasTarget.SaveAsync(pngBufferRandomStream, CanvasBitmapFileFormat.Png);

            // Dispose the source canvas
            sourceCanvasTarget.Dispose();

            // Prepare BITMAPV5HEADER
            Span<byte> headerSpan = buffer.AsSpan(0, sizeOfHeader);
            headerSpan.Clear();
            ref BITMAPV5HEADER header = ref AsRef<BITMAPV5HEADER>(headerSpan);

            header.bV5Size        = (uint)sizeOfHeader;
            header.bV5Width       = width;
            header.bV5Height      = -height;
            header.bV5Planes      = 1;
            header.bV5BitCount    = 32;
            header.bV5Compression = biBitfields;
            header.bV5SizeImage   = (uint)stride;
            header.bV5RedMask     = 0x00FF0000;
            header.bV5GreenMask   = 0x0000FF00;
            header.bV5BlueMask    = 0x000000FF;
            header.bV5AlphaMask   = 0xFF000000;
            header.bV5CSType      = 0x73524742; // 'sRGB'

            // Try open the Clipboard
            if (!PInvoke.OpenClipboard(nint.Zero))
            {
                Logger.LogWriteLine($"[ClipboardUtility::CopyCanvasTargetBufferToClipboard] Cannot open the clipboard! Error: {Win32Error.GetLastWin32ErrorMessage()}",
                                    LogType.Error,
                                    true);
                return;
            }

            // Make sure to empty the clipboard before setting and allocate the content
            if (!PInvoke.EmptyClipboard())
            {
                Logger.LogWriteLine($"[ClipboardUtility::CopyCanvasTargetBufferToClipboard] Cannot clear the previous clipboard! Error: {Win32Error.GetLastWin32ErrorMessage()}",
                                    LogType.Error,
                                    true);
                return;
            }

            isClipboardOpened = true;

            // Load the "load" ( ͡° ͜ʖ ͡°)
            ILogger logger = ILoggerHelper.GetILogger("ClipboardUtility::CopyCanvasTargetBufferToClipboard");
            Clipboard.CopyDataToClipboard(buffer.AsSpan(0, sizeOfHeader + stride), StandardClipboardFormats.CF_DIBV5, logger); // Copy as BMP for fallback
            Clipboard.CopyDataToClipboard(bufferPng.AsSpan(0, (int)pngBufferStream.Position), StandardClipboardFormats.CF_PNG, logger);   // Copy as PNG
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            ArrayPool<byte>.Shared.Return(bufferPng);

            if (isClipboardOpened)
            {
                // Close the clipboard
                PInvoke.CloseClipboard();
            }
        }
    }

    private static unsafe ref T AsRef<T>(Span<byte> span) => ref Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.AsRef<byte>(span)));
}
