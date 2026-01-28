using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public static class ExtensionDetectUtility
{
    public static async ValueTask<ImageExternalCodecType> GuessImageFormatFromStreamAsync(
        this Stream       stream,
        CancellationToken token = default)
    {
        const int maxBuffer  = 1 << 10;
        byte[]    tempBuffer = ArrayPool<byte>.Shared.Rent(maxBuffer);

        try
        {
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

        // Checks for SVG
        var svgSig = "<svg"u8;
        bool isSvg = buffer.IndexOf(svgSig) >= 0;
        if (isSvg)
        {
            return ImageExternalCodecType.Svg;
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
