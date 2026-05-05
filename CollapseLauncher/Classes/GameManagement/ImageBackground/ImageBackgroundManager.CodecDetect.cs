using CollapseLauncher.Dialogs;
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

    private async ValueTask<(bool IsSupported, bool IsVideo)> CheckCodecOrSpawnDialog(Uri? fileUri)
    {
        // -- Cancel if null
        if (fileUri == null)
        {
            return (false, false);
        }

        string filePath = fileUri.IsFile ? fileUri.LocalPath : fileUri.ToString();

        // -- Check for supported extension first
        if (!IsMediaFileExtensionSupported(filePath))
        {
            await SimpleDialogs.Dialog_SpawnMediaExtensionNotSupportedDialog(filePath);
            return (false, false);
        }

        // -- Check for supported image codec
        if (IsImageMediaFileExtensionSupported(filePath))
        {
            if (WindowsCodecHelper.IsFileSupportedImage(filePath))
            {
                return (true, false);
            }

            await SimpleDialogs.Dialog_SpawnImageNotSupportedDialog(filePath);
            return (false, false);
        }

        // -- Check for supported video codec
        if (WindowsCodecHelper.IsFileSupportedVideo(filePath,
                                                    out bool canPlayVideo,
                                                    out bool canPlayAudio,
                                                    out Guid videoCodecGuid,
                                                    out Guid audioCodecGuid) ||
            (GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable))
        {
            return (true, true);
        }

        return (await SimpleDialogs
                   .Dialog_SpawnVideoNotSupportedDialog(filePath,
                                                        canPlayVideo,
                                                        canPlayAudio,
                                                        videoCodecGuid,
                                                        audioCodecGuid), true);
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
