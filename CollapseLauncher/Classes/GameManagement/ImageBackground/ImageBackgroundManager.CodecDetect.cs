using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper.InternalPInvoke;
using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using System;
using System.IO;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Codec Checks

    private async ValueTask<(bool IsSupported, bool IsVideo, bool ForceFFmpeg)> CheckCodecOrSpawnDialog(Uri? fileUri)
    {
        // -- Cancel if null or URI is not a local file
        if (fileUri == null || !fileUri.IsFile)
        {
            return (false, false, false);
        }

        string filePath = fileUri.LocalPath;

        // -- Check for supported extension first
        if (!IsMediaFileExtensionSupported(filePath))
        {
            await SimpleDialogs.Dialog_SpawnMediaExtensionNotSupportedDialog(filePath);
            return (false, false, false);
        }

        // -- Check for supported image codec
        if (IsImageMediaFileExtensionSupported(filePath))
        {
            if (WindowsCodecHelper.IsFileSupportedImage(filePath))
            {
                return (true, false, false);
            }

            await SimpleDialogs.Dialog_SpawnImageNotSupportedDialog(filePath);
            return (false, false, false);
        }

        // -- Check using FFmpeg if available
        MediaSupport    codecInfo   = FFmpegPInvoke.GetMediaSupport(filePath);
        PixelColorModel pixelFormat = codecInfo.VideoPixelFormatInfo.ColorModel;
        if ((GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable) ||
            (codecInfo.Video.DecoderAvailable && pixelFormat.HasFlag(PixelColorModel.Rgb)))
        {
            return (true, true, true);
        }

        // -- Check for supported video codec
        if (WindowsCodecHelper.IsFileSupportedVideo(filePath,
                                                    out bool canPlayVideo,
                                                    out bool canPlayAudio,
                                                    out Guid videoCodecGuid,
                                                    out Guid audioCodecGuid) ||
            (GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable))
        {
            return (true, true, true);
        }

        return (await SimpleDialogs
                   .Dialog_SpawnVideoNotSupportedDialog(filePath,
                                                        canPlayVideo,
                                                        canPlayAudio,
                                                        videoCodecGuid,
                                                        audioCodecGuid), true, false);
    }

    #endregion

    private static bool IsMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        return IsImageMediaFileExtensionSupported(filePath) ||
               IsVideoMediaFileExtensionSupported(filePath);
    }

    private static bool IsImageMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(filePath);
        return LayeredBackgroundImage.SupportedImageBitmapExtensionsLookup.Contains(extension) ||
               LayeredBackgroundImage.SupportedImageBitmapExternalCodecExtensionsLookup.Contains(extension) ||
               LayeredBackgroundImage.SupportedImageVectorExtensionsLookup.Contains(extension);
    }

    private static bool IsVideoMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(filePath);
        return LayeredBackgroundImage.SupportedVideoExtensionsLookup.Contains(extension);
    }
}
