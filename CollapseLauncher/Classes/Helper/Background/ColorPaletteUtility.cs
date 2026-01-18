using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Svg;
using PhotoSauce.MagicScaler;
using System;
using System.Buffers;
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
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.Helper.Background;

internal static class ColorPaletteUtility
{
    public static async Task<Color> GetMediaAccentColorFromAsync(
        Uri               uri,
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
            int                channels   = frameInfo.HasAlpha ? 4 : 3;
            byte[]             tempBuffer = ArrayPool<byte>.Shared.Rent(frameInfo.Width * frameInfo.Height * channels);
            using MemoryStream tempStream = new(tempBuffer);
            try
            {
                ProcessImageSettings settings = new();
                settings.TrySetEncoderFormat(ImageMimeTypes.Bmp);
                MagicImageProcessor.ProcessImage(sourceStream, tempStream, settings);

                Span<byte> tempDataSpan = tempBuffer.AsSpan(54, (int)tempStream.Position - 54);
                Color colorArray = GetPaletteFromSpan(tempDataSpan, frameInfo.Width, frameInfo.Height, channels);
                ReadOnlySpan<byte> colorSpanAsBytes = MemoryMarshal.AsBytes([colorArray]);

                File.WriteAllBytes(cachedPaletteFile, colorSpanAsBytes);
                return colorArray;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tempBuffer);
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
                StorageFile          storageFile = await StorageFile.GetFileFromPathAsync(filePath);
                StorageItemThumbnail thumbnail   = await storageFile
                   .GetThumbnailAsync(ThumbnailMode.VideosView,
                                      1600); // Max it can get
                return thumbnail;
            }

            (string, ImageExternalCodecType) result = await ImageBackgroundManager.GetNativeOrDecodedImagePath(filePath, token);
            if (result.Item2 == ImageExternalCodecType.NotSupported)
            {
                return null;
            }

            if (result.Item2 != ImageExternalCodecType.Svg)
                return File.Open(result.Item1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            using CanvasDevice        svgDevice       = new();
            await using FileStream    svgStream       = File.Open(result.Item1, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using IRandomAccessStream svgRandomStream = svgStream.AsRandomAccessStream(true);

            CanvasSvgDocument svgDoc = await CanvasSvgDocument.LoadAsync(svgDevice, svgRandomStream);
            return null;
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