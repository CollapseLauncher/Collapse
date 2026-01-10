using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
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

        const int maxBuffer = 1 << 10;
        byte[] tempBuffer = ArrayPool<byte>.Shared.Rent(maxBuffer);
        try
        {
            await using Stream stream = await OpenStreamFromFileOrUrl(filePath, token);
            int read = await stream.ReadAsync(tempBuffer, token);

            scoped Span<byte> buffer = tempBuffer.AsSpan(0, read);
            if (IsHeaderInternalCodecImage(buffer) is var codecType &&
                codecType != ImageExternalCodecType.NotSupported)
            {
                return codecType;
            }

            return IsHeaderExternalCodecImage(buffer);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tempBuffer);
        }
    }

    private static ImageExternalCodecType IsHeaderInternalCodecImage(scoped Span<byte> buffer)
    {
        // Checks for JPEG
        ReadOnlySpan<byte> jpegSig1 = [0xFF, 0xD8, 0xFF];
        var jpegSigJfif = "JFIF"u8;
        var jpegSigExif = "Exif"u8;

        bool isJpeg = buffer.StartsWith(jpegSig1) && (buffer[6..].StartsWith(jpegSigJfif) || buffer[6..].StartsWith(jpegSigExif));
        if (isJpeg)
        {
            return ImageExternalCodecType.Default;
        }

        // Checks for PNG
        ReadOnlySpan<byte> pngSig = [0x89, 0x50, 0x4E, 0x47];

        bool isPng = buffer.StartsWith(pngSig);
        if (isPng)
        {
            return ImageExternalCodecType.Default;
        }

        // Checks for BMP
        var bmpSig = "BM"u8;

        bool isBmp = buffer.StartsWith(bmpSig);
        if (isBmp)
        {
            return ImageExternalCodecType.Default;
        }

        // Checks for GIF
        var gifSig1 = "GIF87a"u8;
        var gifSig2 = "GIF89a"u8;

        bool isGif = buffer.StartsWith(gifSig1) || buffer.StartsWith(gifSig2);
        if (isGif)
        {
            return ImageExternalCodecType.Default;
        }

        // Checks for TIFF
        var tiffSig1 = "II*\0"u8; // Little-endian
        var tiffSig2 = "MM\0*"u8; // Big-endian

        bool isTiff = buffer.StartsWith(tiffSig1) || buffer.StartsWith(tiffSig2);
        if (isTiff)
        {
            return ImageExternalCodecType.Default;
        }

        // Checks for ICO
        bool isIco = MemoryMarshal.Read<short>(buffer) == 0 &&           // Reserved must be 0
                     MemoryMarshal.Read<short>(buffer[2..]) is 1 or 2 && // Make sure if value is 1 or 2 (1 = ICO, 2 = CUR)
                     MemoryMarshal.Read<short>(buffer[4..]) > 0;         // Check for count always be > 0
        if (isIco)
        {
            return ImageExternalCodecType.Default;
        }

        return ImageExternalCodecType.NotSupported;
    }

    private static ImageExternalCodecType IsHeaderExternalCodecImage(scoped Span<byte> buffer)
    {
        // Checks for VP8
        if (buffer.StartsWith("RIFF"u8) &&
            buffer[8..].StartsWith("WEBPVP8"u8))
        {
            return ImageExternalCodecType.Webp;
        }

        // Checks for AVIF
        if (buffer[4..].StartsWith("ftypavif"u8))
        {
            return ImageExternalCodecType.Avif;
        }

        // Checks for HEIF/HEIC
        if (buffer[4..].StartsWith("ftyphe"u8))
        {
            return ImageExternalCodecType.Heic;
        }

        // Checks for JXR
        ReadOnlySpan<byte> jxrSig = [0x49, 0x49, 0xBC, 0x01, 0x08, 0x00, 0x00, 0x00];
        if (buffer.StartsWith(jxrSig))
        {
            return ImageExternalCodecType.Jxr;
        }

        return ImageExternalCodecType.NotSupported;
    }

}
