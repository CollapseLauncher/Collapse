using Hi3Helper.Data;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    internal static async Task<(ImageSource? Source, bool AlreadyLoaded)> CreateImageSourceFromObjectAsync(
        object?           source,
        CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return (null, false);

        if (source is ImageSource sourceAsImageSource)
        {
            return (sourceAsImageSource, true);
        }

        if (source is IRandomAccessStream or Stream)
        {
            (ImageSource? imageSource, bool alreadyLoaded) = await CreateImageSourceFromStreamAsync(source, token);
            return (imageSource, alreadyLoaded);
        }

        if (source is Uri sourceAsUri ||
            (source is string sourceUriAsString &&
             Uri.TryCreate(sourceUriAsString, UriKind.Absolute, out sourceAsUri!)))
        {
            return (CreateImageSourceFromUriExtension(sourceAsUri), false);
        }

        if (source is string sourceAsString)
        {
            sourceAsString = sourceAsString.GetFullyQualifiedPath();
            if (Uri.TryCreate(sourceAsString, UriKind.Absolute, out Uri? localPathUri))
            {
                return (CreateImageSourceFromUriExtension(localPathUri), false);
            }
        }

        return (null, false);
    }

    private static ImageSource CreateImageSourceFromUriExtension(Uri uri)
    {
        ReadOnlySpan<char> pathAndQuery = uri.PathAndQuery;
        int indexOfQuery = pathAndQuery.IndexOf('?');
        if (indexOfQuery >= 0)
        {
            pathAndQuery = pathAndQuery[..indexOfQuery];
        }

        ReadOnlySpan<char> extension = Path.GetExtension(pathAndQuery);
        return LayeredBackgroundImage
              .SupportedImageVectorExtensionsLookup
              .Contains(extension)
            ? new SvgImageSource(uri)
            : new BitmapImage(uri)
            {
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };
    }

    private static async Task<(ImageSource? Source, bool AlreadyLoaded)>
        CreateImageSourceFromStreamAsync(
            object?           streamObj,
            CancellationToken token)
    {
        if (token.IsCancellationRequested)
            return (null, false);

        Stream? stream = streamObj as Stream;
        stream ??= (streamObj as IRandomAccessStream)?.AsStream();

        if (stream == null)
            return (null, false);

        byte[] bufferTemp = ArrayPool<byte>.Shared.Rent(1 << 10);
        var    buffer     = new Memory<byte>(bufferTemp);
        try
        {
            stream.Position = 0;
            Span<byte> bufferSpan = buffer[..await stream.ReadAsync(buffer, token)].Span;

            stream.Position = 0;
            int indexOfLSign = bufferSpan.IndexOf((byte)'<');
            if (indexOfLSign > 0)
            {
                bufferSpan = bufferSpan[indexOfLSign..];
                byte sLetter = (byte)((bufferSpan.Length >= 3 ? 0 : bufferSpan[(indexOfLSign + 1)..][0]) | 0x20);
                byte vLetter = (byte)((bufferSpan.Length >= 3 ? 0 : bufferSpan[(indexOfLSign + 2)..][0]) | 0x20);
                byte gLetter = (byte)((bufferSpan.Length >= 3 ? 0 : bufferSpan[(indexOfLSign + 3)..][0]) | 0x20);
                if (sLetter == 's' &&
                    vLetter == 'v' &&
                    gLetter == 'g')
                {
                    SvgImageSource            svgSource        = new();
                    using IRandomAccessStream svgStreamWrapper = stream.AsRandomAccessStream(true, true);

                    if (token.IsCancellationRequested)
                        return (null, false);

                    await svgSource.SetSourceAsync(svgStreamWrapper);
                    return (svgSource, true);
                }
            }

            if (token.IsCancellationRequested)
                return (null, false);

            BitmapImage source = new()
            {
                CreateOptions = BitmapCreateOptions.IgnoreImageCache
            };
            using IRandomAccessStream streamWrapper = stream.AsRandomAccessStream(true, true);
            if (token.IsCancellationRequested)
                return (null, false);

            await source.SetSourceAsync(streamWrapper);
            return (source, true);
        }
        finally
        {
            stream.Position = 0;
            if (streamObj is IRandomAccessStream)
                await stream.DisposeAsync();
        }
    }
}
