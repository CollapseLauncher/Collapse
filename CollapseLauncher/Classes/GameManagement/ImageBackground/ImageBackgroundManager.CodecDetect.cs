using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;

// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    private static async Task<bool> CheckCodecOrSpawnDialog(Uri? fileUri)
    {
        // -- Cancel if null
        if (fileUri == null)
        {
            return false;
        }

        string filePath = fileUri.IsFile ? fileUri.LocalPath : fileUri.ToString();

        // -- Check for supported extension first
        if (!IsMediaFileExtensionSupported(filePath))
        {
            await SpawnMediaExtensionNotSupportedDialog(filePath);
            return false;
        }

        // -- Check for supported image codec
        if (IsImageMediaFileExtensionSupported(filePath))
        {
            if (WindowsCodecHelper.IsFileSupportedImage(filePath))
            {
                return true;
            }

            await SpawnImageNotSupportedDialog(filePath);
            return false;
        }

        // -- Check for supported video codec
        if (IsVideoMediaFileExtensionSupported(filePath))
        {
            if (WindowsCodecHelper.IsFileSupportedVideo(filePath,
                                                        out bool canPlayVideo,
                                                        out bool canPlayAudio,
                                                        out Guid videoCodecGuid,
                                                        out Guid audioCodecGuid) ||
                IsFfmpegAvailable())
            {
                return true;
            }

            return await SpawnVideoNotSupportedDialog(filePath,
                                                      canPlayVideo,
                                                      canPlayAudio,
                                                      videoCodecGuid,
                                                      audioCodecGuid);
        }

        return true;
    }

    private static async Task SpawnMediaExtensionNotSupportedDialog(string filePath)
    {
        TextBlock textBlock = UIElementExtensions.CreateElementFromUIThread<TextBlock>(x =>
                                                  {
                                                      x.TextWrapping = TextWrapping.WrapWholeWords;
                                                  })
                                                 .AddTextBlockLine("Sorry but the file extension for the following background image/video file is not supported.")
                                                 .AddTextBlockNewLine(2)
                                                 .AddTextBlockLine(string.Format("File Path/URL: {0}", filePath));
        await SimpleDialogs.SpawnDialog("Background File is not Supported",
                                        textBlock,
                                  null,
                                  Locale.Lang._Misc.OkaySad,
                                        defaultButton: ContentDialogButton.Close,
                                        dialogTheme: ContentDialogTheme.Error);
    }

    private static async Task SpawnImageNotSupportedDialog(string filePath)
    {
        TextBlock textBlock = UIElementExtensions.CreateElementFromUIThread<TextBlock>(x =>
                                                  {
                                                      x.TextWrapping = TextWrapping.WrapWholeWords;
                                                  })
                                                 .AddTextBlockLine("Sorry but the following background image file is not supported by internal Windows Imaging Component (WIC) decoder. Make sure you have installed the decoder from Microsoft Store.")
                                                 .AddTextBlockNewLine(2)
                                                 .AddTextBlockLine(string.Format("File Path/URL: {0}", filePath));
        await SimpleDialogs.SpawnDialog("Image Background File is not Supported",
                                        textBlock,
                                        null,
                                        Locale.Lang._Misc.OkaySad,
                                        defaultButton: ContentDialogButton.Close,
                                        dialogTheme: ContentDialogTheme.Error);
    }

    private static async Task<bool> SpawnVideoNotSupportedDialog(string filePath,
                                                                 bool   canPlayVideo,
                                                                 bool   canPlayAudio,
                                                                 Guid   videoCodecGuid,
                                                                 Guid   audioCodecGuid)
    {
        WindowsCodecHelper.TryGetFourCCString(in videoCodecGuid,
                                              out string? videoCodecString);

        videoCodecString ??= "Unknown";

        string useInternalMfLocale = "Install Codecs from Microsoft Store";
        string useFfmpegLocale     = "Install & Use FFmpeg Decoder";

        TextBlock textBlock = UIElementExtensions.CreateElementFromUIThread<TextBlock>(x =>
                                                  {
                                                      x.TextWrapping = TextWrapping.WrapWholeWords;
                                                  })
                                                 .AddTextBlockLine("We have detected that the video background cannot be played due to missing decoder with details below:")
                                                 .AddTextBlockNewLine(2)
                                                 .AddTextBlockLine(string.Format("File Path/URL: {0}", filePath), size: 11, weight: FontWeights.Bold)
                                                 .AddTextBlockNewLine()
                                                 .AddTextBlockLine(string.Format("Video Codec FourCC: {0}", videoCodecString), size: 11, weight: FontWeights.Bold)
                                                 .AddTextBlockNewLine()
                                                 .AddTextBlockLine(string.Format("Video Codec GUID: {0}", videoCodecGuid), size: 11, weight: FontWeights.Bold)
                                                 .AddTextBlockNewLine()
                                                 .AddTextBlockLine(string.Format("Audio Codec GUID: {0}", audioCodecGuid), size: 11, weight: FontWeights.Bold)
                                                 .AddTextBlockNewLine()
                                                 .AddTextBlockLine(string.Format("Can Play Video/Audio?: (Video: {0} | Audio: {1})", canPlayVideo, canPlayAudio), size: 11, weight: FontWeights.Bold)
                                                 .AddTextBlockNewLine(2)
                                                 .AddTextBlockLine(string.Format("We suggest you to install required video/audio codecs from Microsoft Store by clicking \"{0}\" or use FFmpeg by clicking \"{1}\" button below.", useInternalMfLocale, useFfmpegLocale))
                                                 .AddTextBlockNewLine(2)
                                                 .AddTextBlockLine("Note:", size: 11, weight: FontWeights.Bold)
                                                 .AddTextBlockNewLine()
                                                 .AddTextBlockLine("We heavily recommend you to use FFmpeg as it broadly supports wide range of video/audio codecs and HW Decoding capability (depends on your hardware).", size: 11);

        StackPanel panel = UIElementExtensions.CreateStackPanel();
        panel.AddElementToStackPanel(textBlock);

        Button buttonIconCopyDetails = UIElementExtensions.CreateButtonWithIcon<Button>("Copy Details", textSize: 12d, textWeight: FontWeights.Bold)
                                                          .WithHorizontalAlignment(HorizontalAlignment.Left)
                                                          .WithMargin(0, 16, 0, 0);
        panel.AddElementToStackPanel(buttonIconCopyDetails);
        buttonIconCopyDetails.Click += ButtonIconCopyDetailsOnClick;
        buttonIconCopyDetails.Unloaded += ButtonIconCopyDetailsOnUnloaded;

        ContentDialogResult result = await SimpleDialogs
           .SpawnDialog("Video Background Codec is not Supported",
                        panel,
                        null,
                        Locale.Lang._Misc.Close,
                        useFfmpegLocale,
                        useInternalMfLocale,
                        defaultButton: ContentDialogButton.Primary,
                        dialogTheme: ContentDialogTheme.Error);

        switch (result)
        {
            case ContentDialogResult.Primary:
                return await SpawnFfmpegInstallDialog();
            case ContentDialogResult.Secondary:
                return await SpawnMediaFoundationCodecInstallDialog();
            case ContentDialogResult.None:
            default:
                return false;
        }

        void ButtonIconCopyDetailsOnClick(object sender, RoutedEventArgs e)
        {
            string detailStrings = $"""
                                    File Path/URL: {filePath}
                                    
                                    Video Codec FourCC Type: {videoCodecString}
                                    Video Codec GUID: {videoCodecGuid}
                                    Can Play Video Codec: {canPlayVideo}
                                    
                                    Audio Codec GUID: {audioCodecGuid}
                                    Can Play Audio Codec: {canPlayAudio}
                                    """;
            Clipboard.CopyStringToClipboard(detailStrings);
        }

        void ButtonIconCopyDetailsOnUnloaded(object sender, RoutedEventArgs e)
        {
            buttonIconCopyDetails.Click    -= ButtonIconCopyDetailsOnClick;
            buttonIconCopyDetails.Unloaded -= ButtonIconCopyDetailsOnUnloaded;
        }
    }

    internal static bool IsFfmpegAvailable()
    {
        return false;
    }

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

    internal static async Task<bool> SpawnMediaFoundationCodecInstallDialog()
    {
        return true;
    }

    internal static async Task<bool> SpawnFfmpegInstallDialog()
    {
        return true;
    }
}
