using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.Dialogs
{
    public static class SimpleDialogs
    {
        public static async Task<ContentDialogResult> Dialog_DeltaPatchFileDetected(UIElement Content, string sourceVer, string targetVer) =>
               await SpawnDialog(
                       Lang._Dialogs.DeltaPatchDetectedTitle,
                       string.Format(Lang._Dialogs.DeltaPatchDetectedSubtitle, sourceVer, targetVer),
                       Content,
                       Lang._Misc.Cancel,
                       Lang._Misc.Yes,
                       null,
                       ContentDialogButton.Primary
                   );

        public static async Task<ContentDialogResult> Dialog_PreDownloadPackageVerified(UIElement Content) =>
               await SpawnDialog(
                       Lang._Dialogs.PreloadVerifiedTitle,
                       Lang._Dialogs.PreloadVerifiedSubtitle,
                       Content,
                       Lang._Misc.Close,
                       null,
                       null,
                       ContentDialogButton.Secondary
                   );

        public static async Task<ContentDialogResult> Dialog_InstallationLocation(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.LocateInstallTitle,
                    Lang._Dialogs.LocateInstallSubtitle,
                    Content,
                    Lang._Misc.Cancel,
                    Lang._Misc.UseDefaultDir,
                    Lang._Misc.LocateDir
                );

        public static async Task<ContentDialogResult> Dialog_InsufficientWritePermission(UIElement Content, string path) =>
            await SpawnDialog(
                    Lang._Dialogs.UnauthorizedDirTitle,
                    string.Format(Lang._Dialogs.UnauthorizedDirSubtitle, path),
                    Content,
                    Lang._Misc.Okay,
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
                PlaceholderText = Lang._Dialogs.ChooseAudioLangSelectPlaceholder,
                Width = 256,
                ItemsSource = langlist,
                SelectedIndex = index,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            Panel.Children.Add(new TextBlock()
            {
                Text = Lang._Dialogs.ChooseAudioLangSubtitle,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            });
            Panel.Children.Add(LangBox);
            await SpawnDialog(Lang._Dialogs.ChooseAudioLangTitle, Panel, Content, null, Lang._Misc.Next, null);

            index = LangBox.SelectedIndex;

            return index;
        }

        public static async Task<ContentDialogResult> Dialog_AdditionalDownloadNeeded(UIElement Content, long fileSize) =>
            await SpawnDialog(
                    Lang._Dialogs.AddtDownloadNeededTitle,
                    string.Format(Lang._Dialogs.AddtDownloadNeededSubtitle, SummarizeSizeSimple(fileSize)),
                    Content,
                    Lang._Misc.Skip,
                    Lang._Misc.Yes,
                    null
                );

        public static async Task<ContentDialogResult> Dialog_AdditionalDownloadCompleted(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.AddtDownloadCompletedTitle,
                    Lang._Dialogs.AddtDownloadCompletedSubtitle,
                    Content,
                    null,
                    Lang._Misc.OkayHappy,
                    null
                );

        public static async Task<ContentDialogResult> Dialog_RepairCompleted(UIElement Content, int Count) =>
            await SpawnDialog(
                    Lang._Dialogs.RepairCompletedTitle,
                    $"{(Count > 0 ? string.Format(Lang._Dialogs.RepairCompletedSubtitle, Count) : Lang._Dialogs.RepairCompletedSubtitleNoBroken)}",
                    Content,
                    null,
                    Lang._Misc.Okay,
                    null
                );

        public static async Task<ContentDialogResult> Dialog_GraphicsVeryHighWarning(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.ExtremeGraphicsSettingsWarnTitle,
                    Lang._Dialogs.ExtremeGraphicsSettingsWarnSubtitle,
                    Content,
                    null,
                    Lang._Misc.YesIHaveBeefyPC,
                    Lang._Misc.No,
                    ContentDialogButton.Secondary
                );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallation(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.ExistingInstallTitle,
                    string.Format(Lang._Dialogs.ExistingInstallSubtitle, CurrentRegion.ActualGameDataLocation),
                    Content,
                    Lang._Misc.Cancel,
                    Lang._Misc.YesMigrateIt,
                    Lang._Misc.NoKeepInstallIt
            );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationBetterLauncher(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.ExistingInstallBHI3LTitle,
                    string.Format(Lang._Dialogs.ExistingInstallBHI3LSubtitle, CurrentRegion.BetterHi3LauncherConfig.game_info.install_path),
                    Content,
                    Lang._Misc.Cancel,
                    Lang._Misc.YesMigrateIt,
                    Lang._Misc.NoKeepInstallIt
            );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationSteam(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.ExistingInstallSteamTitle,
                    string.Format(Lang._Dialogs.ExistingInstallSteamSubtitle, GamePathOnSteam),
                    Content,
                    Lang._Misc.Cancel,
                    Lang._Misc.YesMigrateIt,
                    Lang._Misc.NoKeepInstallIt
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionNoPermission(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.SteamConvertNeedMigrateTitle,
                    Lang._Dialogs.SteamConvertNeedMigrateSubtitle,
                    Content,
                    Lang._Misc.Cancel,
                    Lang._Misc.Yes,
                    Lang._Misc.NoOtherLocation
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionDownloadDialog(UIElement Content, string SizeString) =>
            await SpawnDialog(
                    Lang._Dialogs.SteamConvertIntegrityDoneTitle,
                    Lang._Dialogs.SteamConvertIntegrityDoneSubtitle,
                    Content,
                    Lang._Misc.Cancel,
                    Lang._Misc.Yes,
                    null
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionFailedDialog(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.SteamConvertFailedTitle,
                    Lang._Dialogs.SteamConvertFailedSubtitle,
                    Content,
                    Lang._Misc.OkaySad,
                    null,
                    null
            );

        public static async Task<ContentDialogResult> Dialog_GameInstallationFileCorrupt(UIElement Content, string sourceHash, string downloadedHash) =>
            await SpawnDialog(
                    Lang._Dialogs.InstallDataCorruptTitle,
                    string.Format(Lang._Dialogs.InstallDataCorruptSubtitle, sourceHash, downloadedHash),
                    Content,
                    Lang._Misc.NoCancel,
                    Lang._Misc.YesRedownload,
                    null
            );

        public static async Task<ContentDialogResult> Dialog_LocateFirstSetupFolder(UIElement Content, string defaultAppFolder) =>
            await SpawnDialog(
                    Lang._StartupPage.ChooseFolderDialogTitle,
                    string.Format(Lang._StartupPage.ChooseFolderDialogSubtitle, defaultAppFolder),
                    Content,
                    Lang._StartupPage.ChooseFolderDialogCancel,
                    Lang._StartupPage.ChooseFolderDialogPrimary,
                    Lang._StartupPage.ChooseFolderDialogSecondary
            );

        public static async Task<ContentDialogResult> Dialog_ExistingDownload(UIElement Content, long partialLength, long contentLength) =>
            await SpawnDialog(
                    Lang._Dialogs.InstallDataDownloadResumeTitle,
                    string.Format(Lang._Dialogs.InstallDataDownloadResumeSubtitle,
                                  SummarizeSizeSimple(partialLength),
                                  SummarizeSizeSimple(contentLength)),
                    Content,
                    null,
                    Lang._Misc.YesResume,
                    Lang._Misc.NoStartFromBeginning
                );

        public static async Task<ContentDialogResult> Dialog_InsufficientDriveSpace(UIElement Content, long DriveFreeSpace, long RequiredSpace, string DriveLetter) =>
            await SpawnDialog(
                    Lang._Dialogs.InsufficientDiskTitle,
                    string.Format(Lang._Dialogs.InsufficientDiskSubtitle,
                                  SummarizeSizeSimple(DriveFreeSpace),
                                  SummarizeSizeSimple(RequiredSpace),
                                  DriveLetter),
                    Content,
                    null,
                    Lang._Misc.Okay,
                    null
                );

        public static async Task<ContentDialogResult> Dialog_RelocateFolder(UIElement Content) =>
            await SpawnDialog(
                    Lang._Dialogs.RelocateFolderTitle,
                    string.Format(Lang._Dialogs.RelocateFolderSubtitle,
                                  GetAppConfigValue("GameFolder").ToString()),
                    Content,
                    null,
                    Lang._Misc.YesRelocate,
                    Lang._Misc.Cancel
                );

        public static async Task<ContentDialogResult> Dialog_UninstallGame(UIElement Content, string gameLocation, string region) =>
            await SpawnDialog(
                    string.Format(Lang._Dialogs.UninstallGameTitle, region),
                    string.Format(Lang._Dialogs.UninstallGameSubtitle,
                                  gameLocation),
                    Content,
                    null,
                    Lang._Misc.Uninstall,
                    Lang._Misc.Cancel
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
