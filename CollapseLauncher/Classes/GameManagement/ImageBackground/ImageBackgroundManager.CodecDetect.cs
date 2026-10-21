using CollapseLauncher.Dialogs;
using CollapseLauncher.Helper.InternalPInvoke;
using CollapseLauncher.Helper.InternalPInvoke.FFmpeg;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using System;
using System.IO;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Codec Checks

    private async ValueTask<(bool IsSupported, bool ForceFFmpeg)> CheckCodecOrSpawnDialog(string? localPath)
    {
        if (localPath == null)
            return (false, false);

        // -- Check for supported extension first
        if (!IsMediaFileExtensionSupported(localPath))
        {
            await SimpleDialogs.Dialog_SpawnMediaExtensionNotSupportedDialog(localPath);
            return (false, false);
        }

        // -- Check for supported image codec
        if (IsImageMediaFileExtensionSupported(localPath))
        {
            if (WindowsCodecHelper.IsFileSupportedImage(localPath))
            {
                return (true, false);
            }

            await SimpleDialogs.Dialog_SpawnImageNotSupportedDialog(localPath);
            return (false, false);
        }

        // -- Check using FFmpeg if available
        MediaSupport    codecInfo   = FFmpegPInvoke.GetMediaSupport(localPath);
        PixelColorModel pixelFormat = codecInfo.VideoPixelFormatInfo.ColorModel;
        if ((GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable) ||
            (codecInfo.Video.DecoderAvailable && pixelFormat.HasFlag(PixelColorModel.Rgb)))
        {
            return (true, true);
        }

        // -- Check for supported video codec
        if (WindowsCodecHelper.IsFileSupportedVideo(localPath,
                                                    out bool canPlayVideo,
                                                    out bool canPlayAudio,
                                                    out Guid videoCodecGuid,
                                                    out Guid audioCodecGuid) ||
            (GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable))
        {
            return (true, false);
        }

        return (await SimpleDialogs
                   .Dialog_SpawnVideoNotSupportedDialog(localPath,
                                                        canPlayVideo,
                                                        canPlayAudio,
                                                        videoCodecGuid,
                                                        audioCodecGuid), false);
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

    public static bool IsVideoMediaFileExtensionSupported(ReadOnlySpan<char> filePath)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(filePath);
        return LayeredBackgroundImage.SupportedVideoExtensionsLookup.Contains(extension);
    }
}
