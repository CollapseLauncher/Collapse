using System;
using System.Collections.Generic;
using System.Linq;

namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    /// <summary>
    /// All the sources in this list are common image formats supported by Windows Imaging Component (WIC).
    /// Some formats might require additional codecs to be installed.
    /// <br/><br/>
    /// The extensions list are taken from:<br/>
    /// https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types#jpeg_joint_photographic_experts_group_image
    /// </summary>
    public static readonly HashSet<string> SupportedImageBitmapExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".jpg", ".jpeg", ".jpe", ".jif", ".jfif", // "image/jpeg"
        ".apng", ".png",                          // "image/apng" and "image/png"
        ".bmp",                                   // "image/bmp"
        ".gif",                                   // "image/gif"
        ".ico",                                   // "image/x-icon"
        ".tif", ".tiff"                           // "image/tiff"
    ];
    public static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageBitmapExtensionsLookup =
        SupportedImageBitmapExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    public static readonly HashSet<string> SupportedImageBitmapExternalCodecExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".jxr",  // "image/jxr"  (Requires additional codec)
        ".avif", // "image/avif" (Requires additional codec)
        ".heic", // "image/heic" (Requires additional codec)
        ".heif", // "image/heif" (Requires additional codec)
        ".webp"  // "image/webp" (Requires additional codec)
    ];
    public static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageBitmapExternalCodecExtensionsLookup =
        SupportedImageBitmapExternalCodecExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    public static readonly HashSet<string> SupportedImageVectorExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".svg" // "image/svg"
    ];
    public static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageVectorExtensionsLookup =
        SupportedImageVectorExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    public static readonly HashSet<string> SupportedVideoExtensions =
    [
        with(StringComparer.OrdinalIgnoreCase),
        ".3gp", ".3gp2",                                                // "video/3gp"
        ".asf", ".wmv",                                                 // "video/wmv"
        ".avi",                                                         // "video/avi"
        ".flv", ".f4v",                                                 // "video/flv"
        ".mp4", ".m4v",                                                 // "video/mp4"
        ".mov", ".movie", ".qt",                                        // "video/quicktime"
        ".webm",                                                        // "video/webm"
        ".mpg", ".mpeg", ".ts", ".tsv", ".ps", ".m2ts", ".mts", ".vob", // "video/mpeg"
        ".ogv",                                                         // "video/ogg"
        ".mkv", ".mks"                                                  // "video/matroska"
    ];
    public static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedVideoExtensionsLookup =
        SupportedVideoExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    public static readonly string  SupportedImageFormatsFilter =
        string.Join(";", SupportedImageBitmapExtensions.Select(x => $"*{x}")) + ";" +
        string.Join(";", SupportedImageBitmapExternalCodecExtensions.Select(x => $"*{x}")) + ";" +
        string.Join(";", SupportedImageVectorExtensions.Select(x => $"*{x}"));

    public static readonly string SupportedVideoFormatsFilter =
        string.Join(";", SupportedVideoExtensions.Select(x => $"*{x}"));

    public static readonly Dictionary<string, string> SupportedAllBackgroundFormatsFilter =
        new() {
            { "All supported formats", SupportedImageFormatsFilter + ';' + SupportedVideoFormatsFilter },
            { "Image formats", SupportedImageFormatsFilter },
            { "Video formats", SupportedVideoFormatsFilter }
        };
}