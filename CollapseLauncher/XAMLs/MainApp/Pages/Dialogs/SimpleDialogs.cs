using CollapseLauncher.CustomControls;
using Hi3Helper;
using Hi3Helper.Preset;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Preset.ConfigV2Store;
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
                        Lang._Misc.No,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                   );

        public static async Task<ContentDialogResult> Dialog_PreDownloadPackageVerified(UIElement Content) =>
               await SpawnDialog(
                        Lang._Dialogs.PreloadVerifiedTitle,
                        Lang._Dialogs.PreloadVerifiedSubtitle,
                        Content,
                        Lang._Misc.Close,
                        null,
                        null,
                        ContentDialogButton.Secondary,
                        ContentDialogTheme.Success
                   );

        public static async Task<ContentDialogResult> Dialog_PreviousDeltaPatchInstallFailed(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.DeltaPatchPrevFailedTitle,
                        Lang._Dialogs.DeltaPatchPrevFailedSubtitle,
                        Content,
                        null,
                        Lang._Misc.Yes,
                        Lang._Misc.No,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Error
                );

        public static async Task<ContentDialogResult> Dialog_PreviousGameConversionFailed(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.GameConversionPrevFailedTitle,
                        Lang._Dialogs.GameConversionPrevFailedSubtitle,
                        Content,
                        null,
                        Lang._Misc.Yes,
                        Lang._Misc.No,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Error
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

        public static async Task<ContentDialogResult> Dialog_OpenExecutable(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.LocateExePathTitle,
                        Lang._Dialogs.LocateExePathSubtitle,
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.LocateExecutable,
                        Lang._Misc.OpenDownloadPage
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
            await SpawnDialog(Lang._Dialogs.ChooseAudioLangTitle, Panel, Content, null, Lang._Misc.Next, null, ContentDialogButton.Primary, ContentDialogTheme.Informational);

            index = LangBox.SelectedIndex;

            return index;
        }

        public static async Task<ContentDialogResult> Dialog_GraphicsVeryHighWarning(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.ExtremeGraphicsSettingsWarnTitle,
                        Lang._Dialogs.ExtremeGraphicsSettingsWarnSubtitle,
                        Content,
                        null,
                        Lang._Misc.YesIHaveBeefyPC,
                        Lang._Misc.No,
                        ContentDialogButton.Secondary,
                        ContentDialogTheme.Warning
                );

        public static async Task<(ContentDialogResult, ComboBox, ComboBox)> Dialog_SelectGameConvertRecipe(UIElement Content)
        {
            Dictionary<string, PresetConfigV2> ConvertibleRegions = new Dictionary<string, PresetConfigV2>();
            foreach (KeyValuePair<string, PresetConfigV2> Config in ConfigV2.MetadataV2[CurrentConfigV2GameCategory].Where(x => x.Value.IsConvertible ?? false))
                ConvertibleRegions.Add(Config.Key, Config.Value);

            ContentDialogCollapse Dialog = new ContentDialogCollapse();
            ComboBox SourceGame = new ComboBox();
            ComboBox TargetGame = new ComboBox();

            SelectionChangedEventHandler SourceGameChangedArgs = new SelectionChangedEventHandler((object sender, SelectionChangedEventArgs e) =>
            {
                TargetGame.IsEnabled = true;
                Dialog.IsSecondaryButtonEnabled = false;
                TargetGame.ItemsSource = InnerLauncherConfig.BuildGameRegionListUI(CurrentConfigV2GameCategory, InstallationConvert.GetConvertibleNameList(
                    InnerLauncherConfig.GetComboBoxGameRegionValue((sender as ComboBox).SelectedItem)));
            });
            SelectionChangedEventHandler TargetGameChangedArgs = new SelectionChangedEventHandler((object sender, SelectionChangedEventArgs e) =>
            {
                if ((sender as ComboBox).SelectedIndex != -1)
                    Dialog.IsSecondaryButtonEnabled = true;
            });
            SourceGame = new ComboBox
            {
                Width = 200,
                ItemsSource = InnerLauncherConfig.BuildGameRegionListUI(CurrentConfigV2GameCategory, new List<string>(ConvertibleRegions.Keys)),
                PlaceholderText = Lang._InstallConvert.SelectDialogSource,
                CornerRadius = new CornerRadius(14)
            };
            SourceGame.SelectionChanged += SourceGameChangedArgs;
            TargetGame = new ComboBox
            {
                Width = 200,
                PlaceholderText = Lang._InstallConvert.SelectDialogTarget,
                IsEnabled = false,
                CornerRadius = new CornerRadius(14)
            };
            TargetGame.SelectionChanged += TargetGameChangedArgs;

            StackPanel DialogContainer = new StackPanel() { Orientation = Orientation.Vertical };
            StackPanel ComboBoxContainer = new StackPanel() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            ComboBoxContainer.Children.Add(SourceGame);
            ComboBoxContainer.Children.Add(new FontIcon() { Glyph = "", FontFamily = Application.Current.Resources["FontAwesomeSolid"] as FontFamily, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 16, 0), Opacity = 0.5f });
            ComboBoxContainer.Children.Add(TargetGame);
            DialogContainer.Children.Add(new TextBlock
            {
                Text = Lang._InstallConvert.SelectDialogSubtitle,
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            });
            DialogContainer.Children.Add(ComboBoxContainer);

            Dialog = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = Lang._InstallConvert.SelectDialogTitle,
                Content = DialogContainer,
                CloseButtonText = null,
                PrimaryButtonText = Lang._Misc.Cancel,
                SecondaryButtonText = Lang._Misc.Next,
                IsSecondaryButtonEnabled = false,
                DefaultButton = ContentDialogButton.Secondary,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                Style = (Style)Application.Current.Resources["CollapseContentDialogStyle"],
                XamlRoot = Content.XamlRoot
            };
            return (await Dialog.ShowAsync(), SourceGame, TargetGame);
        }

        public static async Task<ContentDialogResult> Dialog_LocateDownloadedConvertRecipe(UIElement Content, string FileName)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.CookbookLocateSubtitle1 });
            texts.Inlines.Add(new Hyperlink()
            {
                Inlines = { new Run { Text = Lang._Dialogs.CookbookLocateSubtitle2, FontWeight = FontWeights.Bold, Foreground = (SolidColorBrush)Application.Current.Resources["AccentColor"] } },
                NavigateUri = new Uri("https://www.mediafire.com/folder/gb09r9fw0ndxb/Hi3ConversionRecipe"),
            });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.CookbookLocateSubtitle3 });
            texts.Inlines.Add(new Run { Text = $" {Lang._Misc.Next} ", FontWeight = FontWeights.Bold });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.CookbookLocateSubtitle5 });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.CookbookLocateSubtitle6, FontWeight = FontWeights.Bold });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.CookbookLocateSubtitle7 });
            texts.Inlines.Add(new Run { Text = FileName, FontWeight = FontWeights.Bold });
            return await SpawnDialog(
                        Lang._Dialogs.CookbookLocateTitle,
                        texts,
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.Next,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
                );
        }

        public static async Task<ContentDialogResult> Dialog_ChangeReleaseChannel(string ChannelName, UIElement Content)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ReleaseChannelChangeSubtitle1 });
            texts.Inlines.Add(new Run { Text = $" {ChannelName}", FontWeight = FontWeights.Bold });
            texts.Inlines.Add(new Run { Text = "\r\n\r\n" });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ReleaseChannelChangeSubtitle2 + "\r\n", FontSize = 18, FontWeight = FontWeights.Bold });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ReleaseChannelChangeSubtitle3 });
            return await SpawnDialog(
                        Lang._Dialogs.ReleaseChannelChangeTitle,
                        texts,
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.OkayHappy,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                );
        }

        public static async Task<ContentDialogResult> Dialog_ExistingInstallation(UIElement Content, string actualLocation) =>
            await SpawnDialog(
                        Lang._Dialogs.ExistingInstallTitle,
                        string.Format(Lang._Dialogs.ExistingInstallSubtitle, actualLocation),
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.YesMigrateIt,
                        Lang._Misc.NoKeepInstallIt,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
            );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationBetterLauncher(UIElement Content, string gamePath) =>
            await SpawnDialog(
                        Lang._Dialogs.ExistingInstallBHI3LTitle,
                        string.Format(Lang._Dialogs.ExistingInstallBHI3LSubtitle, gamePath),
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.YesMigrateIt,
                        Lang._Misc.NoKeepInstallIt,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
            );

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationSteam(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.ExistingInstallSteamTitle,
                        string.Format(Lang._Dialogs.ExistingInstallSteamSubtitle, GamePathOnSteam),
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.YesMigrateIt,
                        Lang._Misc.NoKeepInstallIt,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
            );


        public static async Task<ContentDialogResult> Dialog_MigrationChoiceDialog(UIElement Content, string existingGamePath, string gameTitle, string gameRegion, string launcherName)
        {
            string gameFullnameString = $"{InnerLauncherConfig.GetGameTitleRegionTranslationString(gameTitle, Lang._GameClientTitles)} - {InnerLauncherConfig.GetGameTitleRegionTranslationString(gameRegion, Lang._GameClientRegions)}";

            TextBlock contentTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            contentTextBlock.AddTextBlockLine(string.Format(Lang._Dialogs.MigrateExistingInstallChoiceSubtitle1, launcherName));
            contentTextBlock.AddTextBlockNewLine(2);
            contentTextBlock.AddTextBlockLine(existingGamePath, FontWeights.SemiBold);
            contentTextBlock.AddTextBlockNewLine(2);
            contentTextBlock.AddTextBlockLine(string.Format(Lang._Dialogs.MigrateExistingInstallChoiceSubtitle2, launcherName));

            return await SpawnDialog(
                string.Format(Lang._Dialogs.MigrateExistingInstallChoiceTitle, gameFullnameString),
                contentTextBlock,
                Content,
                Lang._Misc.Cancel,
                Lang._Misc.UseCurrentDir,
                Lang._Misc.MoveToDifferentDir,
                ContentDialogButton.Primary,
                ContentDialogTheme.Informational
            );
        }
        public static async Task<ContentDialogResult> Dialog_SteamConversionNoPermission(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.SteamConvertNeedMigrateTitle,
                        Lang._Dialogs.SteamConvertNeedMigrateSubtitle,
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.Yes,
                        Lang._Misc.NoOtherLocation,
                        ContentDialogButton.Secondary,
                        ContentDialogTheme.Error
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionDownloadDialog(UIElement Content, string SizeString) =>
            await SpawnDialog(
                        Lang._Dialogs.SteamConvertIntegrityDoneTitle,
                        Lang._Dialogs.SteamConvertIntegrityDoneSubtitle,
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.Yes,
                        null,
                        ContentDialogButton.Secondary,
                        ContentDialogTheme.Success
            );

        public static async Task<ContentDialogResult> Dialog_SteamConversionFailedDialog(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.SteamConvertFailedTitle,
                        Lang._Dialogs.SteamConvertFailedSubtitle,
                        Content,
                        Lang._Misc.OkaySad,
                        null,
                        null,
                        ContentDialogButton.Close,
                        ContentDialogTheme.Success
            );

        public static async Task<ContentDialogResult> Dialog_GameInstallationFileCorrupt(UIElement Content, string sourceHash, string downloadedHash) =>
            await SpawnDialog(
                        Lang._Dialogs.InstallDataCorruptTitle,
                        string.Format(Lang._Dialogs.InstallDataCorruptSubtitle, sourceHash, downloadedHash),
                        Content,
                        Lang._Misc.NoCancel,
                        Lang._Misc.YesRedownload,
                        Lang._Misc.ExtractAnyway,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Error
            );

        public static async Task<ContentDialogResult> Dialog_GameInstallCorruptedDataAnyway(UIElement Content, string fileName, long fileSize)
        {
            TextBlock textBlock = new TextBlock()
            {
                TextWrapping = TextWrapping.Wrap
            };
            textBlock.Inlines.Add(new Run { Text = Lang._Dialogs.InstallCorruptDataAnywaySubtitle1 });
            textBlock.Inlines.Add(new Run
            {
                Text = string.Format(Lang._Dialogs.InstallCorruptDataAnywaySubtitle2, fileName, SummarizeSizeSimple(fileSize), fileSize),
                FontWeight = FontWeights.SemiBold
            });
            textBlock.Inlines.Add(new Run { Text = Lang._Dialogs.InstallCorruptDataAnywaySubtitle3 });

            return await SpawnDialog(
                Lang._Dialogs.InstallCorruptDataAnywayTitle,
                textBlock,
                Content,
                Lang._Misc.NoCancel,
                Lang._Misc.YesImReallySure,
                null,
                ContentDialogButton.Primary,
                ContentDialogTheme.Warning
            );
        }

        public static async Task<ContentDialogResult> Dialog_LocateFirstSetupFolder(UIElement Content, string defaultAppFolder) =>
            await SpawnDialog(
                        Lang._StartupPage.ChooseFolderDialogTitle,
                        string.Format(Lang._StartupPage.ChooseFolderDialogSubtitle, defaultAppFolder),
                        Content,
                        Lang._StartupPage.ChooseFolderDialogCancel,
                        Lang._StartupPage.ChooseFolderDialogPrimary,
                        Lang._StartupPage.ChooseFolderDialogSecondary,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
            );

        public static async Task<ContentDialogResult> Dialog_CannotUseAppLocationForGameDir(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.CannotUseAppLocationForGameDirTitle,
                        Lang._Dialogs.CannotUseAppLocationForGameDirSubtitle,
                        Content,
                        Lang._Misc.Okay,
                        null,
                        null,
                        ContentDialogButton.Close,
                        ContentDialogTheme.Error
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
                        Lang._Misc.NoStartFromBeginning,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
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
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Error
                );

        public static async Task<ContentDialogResult> Dialog_WarningOperationNotCancellable(UIElement Content)
        {
            TextBlock warningMessage = new TextBlock { TextWrapping = TextWrapping.Wrap };
            warningMessage.AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg1);
            warningMessage.AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg2, FontWeights.Bold);
            warningMessage.AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg3);
            warningMessage.AddTextBlockLine(Lang._Misc.Yes, FontWeights.SemiBold);
            warningMessage.AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg4);
            warningMessage.AddTextBlockLine(Lang._Misc.NoCancel, FontWeights.SemiBold);
            warningMessage.AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg5);

            return await SpawnDialog(
                        Lang._Dialogs.OperationWarningNotCancellableTitle,
                        warningMessage,
                        Content,
                        Lang._Misc.NoCancel,
                        Lang._Misc.Yes,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                );
        }

        public static async Task<ContentDialogResult> Dialog_RelocateFolder(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.RelocateFolderTitle,
                        string.Format(Lang._Dialogs.RelocateFolderSubtitle,
                                      GetAppConfigValue("GameFolder").ToString()),
                        Content,
                        null,
                        Lang._Misc.YesRelocate,
                        Lang._Misc.Cancel,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
                );

        public static async Task<ContentDialogResult> Dialog_UninstallGame(UIElement Content, string gameLocation, string region) =>
            await SpawnDialog(
                        string.Format(Lang._Dialogs.UninstallGameTitle, region),
                        string.Format(Lang._Dialogs.UninstallGameSubtitle,
                                      gameLocation),
                        Content,
                        null,
                        Lang._Misc.Uninstall,
                        Lang._Misc.Cancel,
                        ContentDialogButton.Secondary,
                        ContentDialogTheme.Error
                );

        public static async Task<ContentDialogResult> Dialog_ClearMetadata(UIElement Content) =>
            await SpawnDialog(
                    string.Format(Lang._SettingsPage.AppFiles_ClearMetadataDialog),
                    string.Format(Lang._SettingsPage.AppFiles_ClearMetadataDialogHelp),
                    Content,
                    null,
                    Lang._Misc.Yes,
                    Lang._Misc.Cancel,
                    ContentDialogButton.Secondary,
                    ContentDialogTheme.Warning
                );

        public static async Task<ContentDialogResult> Dialog_NeedInstallMediaPackage(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.NeedInstallMediaPackTitle,
                        Lang._Dialogs.NeedInstallMediaPackSubtitle1 + Lang._Dialogs.NeedInstallMediaPackSubtitle2,
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.Install,
                        Lang._Misc.Skip,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                );

        public static async Task<ContentDialogResult> Dialog_InstallMediaPackageFinished(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.InstallMediaPackCompleteTitle,
                        Lang._Dialogs.InstallMediaPackCompleteSubtitle,
                        Content,
                        null,
                        Lang._Misc.OkayBackToMenu,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Success
                );

        public static async Task<ContentDialogResult> Dialog_ChangePlaytime(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.ChangePlaytimeTitle,
                        Lang._Dialogs.ChangePlaytimeSubtitle,
                        Content,
                        Lang._Misc.NoCancel,
                        Lang._Misc.Yes,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
            );

        public static async Task<ContentDialogResult> Dialog_StopGame(UIElement Content) =>
            await SpawnDialog(
                        Lang._Dialogs.StopGameTitle,
                        Lang._Dialogs.StopGameSubtitle,
                        Content,
                        Lang._Misc.NoCancel,
                        Lang._Misc.Yes,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                );

        public static async Task<ContentDialogResult> Dialog_ResetPlaytime(UIElement Content)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ResetPlaytimeSubtitle });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ResetPlaytimeSubtitle2, FontWeight = FontWeights.Bold });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ResetPlaytimeSubtitle3 });

            return await SpawnDialog(
                        Lang._Dialogs.ResetPlaytimeTitle,
                        texts,
                        Content,
                        Lang._Misc.NoCancel,
                        Lang._Misc.Yes,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
            );
        }

        public static async Task<ContentDialogResult> Dialog_MeteredConnectionWarning(UIElement Content)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.MeteredConnectionWarningSubtitle });

            return await SpawnDialog(
                        Lang._Dialogs.MeteredConnectionWarningTitle,
                        texts,
                        Content,
                        Lang._Misc.NoCancel,
                        Lang._Misc.Yes,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                );   
        }

        public static async Task<ContentDialogResult> Dialog_ResetKeyboardShortcuts(UIElement Content)
        {
            return await SpawnDialog(
                Lang._Dialogs.ResetKbShortcutsTitle,
                Lang._Dialogs.ResetKbShortcutsSubtitle,
                Content,
                Lang._Misc.NoCancel,
                Lang._Misc.Yes,
                null,
                ContentDialogButton.Primary,
                ContentDialogTheme.Warning
                );
        }

        public static async Task<ContentDialogResult> Dialog_ShowUnhandledExceptionMenu(UIElement Content)
        {
            void CopyTextToClipboard(object sender, RoutedEventArgs e)
            {
                InvokeProp.CopyStringToClipboard(ErrorSender.ExceptionContent);

                Button btn = sender as Button;
                FontIcon fontIcon = (btn.Content as StackPanel).Children[0] as FontIcon;
                TextBlock textBlock = (btn.Content as StackPanel).Children[1] as TextBlock;
                fontIcon.Glyph = "";
                textBlock.Text = Lang._UnhandledExceptionPage.CopyClipboardBtn2;
                btn.IsEnabled = false;
            }

            Button copyButton = null;

            try
            {
                string exceptionContent = ErrorSender.ExceptionContent;
                string title = ErrorSender.ExceptionTitle;
                string subtitle = ErrorSender.ExceptionSubtitle;

                bool isShowBackButton = (ErrorSender.ExceptionType == ErrorType.Connection) && (InnerLauncherConfig.m_window as MainWindow).rootFrame.CanGoBack;

                Grid rootGrid = new Grid()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    RowDefinitions =
                    {
                        new RowDefinition() { Height = GridLength.Auto },
                        new RowDefinition(),
                        new RowDefinition() { Height = GridLength.Auto }
                    }
                };

                TextBlock subtitleText = new TextBlock
                {
                    Text = subtitle,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.Medium
                };
                TextBox exceptionText = new TextBox
                {
                    IsReadOnly = true,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 300,
                    AcceptsReturn = true,
                    Text = exceptionContent,
                    Margin = new Thickness(0, 8, 0, 8)
                };

                StackPanel copyButtonTextPanel = new StackPanel() { Orientation = Orientation.Horizontal };
                copyButtonTextPanel.Children.Add(new FontIcon { FontFamily = (FontFamily)Application.Current.Resources["FontAwesomeSolid"], Glyph = "", Margin = new Thickness(0, 0, 8, 0), FontSize = 16 });
                copyButtonTextPanel.Children.Add(new TextBlock() { Text = Lang._UnhandledExceptionPage.CopyClipboardBtn1, FontWeight = FontWeights.Medium });
                copyButton = new Button
                {
                    Style = Application.Current.Resources["AccentButtonStyle"] as Style,
                    Content = copyButtonTextPanel,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    CornerRadius = new CornerRadius(15)
                };
                copyButton.Click += CopyTextToClipboard;

                rootGrid.Children.Add(subtitleText);
                rootGrid.Children.Add(exceptionText);
                rootGrid.Children.Add(copyButton);

                Grid.SetRow(subtitleText, 0);
                Grid.SetRow(exceptionText, 1);
                Grid.SetRow(copyButton, 2);

                ContentDialogResult result = await SpawnDialog(
                    title, rootGrid, Content,
                    Lang._UnhandledExceptionPage.GoBackPageBtn1,
                    null,
                    null,
                    ContentDialogButton.Close,
                    ContentDialogTheme.Error);

                return result;
            }
            catch { throw; }
            finally
            {
                if (copyButton != null)
                    copyButton.Click -= CopyTextToClipboard;
            }
        }

        #region Shortcut Creator Dialogs
        public static async Task<Tuple<ContentDialogResult, bool>> Dialog_ShortcutCreationConfirm(UIElement Content, string path)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical, MaxWidth = 500 };
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmSubtitle1, Margin = new Thickness(0, 2, 0, 4), HorizontalAlignment = HorizontalAlignment.Center });
            TextBlock pathText = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
            pathText.Inlines.Add(new Run() { Text = path, FontWeight = FontWeights.Bold });
            panel.Children.Add(pathText);
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmSubtitle2, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4), HorizontalAlignment = HorizontalAlignment.Center });

            CheckBox playOnLoad = new CheckBox() { 
                Content = new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmCheckBox, TextWrapping = TextWrapping.Wrap },
                Margin = new Thickness(0, 4, 0, -8),
                HorizontalAlignment = HorizontalAlignment.Center
            };    
            panel.Children.Add(playOnLoad);
        
            ContentDialogResult result = await SpawnDialog(
                Lang._Dialogs.ShortcutCreationConfirmTitle,
                panel,
                Content,
                Lang._Misc.Cancel,
                Lang._Misc.YesContinue,
                dialogTheme: ContentDialogTheme.Warning
                );

            return new Tuple<ContentDialogResult, bool>(result, playOnLoad.IsChecked ?? false);
        }

        public static async Task<ContentDialogResult> Dialog_ShortcutCreationSuccess(UIElement Content, string path, bool play = false)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical, MaxWidth = 500 };
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle1, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) });
            TextBlock pathText = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 4) };
            pathText.Inlines.Add(new Run() { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle2 });
            pathText.Inlines.Add(new Run() { Text = path, FontWeight = FontWeights.Bold });
            panel.Children.Add(pathText);

            if (play)
            {
                panel.Children.Add(new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle3, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) });
                panel.Children.Add(new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle4, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
            }

            return await SpawnDialog(
                Lang._Dialogs.ShortcutCreationSuccessTitle,
                panel,
                Content,
                Lang._Misc.Close,
                dialogTheme: ContentDialogTheme.Success
                );
        }

        public static async Task<Tuple<ContentDialogResult, bool>> Dialog_SteamShortcutCreationConfirm(UIElement Content)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical, MaxWidth = 500 };

            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationConfirmSubtitle1, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 2) });
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationConfirmSubtitle2, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });

            CheckBox playOnLoad = new CheckBox()
            {
                Content = new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationConfirmCheckBox, TextWrapping = TextWrapping.Wrap },
                Margin = new Thickness(0, 4, 0, -8),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(playOnLoad);

            ContentDialogResult result = await SpawnDialog(
                Lang._Dialogs.SteamShortcutCreationConfirmTitle,
                panel,
                Content,
                Lang._Misc.Cancel,
                Lang._Misc.YesContinue,
                dialogTheme: ContentDialogTheme.Warning
                );

            return new Tuple<ContentDialogResult, bool>(result, playOnLoad.IsChecked ?? false);
        }

        public static async Task<ContentDialogResult> Dialog_SteamShortcutCreationSuccess(UIElement Content, bool play = false)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical, MaxWidth = 500 };
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle1, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle2, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) });
            if (play)
            {
                panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle3, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
            }
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle4, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2) });
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });

            return await SpawnDialog(
                Lang._Dialogs.SteamShortcutCreationSuccessTitle,
                panel,
                Content,
                Lang._Misc.Close,
                dialogTheme: ContentDialogTheme.Success
                );
        }

        public static async Task<ContentDialogResult> Dialog_SteamShortcutCreationFailure(UIElement Content)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Vertical, MaxWidth = 350 };
            panel.Children.Add(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationFailureSubtitle, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) });
            return await SpawnDialog(
                Lang._Dialogs.SteamShortcutCreationFailureTitle,
                panel,
                Content,
                Lang._Misc.Close,
                dialogTheme: ContentDialogTheme.Error
                );
        }
        #endregion

        public static async Task<ContentDialogResult> SpawnDialog(
            string title, object content, UIElement Content,
            string closeText = null, string primaryText = null,
            string secondaryText = null, ContentDialogButton defaultButton = ContentDialogButton.Primary,
            ContentDialogTheme dialogTheme = ContentDialogTheme.Informational)
        {
            (InnerLauncherConfig.m_window as MainWindow).ContentDialog = new ContentDialogCollapse(dialogTheme)
            {
                Title = title,
                Content = content,
                CloseButtonText = closeText,
                PrimaryButtonText = primaryText,
                SecondaryButtonText = secondaryText,
                DefaultButton = defaultButton,
                Background = (Brush)Application.Current.Resources["DialogAcrylicBrush"],
                Style = (Style)Application.Current.Resources["CollapseContentDialogStyle"],
                XamlRoot = Content.XamlRoot
            };
            return await (InnerLauncherConfig.m_window as MainWindow).ContentDialog.ShowAsync();
        }
    }
}
