using Hi3Helper.Win32.WinRT.WindowsCodec;
using Microsoft.UI.Xaml.Media;
using System;
using System.IO;
using Windows.Media.Core;
using Windows.Storage.Streams;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

[Flags]
public enum SourceFormatType
{
    Unknown,
    Image,
    Video
}

public enum SourceObjectType
{
    Unknown,
    Stream,
    IRandomAccessStream,
    ImageSource,
    MediaSource,
    URL
}

public struct SourceGuessResult
{
    public SourceFormatType FormatType;
    public SourceObjectType ObjectType;
}

internal partial class LayeredBackgroundImageExtensions
{
    public static SourceGuessResult GuessSourceType(object? source)
    {
        SourceFormatType formatType = default;
        SourceObjectType objectType = default;

        switch (source)
        {
            // -- Check if source is either MediaSource or ImageSource respectively.
            case MediaSource:
                objectType = SourceObjectType.MediaSource;
                formatType = SourceFormatType.Video;
                goto Result;
            case ImageSource:
                objectType = SourceObjectType.ImageSource;
                formatType = SourceFormatType.Image;
                goto Result;
        }

        Stream? lendWrapperStream = null;
        if (source is IRandomAccessStream { CanRead: true } sourceAsIRAS)
        {
            objectType = SourceObjectType.IRandomAccessStream;
            source     = lendWrapperStream = sourceAsIRAS.AsStream();

            // Borrow check from Stream type. Continue...
        }

        if (source is Stream { CanRead: true, CanSeek: true } sourceAsStream)
        {
            if (objectType == SourceObjectType.Unknown)
                objectType = SourceObjectType.Stream;

            sourceAsStream.Position = 0;
            if (WindowsCodecHelper.IsFileSupportedImage(sourceAsStream))
            {
                formatType = SourceFormatType.Image;
            }

            // Try to guess with path extension if media is coming from a FileStream
            // and have an extension. We can't determine the format from its header for now.
            if (formatType == SourceFormatType.Unknown &&
                sourceAsStream is FileStream sourceAsFileStream &&
                !string.IsNullOrEmpty(Path.GetExtension(sourceAsFileStream.Name)))
            {
                source = new Uri(sourceAsFileStream.Name);
            }

            // Dispose IRandomAccessStream wrapper if assigned.
            lendWrapperStream?.Dispose();
        }

        // -- Check the source types if it's either Uri or a string.
        if (source is Uri or string)
        {
            goto GuessFromPath;
        }

        goto Result;

    GuessFromPath:
        string stringifySource = source is Uri sourceAsUri
            ? sourceAsUri.IsFile ? sourceAsUri.LocalPath : sourceAsUri.ToString()
            : (string)source;

        ReadOnlySpan<char> fileExtension = Path.GetExtension(stringifySource);
        if (LayeredBackgroundImage.SupportedImageFormatsFilter
                                  .Contains(fileExtension, StringComparison.OrdinalIgnoreCase))
        {
            objectType = SourceObjectType.URL;
            formatType = SourceFormatType.Image;
            goto Result;
        }

        if (LayeredBackgroundImage.SupportedVideoFormatsFilter
                                  .Contains(fileExtension, StringComparison.OrdinalIgnoreCase))
        {
            objectType = SourceObjectType.URL;
            formatType = SourceFormatType.Video;
        }

    Result:
        return new SourceGuessResult
        {
            FormatType = formatType,
            ObjectType = objectType
        };
    }
}