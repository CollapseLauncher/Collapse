using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
// ReSharper disable AccessToDisposedClosure

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Pages;

public partial class ImageDecoderValueConverter : IValueConverter
{
    public static readonly ImageDecoderValueConverter Shared = new();

    private static readonly ProcessImageSettings DecodingSettings = new()
    {
        EncoderOptions = PngEncoderOptions.Default
    };

    private static readonly ILogger Logger = ILoggerHelper.GetILogger("ImageDecoderValueConverter");

    public object? Convert(object? value, Type targetType, object? parameter, string? language)
        => GetImageSource(value) ?? value;

    public object ConvertBack(object? value, Type targetType, object? parameter, string? language)
    {
        throw new NotSupportedException();
    }

    private static ImageSource? GetImageSource(object? sourceObj)
    {
        switch (sourceObj)
        {
            case null:
                return null;
            case ImageSource asImageSource:
                return asImageSource;
        }

        Uri?    sourceAsUri    = sourceObj as Uri;
        string? sourceAsString = sourceObj as string;
        Stream? sourceAsStream = sourceObj as Stream;
        IRandomAccessStream? sourceAsIRandomStream = sourceObj as IRandomAccessStream ??
                                                     sourceAsStream?.AsRandomAccessStream(false, true);
        bool isOwnIRandomStream = sourceAsStream is null && sourceAsIRandomStream != null;

        if (sourceAsUri != null)
            return GetImageSourceFromUri(sourceAsUri);

        if (sourceAsString != null && sourceAsUri == null)
        {
            if (Uri.TryCreate(sourceAsString, UriKind.RelativeOrAbsolute, out sourceAsUri))
                return GetImageSourceFromUri(sourceAsUri);

            if (!Uri.TryCreate(sourceAsString, UriKind.RelativeOrAbsolute, out sourceAsUri) &&
                StringUtility.TryGetStreamFromUrlDataString(sourceAsString,
                                                            out _,
                                                            out MemoryStream? memoryStreamFromB64))
            {
                isOwnIRandomStream    = true;
                sourceAsIRandomStream = memoryStreamFromB64.AsRandomAccessStream(false);
            }
        }

        if (sourceAsIRandomStream == null)
            return null;

        return GetImageSourceFromRandomStream(sourceAsIRandomStream, isOwnIRandomStream);
    }

    private static ImageSource? GetImageSourceFromUri(Uri uri)
    {
        ReadOnlySpan<char> fileExtension = TryGetFileExtensionFromUri(uri);
        ImageSource? returnImageSource = null;
        if (LayeredBackgroundImage.SupportedImageBitmapExtensionsLookup
                                  .Contains(fileExtension) ||
            LayeredBackgroundImage.SupportedImageBitmapExternalCodecExtensionsLookup
                                  .Contains(fileExtension))
        {
            returnImageSource = new BitmapImage();
            ReadBitmapImageFromUriDetached(uri, returnImageSource);
            return returnImageSource;
        }

        if (LayeredBackgroundImage.SupportedImageVectorExtensionsLookup
                                  .Contains(fileExtension))
        {
            returnImageSource = new SvgImageSource(uri);
        }

        return returnImageSource;
    }

    private static ImageSource? GetImageSourceFromRandomStream(IRandomAccessStream stream, bool isOwn)
    {
        // Guess format
        if (!IsImageStreamSupported(stream,
                                    out bool isSvg,
                                    out bool requireExternalDecoder))
        {
            if (isOwn) stream.Dispose(); // Dispose early
            return null;
        }

        stream.Seek(0);
        if (isSvg)
        {
            SvgImageSource svgImageSource = new();
            ReadVectorImageFromStreamDetached(stream, svgImageSource, isOwn);
            return svgImageSource;
        }

        BitmapImage bitmapImage = new();
        ReadBitmapImageFromStreamDetached(stream, bitmapImage, requireExternalDecoder, isOwn);
        return bitmapImage;
    }

    private static async void ReadBitmapImageFromUriDetached(Uri uri, ImageSource imageSource)
    {
        try
        {
            if (imageSource is not BitmapImage asBitmapImage)
                return;

            // Download file if URL is a remote and get its cached path, or get the local path (if it's already local).
            Uri cachedUri = await ImageBackgroundManager.GetLocalOrDownloadedFilePath(uri, CancellationToken.None);

            // Decode the image or get the cached one.
            (cachedUri, _) = await ImageBackgroundManager.GetNativeOrDecodedImagePath(cachedUri, CancellationToken.None);

            // Load the load
            await using FileStream fileStream = File.Open(cachedUri.LocalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using IRandomAccessStream randomFileStream = fileStream.AsRandomAccessStream();
            await asBitmapImage.SetSourceAsync(randomFileStream);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error has occurred while trying to load BitmapImage from: {uri}", uri);
        }
    }

    private static async void ReadVectorImageFromStreamDetached(IRandomAccessStream stream,
                                                                SvgImageSource      imageSource,
                                                                bool                isOwn)
    {
        try
        {
            await imageSource.SetSourceAsync(stream);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error has occurred while trying to load SvgImageSource from Stream");
        }
        finally
        {
            if (isOwn) stream.Dispose();
        }
    }

    private static async void ReadBitmapImageFromStreamDetached(IRandomAccessStream stream,
                                                                BitmapImage         imageSource,
                                                                bool                requireExternalDecoder,
                                                                bool                isOwn)
    {
        try
        {
            stream.Seek(0);
            if (!requireExternalDecoder)
            {
                await imageSource.SetSourceAsync(stream);
                return;
            }

            await using Stream       encodedStream = stream.AsStream(true);
            await using MemoryStream decodedStream = new();

            await Task.Factory.StartNew(() =>
            {
                using ProcessingPipeline decodingPipeline = MagicImageProcessor.BuildPipeline(encodedStream, DecodingSettings);
                decodingPipeline.WriteOutput(decodedStream);
                decodedStream.Position = 0;
            });

            TaskCompletionSource tcs = new();
            imageSource.DispatcherQueue.TryEnqueue(async void () =>
            {
                try
                {
                    using IRandomAccessStream randomDecodedStream = decodedStream.AsRandomAccessStream();
                    await imageSource.SetSourceAsync(randomDecodedStream);

                    tcs.SetResult();
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            });

            await tcs.Task;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error has occurred while trying to load BitmapImage from Stream");
        }
        finally
        {
            if (isOwn) stream.Dispose();
        }
    }

    private static bool IsImageStreamSupported(IRandomAccessStream stream, out bool isSvg, out bool requireExternalDecoder)
    {
        Span<byte>   buffer       = stackalloc byte[512];
        using Stream asStream     = stream.AsStream(true);
        _ = asStream.Read(buffer);

        ImageExternalCodecType internalType = ExtensionDetectUtility.IsHeaderInternalCodecImage(buffer);
        if (internalType == ImageExternalCodecType.Svg)
        {
            requireExternalDecoder = false;
            isSvg                  = true;
            return true;
        }

        ImageExternalCodecType externalType = ExtensionDetectUtility.IsHeaderExternalCodecImage(buffer);
        isSvg                  = false;
        requireExternalDecoder = externalType != ImageExternalCodecType.NotSupported &&
                                 internalType == ImageExternalCodecType.NotSupported;

        return internalType != ImageExternalCodecType.NotSupported ||
               externalType != ImageExternalCodecType.NotSupported;
    }

    private static ReadOnlySpan<char> TryGetFileExtensionFromUri(Uri uri)
    {
        ReadOnlySpan<char> pathAndQuery     = uri.PathAndQuery;
        int                indexOfQuerySign = pathAndQuery.IndexOf('?');
        if (indexOfQuerySign > 0)
            pathAndQuery = pathAndQuery[..indexOfQuerySign];

        return Path.GetExtension(pathAndQuery);
    }
}
