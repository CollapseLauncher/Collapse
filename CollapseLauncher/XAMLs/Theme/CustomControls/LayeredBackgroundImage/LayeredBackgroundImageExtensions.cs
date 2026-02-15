using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Transforms;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable ConvertIfStatementToSwitchStatement

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

internal static class LayeredBackgroundImageExtensions
{
    private static readonly HttpClient Client;

    static LayeredBackgroundImageExtensions()
    {
        Client = CreateHttpClient();
    }

    private static HttpClient CreateHttpClient()
    {
        SocketsHttpHandler handler = new()
        {
            AllowAutoRedirect       = true,
            MaxConnectionsPerServer = Environment.ProcessorCount * 2
        };
        return new HttpClient(handler, false);
    }

    internal static async ValueTask<bool> LoadImageAsync(
        this Image             image,
        Uri?                   sourceFromPath,
        Stream?                sourceFromStream,
        LayeredBackgroundImage instance,
        DependencyProperty     useCacheProperty,
        IPixelTransform?       pixelTransform = null)
    {
        IMediaCacheHandler? cacheHandler            = instance.MediaCacheHandler;
        bool                isDisposeStream         = sourceFromStream == null;
        bool                forceUseInternalDecoder = false;

        // Try get cached source from cache handler
        if (cacheHandler != null)
        {
            object? source = sourceFromPath;
            source ??= sourceFromStream;
            MediaCacheResult cacheResult = await cacheHandler.LoadCachedSource(source);

            forceUseInternalDecoder = cacheResult.ForceUseInternalDecoder;
            isDisposeStream         = cacheResult.DisposeStream;

            if (cacheResult.CachedSource is Uri sourceAsPath)
            {
                sourceFromPath   = sourceAsPath;
                sourceFromStream = null;
            }
            else if (cacheResult.CachedSource is Stream sourceAsStream)
            {
                sourceFromPath   = null;
                sourceFromStream = sourceAsStream;
            }
            else
            {
                return false;
            }
        }

        try
        {
            if (sourceFromPath != null)
            {
                return await image.LoadImageFromUriPathSourceAsync(sourceFromPath,
                                                                   forceUseInternalDecoder,
                                                                   instance,
                                                                   useCacheProperty,
                                                                   pixelTransform);
            }

            if (sourceFromStream != null)
            {
                return await image.LoadImageFromStreamSourceAsync(sourceFromStream,
                                                                  forceUseInternalDecoder,
                                                                  instance,
                                                                  useCacheProperty,
                                                                  pixelTransform);
            }

            return false;
        }
        finally
        {
            if (isDisposeStream && sourceFromStream != null)
            {
                await sourceFromStream.DisposeAsync();
            }
        }
    }

    private static async ValueTask<bool> LoadImageFromUriPathSourceAsync(
        this Image             image,
        Uri                    sourceFromPath,
        bool                   forceUseInternalDecoder,
        LayeredBackgroundImage instance,
        DependencyProperty     useCacheProperty,
        IPixelTransform?       pixelTransform = null)
    {
        string extension = Path.GetExtension(sourceFromPath.ToString());
        bool isInternalFormat = LayeredBackgroundImage
                               .SupportedImageBitmapExtensionsLookup
                               .Contains(extension);
        bool isSvgFormat = LayeredBackgroundImage
                          .SupportedImageVectorExtensionsLookup
                          .Contains(extension);

        if ((forceUseInternalDecoder ||
             isInternalFormat ||
             isSvgFormat) && pixelTransform == null)
        {
            if (isSvgFormat)
            {
                image.Source = new SvgImageSource(sourceFromPath);
                return true;
            }

            image.Source = new BitmapImage(sourceFromPath)
            {
                CreateOptions = (bool)instance.GetValue(useCacheProperty)
                    ? BitmapCreateOptions.None
                    : BitmapCreateOptions.IgnoreImageCache
            };
            return true;
        }

        await using FileStream? fileStream = await TryGetStreamFromPathAsync(sourceFromPath, instance.MediaSourceCacheDir);
        if (fileStream == null)
        {
            return false;
        }

        // Borrow loading process from stream source loader.
        // We gonna ignore internal decoder enforcement here because, uh... idk.
        return await image
           .LoadImageFromStreamSourceAsync(fileStream,
                                           false,
                                           instance,
                                           useCacheProperty,
                                           pixelTransform);
    }

    private static async ValueTask<bool> LoadImageFromStreamSourceAsync(
        this Image             image,
        Stream                 sourceFromStream,
        bool                   forceUseInternalDecoder,
        LayeredBackgroundImage instance,
        DependencyProperty     useCacheProperty,
        IPixelTransform?       pixelTransform = null)
    {
        (Stream? stream, bool isTemporaryStream) =
            await sourceFromStream.GetNativeOrCopiedStreamIfNotSeekable(CancellationToken.None);

        if (stream == null)
        {
            return false;
        }

        try
        {
            // Reset position
            stream.Position = 0;

            // Guess codec type
            ImageExternalCodecType codecType = await stream.GuessImageFormatFromStreamAsync();
            if (codecType == ImageExternalCodecType.NotSupported)
            {
                return false;
            }

            // Reset position after format guessing
            stream.Position = 0;

            if ((forceUseInternalDecoder ||
                codecType == ImageExternalCodecType.Default) &&
                pixelTransform != null)
            {
                using IRandomAccessStream sourceRandomStream = stream.AsRandomAccessStream(true);
                BitmapImage bitmapImage = new()
                {
                    CreateOptions = (bool)instance.GetValue(useCacheProperty)
                        ? BitmapCreateOptions.None
                        : BitmapCreateOptions.IgnoreImageCache
                };
                await bitmapImage.SetSourceAsync(sourceRandomStream);
                image.Source = bitmapImage;
                return true;
            }

            if (codecType == ImageExternalCodecType.Svg)
            {
                using IRandomAccessStream sourceRandomStream = stream.AsRandomAccessStream(true);
                SvgImageSource            svgImageSource     = new();
                svgImageSource.SetSourceAsync(sourceRandomStream);
                image.Source = svgImageSource;
                return true;
            }

            FileStream? tempStream = null;

            try
            {
                string? userDefinedCacheDir = instance.MediaSourceCacheDir;
                bool    requireConversion   = true;

                if (!string.IsNullOrEmpty(userDefinedCacheDir) && stream is FileStream sourceAsFileStream)
                {
                    Directory.CreateDirectory(userDefinedCacheDir);

                    string cachedFilename = $"decoded_{Path.GetFileNameWithoutExtension(sourceAsFileStream.Name)}.png";
                    string decodedFilepath = Path.Combine(userDefinedCacheDir, cachedFilename);
                    FileInfo decodedFileInfo = new(decodedFilepath);

                    if (decodedFileInfo is { Exists: true, Length: > 256 })
                    {
                        requireConversion = false;
                    }

                    tempStream = decodedFileInfo.Open(FileMode.OpenOrCreate,
                                                      FileAccess.ReadWrite,
                                                      FileShare.ReadWrite);
                }

                tempStream ??= CreateTemporaryStream();

                if (requireConversion)
                {
                    // Use MagicScaler for external codec types
                    await Task.Run(() =>
                                   {
                                       ProcessImageSettings settings = new();
                                       settings.TrySetEncoderFormat(ImageMimeTypes.Png);

                                       using ProcessingPipeline pipeline = MagicImageProcessor.BuildPipeline(stream, settings);
                                       if (pixelTransform != null)
                                       {
                                           pipeline.AddTransform(pixelTransform);
                                       }

                                       pipeline.WriteOutput(tempStream);
                                   });
                }

                tempStream.Position = 0;
                using IRandomAccessStream tempRandomStream = tempStream.AsRandomAccessStream(true);
                BitmapImage tempBitmapImage = new()
                {
                    CreateOptions = (bool)instance.GetValue(useCacheProperty)
                        ? BitmapCreateOptions.None
                        : BitmapCreateOptions.IgnoreImageCache
                };
                await tempBitmapImage.SetSourceAsync(tempRandomStream);
                image.Source = tempBitmapImage;
                return true;
            }
            finally
            {
                if (tempStream != null)
                {
                    await tempStream.DisposeAsync();
                }
            }
        }
        finally
        {
            if (isTemporaryStream)
            {
                await stream.DisposeAsync();
            }
        }
    }

    private static async Task<FileStream?> TryGetStreamFromPathAsync(Uri? url, string? userDefinedTempDir)
    {
        if (url == null)
        {
            return null;
        }

        if (url.IsFile)
        {
            return File.Open(url.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        if (await GetFileLengthFromUrl(Client, url) is var fileLength &&
            fileLength == 0)
        {
            return null;
        }

        HttpRequestMessage requestMessage = new(HttpMethod.Get, url);
        HttpResponseMessage responseMessage =
            await Client.SendAsync(requestMessage,
                                   HttpCompletionOption.ResponseHeadersRead);

        if (!responseMessage.IsSuccessStatusCode)
        {
            responseMessage.Dispose();
            requestMessage.Dispose();
            return null;
        }

        FileStream tempStream = url.CreateTemporaryStreamFromUrl(userDefinedTempDir);
        if (tempStream.Length == fileLength)
        {
            return tempStream;
        }

        try
        {
            tempStream.SetLength(0); // Reset length.

            await using Stream networkStream = await responseMessage.Content.ReadAsStreamAsync();
            await networkStream.CopyToAsync(tempStream);
            return tempStream;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await tempStream.DisposeAsync();
        }

        return null;
    }

    private static async Task<long> GetFileLengthFromUrl(HttpClient client, Uri url)
    {
        HttpRequestMessage requestMessage = new(HttpMethod.Head, url);
        HttpResponseMessage responseMessage =
            await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        if (responseMessage.IsSuccessStatusCode)
        {
            return responseMessage.Content.Headers.ContentLength ?? 0;
        }

        responseMessage.Dispose();
        requestMessage.Dispose();
        return 0;
    }

    internal static double GetClampedVolume(this double thisVolume)
    {
        if (double.IsNaN(thisVolume) ||
            double.IsInfinity(thisVolume))
        {
            thisVolume = 0;
        }

        return Math.Clamp(thisVolume, 0, 100d) / 100d;
    }

    private static async ValueTask<(Stream? Stream, bool IsTemporaryStream)>
        GetNativeOrCopiedStreamIfNotSeekable<T>(
        this T            stream,
        CancellationToken token)
        where T : Stream
    {
        // If it's seekable, then just return it
        if (stream.CanSeek)
        {
            return (stream, false);
        }

        // Copy over
        FileStream tempStream = CreateTemporaryStream();
        try
        {
            await stream.CopyToAsync(tempStream, token);
            return (tempStream, true);
        }
        catch
        {
            await tempStream.DisposeAsync();
            return (null, false);
        }
    }

    private static FileStream CreateTemporaryStreamFromUrl(this Uri url, string? userDefinedTempDir)
    {
        string path;
        if (string.IsNullOrEmpty(userDefinedTempDir))
        {
            string extension = Path.GetExtension(url.AbsolutePath);
            path = Path.Combine(Path.GetTempPath(), Path.GetTempFileName() + extension);
        }
        else
        {
            Directory.CreateDirectory(userDefinedTempDir);
            path = Path.Combine(userDefinedTempDir, Path.GetFileName(url.AbsolutePath));
        }

        return File.Open(path,
                         new FileStreamOptions
                         {
                             Mode   = FileMode.Create,
                             Access = FileAccess.ReadWrite,
                             Share  = FileShare.ReadWrite,
                             Options = string.IsNullOrEmpty(userDefinedTempDir) ?
                                 FileOptions.None :
                                 FileOptions.DeleteOnClose
                         });
    }

    private static FileStream CreateTemporaryStream()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
        return File.Open(path,
                         new FileStreamOptions
                         {
                             Mode    = FileMode.Create,
                             Access  = FileAccess.ReadWrite,
                             Share   = FileShare.ReadWrite,
                             Options = FileOptions.DeleteOnClose
                         });
    }

    internal static WindowId GetElementWindowId(this LayeredBackgroundImage element)
    {
        XamlRoot root     = element.XamlRoot;
        WindowId windowId = root.ContentIslandEnvironment.AppWindowId;
        return windowId;
    }
}
