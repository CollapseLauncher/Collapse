using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using ColorThiefDotNet;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.Native.Interfaces;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.WinRT.IBufferCOM;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.IO;
using System.IO.Hashing;
using System.Numerics;
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
    private static          byte[]? _sharedBuffer;
    private static readonly Lock    SharedBufferLock = new();

    private static byte[] GetSharedBufferOrResizeTo(int size)
    {
        using (SharedBufferLock.EnterScope())
        {
            size = (int)BitOperations.RoundUpToPowerOf2((uint)size);

            if (_sharedBuffer != null && _sharedBuffer.Length >= size) return _sharedBuffer;

            if (_sharedBuffer == null)
            {
                _sharedBuffer = GC.AllocateUninitializedArray<byte>(size);
                return _sharedBuffer;
            }

            byte[] newBuffer = GC.AllocateUninitializedArray<byte>(size);
            Array.Copy(_sharedBuffer, newBuffer, _sharedBuffer.Length);
            _sharedBuffer = newBuffer;
            return _sharedBuffer;
        }
    }

    public static async Task<Color> GetMediaAccentColorFromAsync(
        Uri               uri,
        bool              useFfmpegForVideo,
        CancellationToken token = default)
    {
        // -- Ignore if file doesn't exist or not a file URI
        string filePath = uri.LocalPath;
        if (!uri.IsFile ||
            !File.Exists(filePath))
        {
            return default;
        }

        // -- Try to get cached palette data
        if (TryGetCachedPaletteData(filePath,
                                    out Color cachedColors,
                                    out string cachedPaletteFile))
        {
            return cachedColors;
        }

        // -- If not cached due to it not existing or has invalid format, generate new palette data
        FileInfo cachedPaletteFileInfo = new FileInfo(cachedPaletteFile)
           .EnsureCreationOfDirectory();
        cachedPaletteFileInfo.Directory?.Create();

        // -- Get source stream
        object? sourceStreamObj = await GetMediaFrameStream(filePath, token);

        return await Task.Factory.StartNew(Impl, TaskCreationOptions.DenyChildAttach);

        Color Impl()
        {
            using IRandomAccessStream? sourceRandomStream = sourceStreamObj as IRandomAccessStream;
            using Stream?              sourceStream       = sourceStreamObj as Stream ?? sourceRandomStream?.AsStream();

            if (sourceStream == null)
            {
                return default;
            }

            ImageFileInfo           fileInfo  = ImageFileInfo.Load(sourceStream);
            ImageFileInfo.FrameInfo frameInfo = fileInfo.Frames[0];
            sourceStream.Seek(0, SeekOrigin.Begin);

            // -- Process palette
            int                  channels   = frameInfo.HasAlpha ? 4 : 3;
            byte[]               tempBuffer = GetSharedBufferOrResizeTo(frameInfo.Width * frameInfo.Height * channels + 54);
            using MemoryStream   tempStream = new(tempBuffer);
            ProcessImageSettings settings   = new();
            settings.TrySetEncoderFormat(ImageMimeTypes.Bmp);
            MagicImageProcessor.ProcessImage(sourceStream, tempStream, settings);

            Span<byte> tempDataSpan = tempBuffer.AsSpan(54, (int)tempStream.Position - 54);
            Color colorArray = GetPaletteFromSpan(tempDataSpan, frameInfo.Width, frameInfo.Height, channels);
            ReadOnlySpan<byte> colorSpanAsBytes = MemoryMarshal.AsBytes([colorArray]);

            File.WriteAllBytes(cachedPaletteFile, colorSpanAsBytes);
            return colorArray;
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
                const uint biBitfields = 3;

                int    stride   = width * height * 4;
                string tempPath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + ".bmp");

                int                sizeOfHeader = sizeof(BITMAPV5HEADER);
                Span<byte>         headerSpan   = stackalloc byte[sizeOfHeader];
                ref BITMAPV5HEADER header       = ref AsRef<BITMAPV5HEADER>(headerSpan);

                header.bV5Size        = (uint)sizeOfHeader;
                header.bV5Width       = width;
                header.bV5Height      = height;
                header.bV5Planes      = 1;
                header.bV5BitCount    = 32;
                header.bV5Compression = biBitfields;
                header.bV5SizeImage   = (uint)stride;
                header.bV5RedMask     = 0x00FF0000;
                header.bV5GreenMask   = 0x0000FF00;
                header.bV5BlueMask    = 0x000000FF;
                header.bV5AlphaMask   = 0xFF000000;
                header.bV5CSType      = 0x73524742; // 'sRGB'

                FileStream stream = File.Open(tempPath, new FileStreamOptions
                {
                    Mode    = FileMode.Create,
                    Access  = FileAccess.ReadWrite,
                    Share   = FileShare.ReadWrite
                });
                stream.Write(headerSpan);
                stream.Write(buffer);

                stream.Position = 0;
                return stream;
            }

            static unsafe ref T AsRef<T>(Span<byte> span)
                => ref Unsafe.AsRef<T>(Unsafe.AsPointer(ref MemoryMarshal.AsRef<byte>(span)));
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