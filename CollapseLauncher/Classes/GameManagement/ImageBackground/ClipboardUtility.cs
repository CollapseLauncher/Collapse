using CollapseLauncher.Extension;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using System;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
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
        FrameToCopyType frameToCopyType)
    {
        bool isBackgroundVideo = false;

        try
        {
            if (instance == null)
            {
                return;
            }

            bool hasBackground = frameToCopyType.HasFlag(FrameToCopyType.Background);
            bool hasForeground = frameToCopyType.HasFlag(FrameToCopyType.Foreground);
            bool hasBoth       = frameToCopyType == FrameToCopyType.Both;

            // Try to get video frame buffer first (if the background is video).
            LayeredBackgroundImage? currentElement = instance.CurrentBackgroundElement;
            bool isStaticBackgroundShowing = currentElement is { BackgroundStaticSource: not null, IsVideoPlay: false };

            string? usedBackgroundPath = isStaticBackgroundShowing
                ? instance.CurrentSelectedBackgroundContext?
                   .OriginBackgroundStaticImagePath
                : instance.CurrentSelectedBackgroundContext?
                   .OriginBackgroundImagePath;

            CanvasRenderTarget? backgroundCanvas = hasBackground
                ? isStaticBackgroundShowing ? null : currentElement?.LockCanvasRenderTarget()
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
            backgroundCanvas ??= hasBackground
                ? await GetImageCanvasTargetBuffer(canvasDevice,
                                                   usedBackgroundPath)
                : await GetImageCanvasTargetBuffer(canvasDevice,
                                                   instance.CurrentSelectedBackgroundContext?
                                                      .OriginOverlayImagePath);

            // If background canvas is still null, abort.
            if (backgroundCanvas == null)
            {
                return;
            }

            // Get foreground canvas if available.
            CanvasRenderTarget? foregroundCanvas = hasBoth && hasForeground
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
                                         96f,
                                         CanvasAlphaMode.Premultiplied) :
            await CanvasBitmap.LoadAsync(canvasDevice,
                                         sourceUri,
                                         96f,
                                         CanvasAlphaMode.Premultiplied);

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
        {
            return;
        }

        bool isClipboardOpened = true;
        try
        {
            ILogger logger = ILoggerHelper.GetILogger("ClipboardUtility::CopyCanvasTargetBufferToClipboard");

            Direct3DSurfaceDescription desc          = sourceCanvasTarget.Description;
            int                        bmpHeaderSize = Marshal.SizeOf<BITMAPV5HEADER>();
            int                        strideSize    = GetBitmapStrideSize(desc);
            int                        bufferSize    = strideSize + bmpHeaderSize;
            int                        maxBufferSize = (int)BitOperations.RoundUpToPowerOf2((uint)bufferSize);
            byte[]                     buffer        = GC.AllocateUninitializedArray<byte>(maxBufferSize);

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
            Span<byte> headerSpan = buffer.AsSpan(0, bmpHeaderSize);
            headerSpan.Clear();
            ref BITMAPV5HEADER header = ref AsRef<BITMAPV5HEADER>(headerSpan);

            int    bmpChannelCount  = GetBitmapChannelCount(desc.Format);
            double bmpBitPerChannel = GetBitmapBitSize(desc.Format);
            ushort bmpBitCount      = (ushort)(bmpBitPerChannel * bmpChannelCount);

            header.bV5Size        = (uint)bmpHeaderSize;
            header.bV5Width       = desc.Width;
            header.bV5Height      = -desc.Height;
            header.bV5Planes      = 1;
            header.bV5BitCount    = bmpBitCount;
            header.bV5Compression = 3;
            header.bV5SizeImage   = (uint)strideSize;
            header.bV5RedMask     = 0x00FF0000;
            header.bV5GreenMask   = 0x0000FF00;
            header.bV5BlueMask    = 0x000000FF;
            header.bV5AlphaMask   = 0xFF000000;
            header.bV5CSType      = 0x73524742; // 'sRGB'

            await Task.Run(ImplWriteBmp);
            void ImplWriteBmp()
            {
                IBuffer windowsBuffer = buffer.AsBuffer(bmpHeaderSize, strideSize, buffer.Length - bmpHeaderSize);
                sourceCanvasTarget.GetPixelBytes(windowsBuffer);
                Clipboard.CopyDataToClipboard(buffer.AsSpan(0, bufferSize), StandardClipboardFormats.CF_DIBV5, logger);
            }
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
    private static unsafe ref T AsRef<T>(Span<byte> span) => ref Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.AsRef<byte>(span)));
}
