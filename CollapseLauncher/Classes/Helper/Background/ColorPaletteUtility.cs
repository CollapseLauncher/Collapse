using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using ColorThiefDotNet;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.WinRT.IBufferCOM;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.IO;
using System.IO.Hashing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI;

// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.Helper.Background;

internal static class ColorPaletteUtility
{
    public static ValueTask<Color> GetMediaAccentColorFromAsync(
        Uri               uri,
        CancellationToken token = default)
    {
        // -- Ignore if file doesn't exist or not a file URI
        string filePath = uri.LocalPath;
        if (!uri.IsFile || !File.Exists(filePath))
        {
            return ValueTask.FromResult(Color.FromArgb(255, 255, 255, 255));
        }

        // -- Try to get cached palette data
        if (TryGetCachedPaletteData(filePath,
                                    out Color cachedColors,
                                    out string cachedPaletteFile))
        {
            return ValueTask.FromResult(cachedColors);
        }

        TaskCompletionSource<Color> processTcs = new();
        new Thread(Impl)
        {
            IsBackground = true,
            Name         = "Collapse Launcher Media Accent Color Picker"
        }.UnsafeStart();
        return new ValueTask<Color>(processTcs.Task);

        async void Impl()
        {
            try
            {
                // -- If not cached due to it not existing or has invalid format, generate new palette data
                FileInfo cachedPaletteFileInfo = new FileInfo(cachedPaletteFile).EnsureCreationOfDirectory();
                cachedPaletteFileInfo.Directory?.Create();

                // -- Get source stream
                object? sourceStreamObj = await GetMediaFrameStream(filePath, token).ConfigureAwait(false);

                using IRandomAccessStream? sourceRandomStream = sourceStreamObj as IRandomAccessStream;
                await using Stream?        sourceStream = sourceStreamObj as Stream ?? sourceRandomStream?.AsStream();

                if (sourceStream == null)
                {
                    processTcs.SetResult(Color.FromArgb(255, 255, 255, 255));
                    return;
                }

                processTcs.SetResult(await Task.Run(() =>
                {
                    sourceStream.Seek(0, SeekOrigin.Begin);

                    ProcessImageSettings settings = new();
                    settings.TrySetEncoderFormat(ImageMimeTypes.Bmp);

                    using MemoryStream       bmpTempStream = new();
                    using ProcessingPipeline pipeline      = MagicImageProcessor.BuildPipeline(sourceStream, settings);
                    pipeline.WriteOutput(bmpTempStream);

                    byte[]           bmpTempBuffer = bmpTempStream.GetBuffer();
                    ref BITMAPHEADER bmpHeader     = ref BmpImageUtil.TryGetHeader(bmpTempBuffer);

                    if (Unsafe.IsNullRef(ref bmpHeader))
                        throw new InvalidDataException("Output file contains an invalid BMP image file!");

                    ref BITMAPV1HEADER bmpDibHeader = ref bmpHeader.TryReadAs<BITMAPV1HEADER>();
                    if (Unsafe.IsNullRef(ref bmpDibHeader))
                        throw new InvalidDataException("DIB Header of the BMP image is unsupported or invalid!");

                    bool hasAlpha = false;
                    if (bmpDibHeader.V1Size == sizeof(BITMAPV4HEADER))
                    {
                        ref BITMAPV4HEADER bmpDibV4Header = ref bmpHeader.TryReadAs<BITMAPV4HEADER>();
                        hasAlpha = bmpDibV4Header.V4BitCount is 16 or 32 && bmpDibV4Header.V4AlphaMask != 0;
                    }

                    int width    = bmpDibHeader.V1Width;
                    int height   = bmpDibHeader.V1Height;
                    int channels = hasAlpha ? 4 : 3;

                    int pixelOffset = (int)bmpHeader.File.PixelDataOffset;
                    int pixelSize   = (int)bmpHeader.File.Size - pixelOffset;

                    Span<byte> pixelSpan = bmpTempBuffer.AsSpan(pixelOffset, pixelSize);

                    Color              colorArray       = GetPaletteFromSpan(pixelSpan, width, height, channels);
                    ReadOnlySpan<byte> colorSpanAsBytes = MemoryMarshal.AsBytes([colorArray]);

                    File.WriteAllBytes(cachedPaletteFile, colorSpanAsBytes);
                    return colorArray;
                }, token));
            }
            catch (Exception ex)
            {
                processTcs.SetException(ex);
            }
        }
    }

    private static unsafe Color GetPaletteFromSpan(
        ReadOnlySpan<byte> data,
        int                width,
        int                height,
        int                channels)
    {
        nint dataP = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(data));
        QuantizedColor paletteList =
            ColorThief.GetColor(dataP,
                                channels,
                                width,
                                height,
                                1,
                                false);

        Color color = new Color
        {
            A = paletteList.Color.A,
            R = paletteList.Color.R,
            G = paletteList.Color.G,
            B = paletteList.Color.B
        }.SetSaturation(1.2d);

        return InnerLauncherConfig.IsAppThemeLight
            ? color.GetDarkColor()
            : color.GetLightColor();
    }

    private static unsafe bool TryGetCachedPaletteData(
        string     filePath,
        out Color  cachedColor,
        out string cachedPaletteFile)
    {
        string imageCachedFilePath = LauncherConfig.AppGameImgCachedFolder;
        byte[] fileNameHash        = HashUtility<XxHash128>.Shared.GetHashFromString(filePath);

        string fileName = $"{HexTool.BytesToHexUnsafe(fileNameHash)}.{(InnerLauncherConfig.IsAppThemeLight ? "lightpalette" : "darkpalette")}";
        cachedPaletteFile = Path.Combine(imageCachedFilePath, fileName);

        cachedColor = default;

        if (!File.Exists(cachedPaletteFile))
        {
            return false;
        }

        try
        {
            int minimumDataCount = sizeof(Color);

            byte[] data = File.ReadAllBytes(cachedPaletteFile);
            if (data.Length < minimumDataCount)
            {
                return false;
            }

            cachedColor = MemoryMarshal.Read<Color>(data);
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[ColorPaletteUtility::TryGetCachedPaletteData] Failed to get cached palette data due to an error\r\n{ex}",
                                LogType.Error,
                                true);
            SentryHelper.ExceptionHandler(ex);
            return false;
        }
    }

    private static async Task<object?> GetMediaFrameStream(
        string            filePath,
        CancellationToken token = default)
    {
        try
        {
            ReadOnlySpan<char> extension = Path.GetExtension(filePath);
            if (LayeredBackgroundImage
               .SupportedVideoExtensionsLookup
               .Contains(extension))
            {
                // Try to use Windows Media Foundation for video preview grabber even though
                // FFmpeg is used. If not supported, then fallback to use FFmpeg's FrameGrabber.
                if (!WindowsCodecHelper.IsFileSupportedVideo(filePath,
                                                             out _,
                                                             out _,
                                                             out _,
                                                             out _))
                {
                    using FrameGrabber? ffmpegFrameGrabber  = await FrameGrabber.CreateFromFileAsync(filePath);
                    TimeSpan ffmpegFrameNormalSubDur = ffmpegFrameGrabber.Duration * 0.033d;
                    TimeSpan ffmpegFrameDuration = (ffmpegFrameGrabber.Duration - ffmpegFrameNormalSubDur) * 0.25d;
                    using VideoFrame? ffmpegFrame = await ffmpegFrameGrabber.ExtractVideoFrameAsync(ffmpegFrameDuration, true);

                    MemoryStream ffmpegFrameExtracted = new();
                    IRandomAccessStream ffmpegFrameStream = ffmpegFrameExtracted.AsRandomAccessStream();
                    await ffmpegFrame.EncodeAsBmpAsync(ffmpegFrameStream);

                    await ffmpegFrameStream.FlushAsync();
                    await ffmpegFrameExtracted.FlushAsync(token);

                    // Downscale by 1920 width to match WMF's thumbnail maximum size.
                    if (ffmpegFrameGrabber.CurrentVideoStream.PixelWidth > 1920)
                    {
                        int newHeight = (int)(ffmpegFrameGrabber.CurrentVideoStream.PixelHeight *
                                              (1920d / ffmpegFrameGrabber.CurrentVideoStream.PixelWidth));
                        ffmpegFrameExtracted.Position = 0;
                        using MemoryStream newResizedFfmpegFrameStream = new();

                        ProcessImageSettings settings = new()
                        {
                            Width          = 1920,
                            Height         = newHeight,
                            EncoderOptions = new PngEncoderOptions()
                        };

                        using ProcessingPipeline pipeline = MagicImageProcessor.BuildPipeline(ffmpegFrameExtracted, settings);
                        pipeline.WriteOutput(newResizedFfmpegFrameStream);

                        ffmpegFrameExtracted.SetLength(0);
                        newResizedFfmpegFrameStream.Position = 0;
                        await newResizedFfmpegFrameStream.CopyToAsync(ffmpegFrameExtracted, token);
                    }

                    ffmpegFrameExtracted.Position = 0;
                    return ffmpegFrameExtracted;
                }

                StorageFile          storageFile = await StorageFile.GetFileFromPathAsync(filePath);
                StorageItemThumbnail thumbnail   = await storageFile.GetThumbnailAsync(ThumbnailMode.SingleItem, 1600);
                return thumbnail;
            }

            (string, ImageExternalCodecType) result = await ImageBackgroundManager.GetNativeOrDecodedImagePath(filePath, token);
            if (result.Item2 == ImageExternalCodecType.NotSupported)
            {
                return null;
            }

            if (result.Item2 != ImageExternalCodecType.Svg)
                return File.Open(result.Item1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            SvgImageSource svgImageSource = new(new Uri(result.Item1));
            double         svgImageWidth  = svgImageSource.RasterizePixelWidth;
            double         svgImageHeight = svgImageSource.RasterizePixelHeight;

            double svgWindowWidth = WindowUtility.CurrentWindowPosition.Width;
            if (svgImageWidth < svgWindowWidth)
            {
                double svgScaleFactor = svgImageWidth / svgWindowWidth;
                svgImageWidth  *= svgScaleFactor;
                svgImageHeight *= svgScaleFactor;
            }

            Microsoft.UI.Xaml.Controls.Image svgImage = new()
            {
                Width  = svgImageWidth,
                Height = svgImageHeight,
                Source = svgImageSource
            };

            RenderTargetBitmap svgBitmap = new();
            await svgBitmap.RenderAsync(svgImage, (int)svgImageWidth, (int)svgImageHeight);

            IBuffer           svgBitmapBuffer       = await svgBitmap.GetPixelsAsync();
            IBufferByteAccess svgBitmapBufferAccess = svgBitmapBuffer.AsBufferByteAccess();

            svgBitmapBufferAccess.Buffer(out nint svgBitmapP);
            Span<byte> svgBitmapBufferSpan = CreateSpan(svgBitmapP, (int)svgBitmapBuffer.Length);

            return CreateSvgTempStream((int)svgImageWidth, (int)svgImageHeight, svgBitmapBufferSpan);

            static unsafe Span<byte> CreateSpan(nint pointer, int size)
                => new((void*)pointer, size);

            static unsafe FileStream CreateSvgTempStream(int width, int height, Span<byte> buffer)
            {
                int    stride   = width * height * 4;
                string tempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".bmp");

                BITMAPHEADER header = default;
                header.Init();
                ref BITMAPV5HEADER dibV5Header = ref header.TryReadAs<BITMAPV5HEADER>();

                dibV5Header.V5Width       = width;
                dibV5Header.V5Height      = height;
                dibV5Header.V5Planes      = 1;
                dibV5Header.V5BitCount    = 32;
                dibV5Header.V5SizeImage   = (uint)stride;
                dibV5Header.V5RedMask     = 0x00FF0000;
                dibV5Header.V5GreenMask   = 0x0000FF00;
                dibV5Header.V5BlueMask    = 0x000000FF;
                dibV5Header.V5AlphaMask   = 0xFF000000;
                dibV5Header.V5CSType      = 0x73524742; // 'sRGB'
                header.File.Size          = (uint)(sizeof(BITMAPHEADER) + buffer.Length);

                FileStream stream = File.Open(tempPath, new FileStreamOptions
                {
                    Mode    = FileMode.Create,
                    Access  = FileAccess.ReadWrite,
                    Share   = FileShare.ReadWrite
                });
                stream.Write(in dibV5Header);
                stream.Write(buffer);

                stream.Position = 0;
                return stream;
            }

            static unsafe ref T AsRef<T>(Span<byte> span)
                => ref Unsafe.As<byte, T>(ref MemoryMarshal.AsRef<byte>(span));
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[ColorPaletteUtility::GetMediaFrameStream] Failed to get media frame stream\r\n{ex}",
                                LogType.Error,
                                true);
            SentryHelper.ExceptionHandler(ex);
            return null;
        }
    }
}