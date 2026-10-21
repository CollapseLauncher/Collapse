using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
// ReSharper disable AccessToDisposedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    private static readonly ProcessImageSettings DecodingSettings = new()
    {
        EncoderOptions = PngEncoderOptions.Default
    };

    public static ImageSource? GetImageSource(object?           sourceObj,
                                              ILogger?          logger = null,
                                              CancellationToken token  = default)
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
            return GetImageSourceFromUri(sourceAsUri, logger, token);

        if (sourceAsString != null && sourceAsUri == null)
        {
            if (Uri.TryCreate(sourceAsString, UriKind.RelativeOrAbsolute, out sourceAsUri))
                return GetImageSourceFromUri(sourceAsUri, logger, token);

            if (!Uri.TryCreate(sourceAsString, UriKind.RelativeOrAbsolute, out sourceAsUri) &&
                StringUtility.TryGetStreamFromUrlDataString(sourceAsString,
                                                            out _,
                                                            out MemoryStream? memoryStreamFromB64))
            {
                isOwnIRandomStream = true;
                sourceAsIRandomStream = memoryStreamFromB64.AsRandomAccessStream(false);
            }
        }

        if (sourceAsIRandomStream == null)
            return null;

        return GetImageSourceFromRandomStream(sourceAsIRandomStream, isOwnIRandomStream, logger, token);
    }

    private static ImageSource? GetImageSourceFromUri(Uri uri, ILogger? logger = null, CancellationToken token = default)
    {
        ReadOnlySpan<char> fileExtension     = TryGetFileExtensionFromUri(uri);
        ImageSource?       returnImageSource = null;
        if (LayeredBackgroundImage.SupportedImageBitmapExtensionsLookup
                                  .Contains(fileExtension) ||
            LayeredBackgroundImage.SupportedImageBitmapExternalCodecExtensionsLookup
                                  .Contains(fileExtension))
        {
            returnImageSource = new BitmapImage();
            ReadBitmapImageFromUriDetached(uri, returnImageSource, logger, token);
            return returnImageSource;
        }

        if (LayeredBackgroundImage.SupportedImageVectorExtensionsLookup
                                  .Contains(fileExtension))
        {
            returnImageSource = new SvgImageSource(uri);
        }

        return returnImageSource;
    }

    private static ImageSource? GetImageSourceFromRandomStream(IRandomAccessStream stream,
                                                               bool                isOwn,
                                                               ILogger?            logger,
                                                               CancellationToken   token)
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
            ReadVectorImageFromStreamDetached(stream, svgImageSource, logger, isOwn);
            return svgImageSource;
        }

        BitmapImage bitmapImage = new();
        ReadBitmapImageFromStreamDetached(stream, bitmapImage, requireExternalDecoder, isOwn, logger, token);
        return bitmapImage;
    }

    private static async void ReadBitmapImageFromUriDetached(Uri               uri,
                                                             ImageSource       imageSource,
                                                             ILogger?          logger,
                                                             CancellationToken token)
    {
        try
        {
            if (imageSource is not BitmapImage asBitmapImage)
                return;

            // Download file if URL is a remote and get its cached path, or get the local path (if it's already local).
            Uri cachedUri = await ImageBackgroundManager.GetLocalOrDownloadedFilePath(uri, token);

            // Decode the image or get the cached one.
            (cachedUri, _) = await ImageBackgroundManager.GetNativeOrDecodedImagePath(cachedUri, token);

            // Load the load
            await using FileStream fileStream = File.Open(cachedUri.LocalPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using IRandomAccessStream randomFileStream = fileStream.AsRandomAccessStream();
            await asBitmapImage.SetSourceAsync(randomFileStream);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error has occurred while trying to load BitmapImage from: {uri}", uri);
        }
    }

    private static async void ReadVectorImageFromStreamDetached(IRandomAccessStream stream,
                                                                SvgImageSource      imageSource,
                                                                ILogger?            logger,
                                                                bool                isOwn)
    {
        try
        {
            await imageSource.SetSourceAsync(stream);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "An error has occurred while trying to load SvgImageSource from Stream");
        }
        finally
        {
            if (isOwn) stream.Dispose();
        }
    }

    private static async void ReadBitmapImageFromStreamDetached(IRandomAccessStream stream,
                                                                BitmapImage         imageSource,
                                                                bool                requireExternalDecoder,
                                                                bool                isOwn,
                                                                ILogger?            logger,
                                                                CancellationToken   token)
    {
        try
        {
            stream.Seek(0);
            if (!requireExternalDecoder)
            {
                await imageSource.SetSourceAsync(stream);
                return;
            }

            await using Stream encodedStream = stream.AsStream(true);
            await using MemoryStream decodedStream = new();

            await Task.Factory.StartNew(() =>
            {
                using ProcessingPipeline decodingPipeline = MagicImageProcessor.BuildPipeline(encodedStream, DecodingSettings);
                decodingPipeline.WriteOutput(decodedStream);
                decodedStream.Position = 0;
            }, token);

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
            logger?.LogError(ex, "An error has occurred while trying to load BitmapImage from Stream");
        }
        finally
        {
            if (isOwn) stream.Dispose();
        }
    }

    private static bool IsImageStreamSupported(IRandomAccessStream stream, out bool isSvg, out bool requireExternalDecoder)
    {
        Span<byte> buffer = stackalloc byte[512];
        using Stream asStream = stream.AsStream(true);
        _ = asStream.Read(buffer);

        ImageExternalCodecType internalType = ExtensionDetectUtility.IsHeaderInternalCodecImage(buffer);
        if (internalType == ImageExternalCodecType.Svg)
        {
            requireExternalDecoder = false;
            isSvg = true;
            return true;
        }

        ImageExternalCodecType externalType = ExtensionDetectUtility.IsHeaderExternalCodecImage(buffer);
        isSvg = false;
        requireExternalDecoder = externalType != ImageExternalCodecType.NotSupported &&
                                 internalType == ImageExternalCodecType.NotSupported;

        return internalType != ImageExternalCodecType.NotSupported ||
               externalType != ImageExternalCodecType.NotSupported;
    }

    private static ReadOnlySpan<char> TryGetFileExtensionFromUri(Uri uri)
    {
        ReadOnlySpan<char> pathAndQuery = uri.PathAndQuery;
        int indexOfQuerySign = pathAndQuery.IndexOf('?');
        if (indexOfQuerySign > 0)
            pathAndQuery = pathAndQuery[..indexOfQuerySign];

        return Path.GetExtension(pathAndQuery);
    }
}
