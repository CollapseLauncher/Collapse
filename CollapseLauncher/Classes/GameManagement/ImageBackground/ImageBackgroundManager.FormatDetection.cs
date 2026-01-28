using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable CheckNamespace

#pragma warning disable IDE0130
#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    private static async ValueTask<ImageExternalCodecType> TryGetImageCodecType(string filePath, CancellationToken token)
    {
        ReadOnlySpan<char> fileExt = Path.GetExtension(filePath);
        // Try to check if extension is a video file, then ignore and return default.
        if (LayeredBackgroundImage.SupportedVideoExtensionsLookup
                                  .Contains(fileExt))
        {
            return ImageExternalCodecType.Default;
        }

        // Try to determine from file extension
        if (LayeredBackgroundImage.SupportedImageBitmapExtensionsLookup
                                  .Contains(fileExt))
        {
            return ImageExternalCodecType.Default;
        }

        // Try to determine from file extension
        if (LayeredBackgroundImage.SupportedImageVectorExtensionsLookup
                                  .Contains(fileExt))
        {
            return ImageExternalCodecType.Svg;
        }

        if (fileExt.EndsWith("webp", StringComparison.OrdinalIgnoreCase))
        {
            return ImageExternalCodecType.Webp;
        }

        if (fileExt.EndsWith("jxr", StringComparison.OrdinalIgnoreCase))
        {
            return ImageExternalCodecType.Jxr;
        }

        if (fileExt.EndsWith("heif", StringComparison.OrdinalIgnoreCase) ||
            fileExt.EndsWith("heic", StringComparison.OrdinalIgnoreCase))
        {
            return ImageExternalCodecType.Heic;
        }

        if (fileExt.EndsWith("avif", StringComparison.OrdinalIgnoreCase))
        {
            return ImageExternalCodecType.Avif;
        }

        await using Stream stream = await OpenStreamFromFileOrUrl(filePath, token);
        return await stream.GuessImageFormatFromStreamAsync(token);
    }
}
