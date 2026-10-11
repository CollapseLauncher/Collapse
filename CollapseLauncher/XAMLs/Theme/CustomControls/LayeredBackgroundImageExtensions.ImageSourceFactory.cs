using Hi3Helper.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    internal static async Task<ImageSource?> CreateImageSourceFromObjectAsync(object? source)
    {
        if (source is ImageSource sourceAsImageSource)
        {
            return sourceAsImageSource;
        }

        if (source is IRandomAccessStream or Stream)
        {
            ImageSource? imageSource = await CreateImageSourceFromStreamAsync(source);
            return imageSource;
        }

        if (source is Uri sourceAsUri ||
            (source is string sourceUriAsString &&
             Uri.TryCreate(sourceUriAsString, UriKind.Absolute, out sourceAsUri!)))
        {
            return CreateImageSourceFromUriExtension(sourceAsUri);
        }

        if (source is string sourceAsString)
        {
            sourceAsString = sourceAsString.GetFullyQualifiedPath();
            if (Uri.TryCreate(sourceAsString, UriKind.Absolute, out Uri? localPathUri))
            {
                return CreateImageSourceFromUriExtension(localPathUri);
            }
        }

        return null;
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
            : new BitmapImage(uri);
    }

    private static async Task<ImageSource?> CreateImageSourceFromStreamAsync(object? streamObj)
    {
        Stream? stream = streamObj as Stream;
        stream ??= (streamObj as IRandomAccessStream)?.AsStream();

        if (stream == null)
            return null;

        try
        {
            stream.Position = 0;
            Span<byte> bufferTemp = stackalloc byte[256];
            bufferTemp = bufferTemp[..stream.Read(bufferTemp)];

            stream.Position = 0;
            int indexOfLSign = bufferTemp.IndexOf((byte)'<');
            if (indexOfLSign > 0)
            {
                bufferTemp = bufferTemp[indexOfLSign..];
                byte sLetter = (byte)((bufferTemp.Length >= 3 ? 0 : bufferTemp[(indexOfLSign + 1)..][0]) | 0x20);
                byte vLetter = (byte)((bufferTemp.Length >= 3 ? 0 : bufferTemp[(indexOfLSign + 2)..][0]) | 0x20);
                byte gLetter = (byte)((bufferTemp.Length >= 3 ? 0 : bufferTemp[(indexOfLSign + 3)..][0]) | 0x20);
                if (sLetter == 's' &&
                    vLetter == 'v' &&
                    gLetter == 'g')
                {
                    SvgImageSource svgSource = new();
                    using IRandomAccessStream svgStreamWrapper = stream.AsRandomAccessStream();
                    await svgSource.SetSourceAsync(svgStreamWrapper);

                    return svgSource;
                }
            }

            SvgImageSource source = new();
            using IRandomAccessStream streamWrapper = stream.AsRandomAccessStream();
            await source.SetSourceAsync(streamWrapper);
            return source;
        }
        finally
        {
            stream.Position = 0;
            if (streamObj is IRandomAccessStream)
                await stream.DisposeAsync();
        }
    }
}
