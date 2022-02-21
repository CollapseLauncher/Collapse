using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;

using Hi3Helper.Data;

using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static Hi3Helper.Shared.Region.InstallationManagement;

namespace CollapseLauncher.Dialogs
{
    public static class SimpleDialogs
    {
        public static async Task<ContentDialogResult> Dialog_UnhandledException(UIElement Content, Exception e) =>
               await SpawnDialog(
                       "Unhandled Error!",
                       string.Format("Oops! The app has suddenly can't continue this function with error details below:\n\n{0}", e.ToString()),
                       Content,
                       "Close",
                       null,
                       null,
                       ContentDialogButton.Close
                   );
        public static async Task<ContentDialogResult> Dialog_PreDownloadPackageVerified(UIElement Content, string hash) =>
               await SpawnDialog(
                       "Pre-Download Package is Verified!",
                       "Your Pre-Download Package is Ready and Verified!\r\n"
                       + "Hash: " + hash,
                       Content,
                       "Close",
                       null,
                       null,
                       ContentDialogButton.Secondary
                   );

        public static async Task<ContentDialogResult> Dialog_InstallationLocation(UIElement Content) =>
            await SpawnDialog(
                    "Locating Installation Folder",
                    "Before Installing the Game, Do you want to specify the location of the game?",
                    Content,
                    "Cancel",
                    "Use default directory",
                    "Locate location"
                );

        public static async Task<ContentDialogResult> Dialog_InsufficientWritePermission(UIElement Content, string path) =>
            await SpawnDialog(
                    "Unauthorized Location Choosen",
                    $"You have choosen a location that you don't have a permission to write on this path:\n\n{path}\n\nPlease choose another location!",
                    Content,
                    "Okay",
                    null,
                    null
                );

        public static async Task<ContentDialogResult> Dialog_InstallationLocateExisting(UIElement Content, string locationPath) =>
            await SpawnDialog(
                    "You've located to the Existing Installation",
                    $"We have detected the existing game in this path.\nLocation Path: {locationPath}",
                    Content,
                    "Cancel",
                    "Use default directory",
                    "Locate location"
                );

        public static async Task<ContentDialogResult> Dialog_InstallationDownloadAdditional(UIElement Content) =>
            await SpawnDialog(
                    "Download Additional Resources",
                    "Do you want to download the additional resources at the same time?\r\n"
                    + "So you wouldn't be necessarily asked to download additional resources in-game.",
                    Content,
                    null,
                    "Yes, please",
                    "No, thank you",
                    ContentDialogButton.Secondary
                );

        public static async Task<ContentDialogResult> Dialog_GraphicsVeryHighWarning(UIElement Content) =>
            await SpawnDialog(
                    "Very High Setting Selected!",
                    "You are about to set the setting to Very High!\r\n"
                    + "Remember that Very High setting is basically 2x Render Scale with MSAA Enabled and it's VERY UNOPTIMIZED!\r\n\r\nAre you sure to use this setting?",
                    Content,
                    null,
                    "Yes, I have a beefy PC!",
                    "No",
                    ContentDialogButton.Secondary
                );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallation(UIElement Content) =>
            await SpawnDialog(
                    "Existing Installation is Detected!",
                    string.Format(
                        "Game is already been installed on this location:\r\n\r\n{0}\r\n\r\n"
                        + "It's recommended to migrate the game to CollapseLauncher.\r\nHowever, you can still use Official Launcher to start the game."
                        + "\r\n\r\nDo you want to continue?", CurrentRegion.ActualGameDataLocation),
                    Content,
                    "Cancel",
                    "Yes, Migrate it",
                    "No, Keep Install it"
            );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationBetterLauncher(UIElement Content) =>
            await SpawnDialog(
                    "Existing Installation on BetterHI3Launcher is Detected!",
                    string.Format(
                        "Game is already been installed on this location:\r\n\r\n{0}\r\n\r\n"
                        + "It's recommended to migrate the game to CollapseLauncher.\r\nHowever, you can still use BetterHi3Launcher to start the game."
                        + "\r\n\r\nDo you want to continue?", CurrentRegion.BetterHi3LauncherConfig.game_info.install_path),
                    Content,
                    "Cancel",
                    "Yes, Migrate it",
                    "No, Keep Install it"
            );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationSteam(UIElement Content) =>
            await SpawnDialog(
                    "Existing Installation on Steam is Detected!",
                    string.Format(
                        "Game is already been installed on Steam in this location:\r\n\r\n{0}\r\n\r\n"
                        + "Do you want to convert this version to Normal Global version?\n"
                        + "Note: Once you converted it, you can't log-in with your steam account and only miHoYo/HoYoverse account login method is available."
                        + "\r\n\r\nDo you want to continue?", GamePathOnSteam),
                    Content,
                    "Cancel",
                    "Yes, Convert it",
                    "No, Keep Install it"
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionNoPermission(UIElement Content) =>
            await SpawnDialog(
                    "Folder Migration is Needed",
                    string.Format(
                        "You need to migrate the game folder to other location since the app doesn't have permission to do write activity.\n"
                        + "It's recommended to move the location to CollapseLauncher folder but you can also choose your own location.\n"
                        + "\nDo you want to move it to CollapseLauncher folder?"),
                    Content,
                    "Cancel",
                    "Yes",
                    "No, Other Location"
            );

        public static async Task<ContentDialogResult> Dialog_GameInstallationFileCorrupt(UIElement Content, string sourceHash, string downloadedHash) =>
            await SpawnDialog(
                    "Oops! Game Installation is Corrupted",
                    string.Format(
                        "Sorry but seems the downloaded Game Installation file."
                       + "\r\n\r\nServer Hash: {0}\r\nDownloaded Hash: {1}"
                       + "\r\n\r\nDo you want to rerdownload the file?", sourceHash, downloadedHash),
                    Content,
                    "No, Cancel",
                    "Yes, Redownload",
                    null
            );

        public static async Task<ContentDialogResult> Dialog_ExistingDownload(UIElement Content, long partialLength, long contentLength) =>
            await SpawnDialog(
                    "Resume Download?",
                    string.Format("You have downloaded {0}/{1} of the game previously.\r\n\r\nDo you want to continue?",
                                  ConverterTool.SummarizeSizeSimple(partialLength),
                                  ConverterTool.SummarizeSizeSimple(contentLength)),
                    Content,
                    null,
                    "Yes, Resume",
                    "No, Start from Beginning"
                );

        public static async Task<ContentDialogResult> SpawnDialog(
            string title, string content, UIElement Content,
            string closeText = null, string primaryText = null,
            string secondaryText = null, ContentDialogButton defaultButton = ContentDialogButton.Primary) =>
            await new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeText,
                PrimaryButtonText = primaryText,
                SecondaryButtonText = secondaryText,
                DefaultButton = defaultButton,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                XamlRoot = Content.XamlRoot
            }.ShowAsync();
    }
}
