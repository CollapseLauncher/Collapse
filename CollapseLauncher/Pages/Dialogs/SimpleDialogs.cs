using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

using Hi3Helper.Data;

using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Shared.Region.LauncherConfig;

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
        public static async Task<ContentDialogResult> Dialog_PreDownloadExtraPackageNotif(UIElement Content, long packageSize, long addtSize) =>
               await SpawnDialog(
                       "Additional Package is Required!",
                       "Some additional package will be downloaded. Make sure that your disk space is enough.\r\n"
                       + "Necessary Package Size: " + SummarizeSizeSimple(packageSize) + "\r\nAdditional Package Size: " + SummarizeSizeSimple(addtSize),
                       Content,
                       "Okay",
                       null,
                       null,
                       ContentDialogButton.None
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

        public static async Task<int> Dialog_ChooseAudioLanguage(UIElement Content, List<string> langlist)
        {
            // Default: 2 (Japanese)
            int index = 2;
            StackPanel Panel = new StackPanel();
            ComboBox LangBox = new ComboBox()
            {
                PlaceholderText = "Select your Audio Language",
                Width = 256,
                ItemsSource = langlist,
                SelectedIndex = index,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Panel.Children.Add(new TextBlock()
            {
                Text = $"Before you install the game, you need to choose which audio language you want to use (Default: Japanese):",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0,0,0,16)
            });
            Panel.Children.Add(LangBox);
            await SpawnDialog("Choose Audio Language", Panel, Content, null, "Next", null);

            index = LangBox.SelectedIndex;

            return index;
        }

        public static async Task<ContentDialogResult> Dialog_AdditionalDownloadNeeded(UIElement Content, long fileSize) =>
            await SpawnDialog(
                    "Additional Download is Needed",
                    $"You have to download atleast {SummarizeSizeSimple(fileSize)} of additional/persistent files. Although you can skip and download it in-game instead."
                    + "\nDo you want to continue?",
                    Content,
                    "Skip",
                    "Yes, proceed",
                    null
                );

        public static async Task<ContentDialogResult> Dialog_AdditionalDownloadCompleted(UIElement Content) =>
            await SpawnDialog(
                    "Additional Download is Completed!",
                    $"Aditional/Persistent Download has been completed! You can now enjoy your game."
                    + "\nHappy gaming!",
                    Content,
                    null,
                    "Okay (≧▽≦)ﾉ",
                    null
                );

        public static async Task<ContentDialogResult> Dialog_RepairCompleted(UIElement Content, int Count) =>
            await SpawnDialog(
                    $"Repair Process is Completed!",
                    $"{(Count > 0 ? $"{Count} file(s) have been repaired." : "No files are broken.")}",
                    Content,
                    null,
                    "Okay",
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

        public static async Task<ContentDialogResult> Dialog_SteamConversionDownloadDialog(UIElement Content, string SizeString) =>
            await SpawnDialog(
                    "Integrity Checking is Done!",
                    string.Format(
                        $"Checking Game Data Integrity is done! The conversion process will download at least {SizeString} of file size\n"
                        + "You can continue or cancel it for later.\n"
                        + "\nDo you want to start the conversion process?"),
                    Content,
                    "Cancel",
                    "Yes",
                    null
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionFailedDialog(UIElement Content) =>
            await SpawnDialog(
                    "Conversion is Failed!",
                    string.Format(
                        "Sorry but, conversion process is failed! :(\n"
                        + "Please try to start-over the conversion process again."),
                    Content,
                    "Okay ;-;)",
                    null,
                    null
            );

        public static async Task<ContentDialogResult> Dialog_GameInstallationFileCorrupt(UIElement Content, string sourceHash, string downloadedHash) =>
            await SpawnDialog(
                    "Oops! Game Installation is Corrupted",
                    string.Format(
                        "Sorry but seems one of downloaded file has been corrupted."
                       + "\r\n\r\nServer Hash: {0}\r\nDownloaded Hash: {1}"
                       + "\r\n\r\nDo you want to redownload the file?", sourceHash, downloadedHash),
                    Content,
                    "No, Cancel",
                    "Yes, Redownload",
                    null
            );

        public static async Task<ContentDialogResult> Dialog_LocateFirstSetupFolder(UIElement Content, string defaultAppFolder) =>
            await SpawnDialog(
                    "Locate Folder",
                    string.Format(
                        "I'm recommending you to use default AppData in this folder"
                       + "\r\n\r\nLocation: {0}"
                       + "\r\n\r\nDo you want to use this location?", defaultAppFolder),
                    Content,
                    "Cancel",
                    "Yes, please",
                    "No, choose folder"
            );

        public static async Task<ContentDialogResult> Dialog_ExistingDownload(UIElement Content, long partialLength, long contentLength) =>
            await SpawnDialog(
                    "Resume Download?",
                    string.Format("You have downloaded {0}/{1} of the game previously.\r\n\r\nDo you want to continue?",
                                  SummarizeSizeSimple(partialLength),
                                  SummarizeSizeSimple(contentLength)),
                    Content,
                    null,
                    "Yes, Resume",
                    "No, Start from Beginning"
                );

        public static async Task<ContentDialogResult> Dialog_InsufficientDriveSpace(UIElement Content, long DriveFreeSpace, long RequiredSpace, string DriveLetter) =>
            await SpawnDialog(
                    "Disk space is insufficient",
                    string.Format("You don't have enough free space to install this game on your {2} drive!\r\n\r\nFree Space: {0}\r\nRequired Space: {1}.\r\n\r\nPlease make sure you have enough disk space before installing.",
                                  SummarizeSizeSimple(DriveFreeSpace),
                                  SummarizeSizeSimple(RequiredSpace),
                                  DriveLetter),
                    Content,
                    null,
                    "Okay",
                    null
                );

        public static async Task<ContentDialogResult> Dialog_RelocateFolder(UIElement Content) =>
            await SpawnDialog(
                    "Relocate App Data Folder",
                    string.Format("You are currently using this folder as your App Data Folder:\r\n\r\n {0}\r\n\r\nDo you want to relocate?",
                                  GetAppConfigValue("GameFolder").ToString()),
                    Content,
                    null,
                    "Yes, Relocate",
                    "No, Cancel"
                );

        public static async Task<ContentDialogResult> Dialog_RedownloadBrokenFilesGenshin(UIElement Content, long fileSize, int fileCount) =>
            await SpawnDialog(
                    "Broken Files were Found on Post-Install!",
                    string.Format("{1} file(s) are broken after post-installation and this may caused by broken files from previous installation."
                                + "\r\nAt least {0} of data need to be redownloaded. You can ignore this but it will probably break your game.\r\n\r\nDo you want to continue?",
                                  SummarizeSizeSimple(fileSize), fileCount),
                    Content,
                    "No, Ignore it",
                    "Yes, Redownload",
                    null
                );

        public static async Task<ContentDialogResult> Dialog_UninstallGame(UIElement Content, string gameLocation, string region) =>
            await SpawnDialog(
                    $"Uninstalling Game: {region}",
                    string.Format("You are about to uninstall the game in this location:\r\n\r\n{0}\r\n\r\nDo you want to continue?",
                                  gameLocation),
                    Content,
                    null,
                    "Uninstall",
                    "Cancel"
                );

        public static async Task<ContentDialogResult> SpawnDialog(
            string title, object content, UIElement Content,
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
