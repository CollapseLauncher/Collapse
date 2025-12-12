using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Transforms;
using PhotoSauce.NativeCodecs.Libheif;
using PhotoSauce.NativeCodecs.Libjxl;
using PhotoSauce.NativeCodecs.Libwebp;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Storage.Streams;
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable ConvertIfStatementToSwitchStatement

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

internal static class LayeredBackgroundImageExtensions
{
    private static readonly HttpClient Client;

    static LayeredBackgroundImageExtensions()
    {
        // Initialize support for MagicScaler's WebP decoding
        CodecManager.Configure(InitializeMagicScalerCodecs);
        Client = CreateHttpClient();
    }

    private static void InitializeMagicScalerCodecs(CodecCollection codecs)
    {
        codecs.UseWicCodecs(WicCodecPolicy.All);
        codecs.UseLibwebp();
        codecs.UseLibheif();
        codecs.UseLibjxl();
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

    extension(Image image)
    {
        internal async ValueTask<bool> LoadImageAsync(Uri?                   sourceFromPath,
                                                      Stream?                sourceFromStream,
                                                      LayeredBackgroundImage instance,
                                                      IPixelTransform?       pixelTransform = null)
        {
            IMediaCacheHandler? cacheHandler    = instance.MediaCacheHandler;
            bool                isDisposeStream = sourceFromStream == null;

            if (cacheHandler != null)
            {
                object? source = sourceFromPath;
                source ??= sourceFromStream;
                MediaCacheResult cacheResult = await cacheHandler.LoadCachedSource(source);

                if (cacheResult == null!)
                {
                    return false;
                }

                Uri? cachedUrlSource = null;
                Stream? cachedStreamSource = null;

                if (cacheResult.CachedSource is string asString)
                {
                    cachedUrlSource = asString.GetStringAsUri();
                }

                if (cacheResult.CachedSource is Stream asStream)
                {
                    cachedStreamSource = asStream;
                }

                if (cacheResult.DisposeStream &&
                    cachedStreamSource != null)
                {
                    isDisposeStream = true;
                }

                if (cacheResult.ForceUseInternalDecoder &&
                    await image.LoadImageWithInternalDecoderAsync(sourceFromPath,
                                                                  sourceFromStream,
                                                                  true,
                                                                  isDisposeStream))
                {
                    return true;
                }

                sourceFromPath   = cachedUrlSource;
                sourceFromStream = cachedStreamSource;
            }

            if (pixelTransform == null &&
                await image.LoadImageWithInternalDecoderAsync(sourceFromPath,
                                                              sourceFromStream))
            {
                return true;
            }

            Stream? sourceStream = sourceFromStream ?? await TryGetStreamFromPathAsync(sourceFromPath);
            if (sourceStream is not { CanSeek: true, CanRead: true })
            {
                return false;
            }

            try
            {
                string temporaryFilePath = Path.Combine(Path.GetTempPath(), Path.GetTempFileName());
                await using FileStream decodedBitmapStream =
                    File.Open(temporaryFilePath,
                              new FileStreamOptions
                              {
                                  Mode    = FileMode.Create,
                                  Access  = FileAccess.ReadWrite,
                                  Share   = FileShare.ReadWrite,
                                  Options = FileOptions.DeleteOnClose
                              });

                await Task.Run(() =>
                {
                    using ProcessingPipeline pipeline =
                        MagicImageProcessor.BuildPipeline(sourceStream, ProcessImageSettings.Default);

                    // For adding Waifu2X transform support later.
                    if (pixelTransform != null)
                    {
                        pipeline.AddTransform(pixelTransform);
                    }

                    pipeline.WriteOutput(decodedBitmapStream);
                });
                using IRandomAccessStream randomStream = decodedBitmapStream.AsRandomAccessStream();

                decodedBitmapStream.Position = 0;
                BitmapImage bitmapImage = new();
                image.Source = bitmapImage;

                await bitmapImage.SetSourceAsync(randomStream);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
            finally
            {
                if (isDisposeStream)
                {
                    await sourceStream.DisposeAsync();
                }
            }
        }

        private async ValueTask<bool> LoadImageWithInternalDecoderAsync(Uri?    sourceFromPath,
                                                                        Stream? sourceFromStream,
                                                                        bool    force         = false,
                                                                        bool    disposeStream = false)
        {
            string? filePath = sourceFromPath?.AbsolutePath;
            filePath ??= (sourceFromStream as FileStream)?.Name;

            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            if (!force &&
                LayeredBackgroundImage.SupportedImageBitmapExternalCodecExtensionsLookup
                                      .Contains(extension))
            {
                return false;
            }

            bool    isDisposeStream = disposeStream || sourceFromStream == null;
            Stream? sourceStream    = sourceFromStream ?? await TryGetStreamFromPathAsync(sourceFromPath);
            if (sourceStream is not { CanSeek: true, CanRead: true })
            {
                return false;
            }

            try
            {
                IRandomAccessStream randomStream = sourceStream.AsRandomAccessStream();
                BitmapImage bitmapImage = new();
                image.Source = bitmapImage;

                await bitmapImage.SetSourceAsync(randomStream);
                if (isDisposeStream)
                {
                    randomStream.Dispose();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
            finally
            {
                if (isDisposeStream)
                {
                    await sourceStream.DisposeAsync();
                }
            }
        }
    }

    private static async Task<Stream?> TryGetStreamFromPathAsync(Uri? url, long? minLengthRequested = null)
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

        minLengthRequested ??= fileLength;

        try
        {
            int                writtenLength   = (int)minLengthRequested;
            byte[]             requestedBuffer = GC.AllocateUninitializedArray<byte>(writtenLength);
            await using Stream networkStream   = await responseMessage.Content.ReadAsStreamAsync();

            Memory<byte> buffer = requestedBuffer;
            while (writtenLength > 0)
            {
                int read = await networkStream.ReadAsync(buffer);
                if (read == 0)
                {
                    break;
                }
                writtenLength -= read;
                buffer        = buffer[read..];
            }

            return new MemoryStream(requestedBuffer);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
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
}
