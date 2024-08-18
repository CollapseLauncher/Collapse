using CollapseLauncher.CustomControls;
using CollapseLauncher.Extension;
using CollapseLauncher.FileDialogCOM;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Statics;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
using CollapseUIExt = CollapseLauncher.Extension.UIElementExtensions;

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

        public static async Task<(Dictionary<string, string>, string)> Dialog_ChooseAudioLanguageChoice(UIElement Content, Dictionary<string, string> langDict, string defaultLocaleCode = "ja-jp")
        {
            bool[] choices = new bool[langDict.Count];
            if (!langDict.ContainsKey(defaultLocaleCode))
                throw new KeyNotFoundException($"Default locale code: {defaultLocaleCode} is not found within langDict argument");

            List<string> localecodelist = langDict.Keys.ToList();
            List<string> langlist = langDict.Values.ToList();

            // Naive approach to lookup default index value
            string reflocalecode = localecodelist.FirstOrDefault(x => x.Equals(defaultLocaleCode, StringComparison.OrdinalIgnoreCase));
            int defaultIndex = localecodelist.IndexOf(reflocalecode);
            int choiceAsDefault = defaultIndex;
            StackPanel parentPanel = CollapseUIExt.CreateStackPanel();

            parentPanel.AddElementToStackPanel(new TextBlock()
            {
                Text = Lang._Dialogs.ChooseAudioLangSubtitle,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
            }.WithMargin(0d, 0d, 0d, 16d));
            parentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

            RadioButtons defaultChoiceRadioButton = new RadioButtons()
                .WithHorizontalAlignment(HorizontalAlignment.Center);
            defaultChoiceRadioButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            parentPanel.AddElementToStackPanel(defaultChoiceRadioButton);

            ContentDialogCollapse dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
            {
                Title = Lang._Dialogs.ChooseAudioLangTitle,
                Content = parentPanel,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = Lang._Misc.Next,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                Style = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                XamlRoot = (WindowUtility.CurrentWindow is MainWindow mainWindow) ? mainWindow.Content.XamlRoot : Content.XamlRoot
            };

            for (int i = 0; i < langlist.Count; i++)
            {
                Grid checkBoxGrid = CollapseUIExt.CreateGrid()
                    .WithColumns(new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Auto))
                    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                    .WithMargin(0, 0, 0, 8);

                CheckBox checkBox = new CheckBox() { Content = checkBoxGrid };
                checkBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                checkBox.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                checkBox.VerticalAlignment = VerticalAlignment.Center;
                checkBox.VerticalContentAlignment = VerticalAlignment.Center;

                TextBlock useAsDefaultText = new TextBlock
                {
                    Text = Lang._Misc.UseAsDefault,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Opacity = 0.25,
                    Name = "UseAsDefaultLabel"
                };
                useAsDefaultText.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                Grid iconTextGrid = CollapseUIExt.CreateIconTextGrid(
                    langlist[i],
                    "\uf1ab",
                    iconSize: 14,
                    textSize: 14,
                    iconFontFamily: "FontAwesomeSolid")
                    .WithOpacity(0.25);
                iconTextGrid.Name = "IconText";
                iconTextGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                iconTextGrid.VerticalAlignment = VerticalAlignment.Center;

                checkBoxGrid.AddElementToGridColumn(iconTextGrid, 0);
                checkBoxGrid.AddElementToGridColumn(useAsDefaultText, 1);

                RadioButton radioButton = new RadioButton
                {
                    Content = checkBox,
                    Style = CollapseUIExt.GetApplicationResource<Style>("AudioLanguageSelectionRadioButtonStyle"),
                    Background = CollapseUIExt.GetApplicationResource<Brush>("AudioLanguageSelectionRadioButtonBrush")
                }
                .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                .WithVerticalAlignment(VerticalAlignment.Center);

                defaultChoiceRadioButton.Items.Add(radioButton);

                radioButton.Tag = i;
                checkBox.Tag = i;

                radioButton.Checked += (sender, _) =>
                {
                    RadioButton radioButtonLocal = sender as RadioButton;
                    choiceAsDefault = (int)radioButtonLocal.Tag;
                    checkBox.IsChecked = true;

                    TextBlock textBlockLocal = (TextBlock)radioButtonLocal.FindDescendant("UseAsDefaultLabel");
                    if (textBlockLocal != null)
                        textBlockLocal.Opacity = 1;
                };

                radioButton.Unchecked += (sender, _) =>
                {
                    RadioButton radioButtonLocal = sender as RadioButton;
                    TextBlock textBlockLocal = (TextBlock)radioButtonLocal.FindDescendant("UseAsDefaultLabel");
                    if (textBlockLocal != null)
                        textBlockLocal.Opacity = 0.25;
                };

                if (i == defaultIndex)
                {
                    choices[i] = true;
                    checkBox.IsChecked = true;
                    defaultChoiceRadioButton.SelectedIndex = i;
                    iconTextGrid.Opacity = 1;
                }

                checkBox.Checked += (sender, _) =>
                {
                    CheckBox thisCheckBox = sender as CheckBox;
                    int thisIndex = (int)thisCheckBox.Tag;
                    choices[thisIndex] = true;
                    radioButton.IsEnabled = true;

                    bool isHasAnyChoices = choices.Any(x => x);
                    dialog.IsPrimaryButtonEnabled = isHasAnyChoices;
                    if (defaultChoiceRadioButton.SelectedIndex < 0)
                        defaultChoiceRadioButton.SelectedIndex = thisIndex;

                    Grid thisIconText = (Grid)thisCheckBox.FindDescendant("IconText");
                    if (thisIconText != null)
                        thisIconText.Opacity = 1;
                };
                checkBox.Unchecked += (sender, _) =>
                {
                    CheckBox thisCheckBox = sender as CheckBox;
                    int thisIndex = (int)thisCheckBox.Tag;
                    choices[thisIndex] = false;
                    radioButton.IsChecked = false;

                    Grid thisIconText = (Grid)thisCheckBox.FindDescendant("IconText");
                    if (thisIconText != null)
                        thisIconText.Opacity = 0.25;

                    bool isHasAnyChoices = choices.Any(x => x);
                    dialog.IsPrimaryButtonEnabled = isHasAnyChoices;

                    // TODO: Find a better way rather than this SPAGHEETTTTT CODE
                    if (defaultChoiceRadioButton.SelectedIndex < 0 && isHasAnyChoices)
                    {
                        for (int i = 0; i < choices.Length; i++)
                        {
                            if (choices[i])
                            {
                                defaultChoiceRadioButton.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                };
            }

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.None)
                return (null, null);

            Dictionary<string, string> returnDictionary = new Dictionary<string, string>();
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i])
                    returnDictionary.Add(localecodelist[i], langlist[i]);
            }

            return (returnDictionary, localecodelist[choiceAsDefault]);
        }

        public static async Task<(List<int>, int)> Dialog_ChooseAudioLanguageChoice(UIElement Content, List<string> langlist, int defaultIndex = 2)
        {
            bool[] choices = new bool[langlist.Count];
            int choiceAsDefault = defaultIndex;
            StackPanel parentPanel = CollapseUIExt.CreateStackPanel();

            parentPanel.AddElementToStackPanel(new TextBlock()
            {
                Text = Lang._Dialogs.ChooseAudioLangSubtitle,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeights.Medium,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
            }.WithMargin(0d, 0d, 0d, 16d));
            parentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

            RadioButtons defaultChoiceRadioButton = new RadioButtons()
                .WithHorizontalAlignment(HorizontalAlignment.Center);
            defaultChoiceRadioButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            parentPanel.AddElementToStackPanel(defaultChoiceRadioButton);

            ContentDialogCollapse dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
            {
                Title = Lang._Dialogs.ChooseAudioLangTitle,
                Content = parentPanel,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = Lang._Misc.Next,
                SecondaryButtonText = null,
                DefaultButton = ContentDialogButton.Primary,
                Style = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                XamlRoot = (WindowUtility.CurrentWindow is MainWindow mainWindow) ? mainWindow.Content.XamlRoot : Content.XamlRoot
            };

            for (int i = 0; i < langlist.Count; i++)
            {
                Grid checkBoxGrid = CollapseUIExt.CreateGrid()
                    .WithColumns(new GridLength(1, GridUnitType.Star), new GridLength(1, GridUnitType.Auto))
                    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                    .WithMargin(0, 0, 0, 8);

                CheckBox checkBox = new CheckBox() { Content = checkBoxGrid };
                checkBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                checkBox.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                checkBox.VerticalAlignment = VerticalAlignment.Center;
                checkBox.VerticalContentAlignment = VerticalAlignment.Center;

                TextBlock useAsDefaultText = new TextBlock
                {
                    Text = Lang._Misc.UseAsDefault,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Opacity = 0.25,
                    Name = "UseAsDefaultLabel"
                };
                useAsDefaultText.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                Grid iconTextGrid = CollapseUIExt.CreateIconTextGrid(
                    langlist[i],
                    "\uf1ab",
                    iconSize: 14,
                    textSize: 14,
                    iconFontFamily: "FontAwesomeSolid")
                    .WithOpacity(0.25);
                iconTextGrid.Name = "IconText";
                iconTextGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                iconTextGrid.VerticalAlignment = VerticalAlignment.Center;

                checkBoxGrid.AddElementToGridColumn(iconTextGrid, 0);
                checkBoxGrid.AddElementToGridColumn(useAsDefaultText, 1);

                RadioButton radioButton = new RadioButton
                {
                    Content = checkBox,
                    Style = CollapseUIExt.GetApplicationResource<Style>("AudioLanguageSelectionRadioButtonStyle"),
                    Background = CollapseUIExt.GetApplicationResource<Brush>("AudioLanguageSelectionRadioButtonBrush")
                }
                .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                .WithVerticalAlignment(VerticalAlignment.Center);

                defaultChoiceRadioButton.Items.Add(radioButton);

                radioButton.Tag = i;
                checkBox.Tag = i;

                radioButton.Checked += (sender, _) =>
                {
                    RadioButton radioButtonLocal = sender as RadioButton;
                    choiceAsDefault = (int)radioButtonLocal.Tag;
                    checkBox.IsChecked = true;

                    TextBlock textBlockLocal = (TextBlock)radioButtonLocal.FindDescendant("UseAsDefaultLabel");
                    if (textBlockLocal != null)
                        textBlockLocal.Opacity = 1;
                };

                radioButton.Unchecked += (sender, _) =>
                {
                    RadioButton radioButtonLocal = sender as RadioButton;
                    TextBlock textBlockLocal = (TextBlock)radioButtonLocal.FindDescendant("UseAsDefaultLabel");
                    if (textBlockLocal != null)
                        textBlockLocal.Opacity = 0.25;
                };

                if (i == defaultIndex)
                {
                    choices[i] = true;
                    checkBox.IsChecked = true;
                    defaultChoiceRadioButton.SelectedIndex = i;
                    iconTextGrid.Opacity = 1;
                }

                checkBox.Checked += (sender, _) =>
                {
                    CheckBox thisCheckBox = sender as CheckBox;
                    int thisIndex = (int)thisCheckBox.Tag;
                    choices[thisIndex] = true;
                    radioButton.IsEnabled = true;

                    bool isHasAnyChoices = choices.Any(x => x);
                    dialog.IsPrimaryButtonEnabled = isHasAnyChoices;
                    if (defaultChoiceRadioButton.SelectedIndex < 0)
                        defaultChoiceRadioButton.SelectedIndex = thisIndex;

                    Grid thisIconText = (Grid)thisCheckBox.FindDescendant("IconText");
                    if (thisIconText != null)
                        thisIconText.Opacity = 1;
                };
                checkBox.Unchecked += (sender, _) =>
                {
                    CheckBox thisCheckBox = sender as CheckBox;
                    int thisIndex = (int)thisCheckBox.Tag;
                    choices[thisIndex] = false;
                    radioButton.IsChecked = false;

                    Grid thisIconText = (Grid)thisCheckBox.FindDescendant("IconText");
                    if (thisIconText != null)
                        thisIconText.Opacity = 0.25;

                    bool isHasAnyChoices = choices.Any(x => x);
                    dialog.IsPrimaryButtonEnabled = isHasAnyChoices;

                    // TODO: Find a better way rather than this SPAGHEETTTTT CODE
                    if (defaultChoiceRadioButton.SelectedIndex < 0 && isHasAnyChoices)
                    {
                        for (int i = 0; i < choices.Length; i++)
                        {
                            if (choices[i])
                            {
                                defaultChoiceRadioButton.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                };
            }

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.None)
                return (null, -1);

            List<int> returnList = new List<int>();
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i])
                    returnList.Add(i);
            }

            return (returnList, choiceAsDefault);
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
            Dictionary<string, PresetConfig> ConvertibleRegions = new Dictionary<string, PresetConfig>();
            foreach (KeyValuePair<string, PresetConfig> Config in LauncherMetadataHelper.LauncherMetadataConfig[LauncherMetadataHelper.CurrentMetadataConfigGameName]
                .Where(x => x.Value.IsConvertible ?? false))
                ConvertibleRegions.Add(Config.Key, Config.Value);

            ContentDialogCollapse Dialog = new ContentDialogCollapse();
            ComboBox SourceGame = new ComboBox();
            ComboBox TargetGame = new ComboBox();

            SelectionChangedEventHandler SourceGameChangedArgs = (object sender, SelectionChangedEventArgs e) =>
                                                                 {
                                                                     TargetGame.IsEnabled            = true;
                                                                     Dialog.IsSecondaryButtonEnabled = false;
                                                                     TargetGame.ItemsSource =
                                                                         InnerLauncherConfig
                                                                            .BuildGameRegionListUI(LauncherMetadataHelper.CurrentMetadataConfigGameName,
                                                                                 InstallationConvert
                                                                                    .GetConvertibleNameList(
                                                                        InnerLauncherConfig.GetComboBoxGameRegionValue((sender as ComboBox).SelectedItem)));
                                                                 };
            SelectionChangedEventHandler TargetGameChangedArgs = (object sender, SelectionChangedEventArgs e) =>
                                                                 {
                                                                     if ((sender as ComboBox).SelectedIndex != -1)
                                                                         Dialog.IsSecondaryButtonEnabled = true;
                                                                 };
            SourceGame = new ComboBox
            {
                Width = 200,
                ItemsSource = InnerLauncherConfig.BuildGameRegionListUI(LauncherMetadataHelper.CurrentMetadataConfigGameName, new List<string>(ConvertibleRegions.Keys)),
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

            StackPanel DialogContainer = CollapseUIExt.CreateStackPanel();
            StackPanel ComboBoxContainer = CollapseUIExt.CreateStackPanel(Orientation.Horizontal).WithHorizontalAlignment(HorizontalAlignment.Center);
            ComboBoxContainer.AddElementToStackPanel(
                SourceGame,
                new FontIcon()
                {
                    Glyph = "",
                    FontFamily = FontCollections.FontAwesomeSolid,
                    Opacity = 0.5f
                }.WithVerticalAlignment(VerticalAlignment.Center).WithMargin(16d, 0d),
                TargetGame
                );
            DialogContainer.AddElementToStackPanel(new TextBlock
            {
                Text = Lang._InstallConvert.SelectDialogSubtitle,
                TextWrapping = TextWrapping.Wrap
            }.WithMargin(0d, 0d, 0d, 16d));
            DialogContainer.AddElementToStackPanel(ComboBoxContainer);

            Dialog = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = Lang._InstallConvert.SelectDialogTitle,
                Content = DialogContainer,
                CloseButtonText = null,
                PrimaryButtonText = Lang._Misc.Cancel,
                SecondaryButtonText = Lang._Misc.Next,
                IsSecondaryButtonEnabled = false,
                DefaultButton = ContentDialogButton.Secondary,
                Background = CollapseUIExt.GetApplicationResource<Brush>("DialogAcrylicBrush"),
                Style = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                XamlRoot = Content.XamlRoot
            };
            return (await Dialog.QueueAndSpawnDialog(), SourceGame, TargetGame);
        }

        public static async Task<ContentDialogResult> Dialog_LocateDownloadedConvertRecipe(UIElement Content, string FileName)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.CookbookLocateSubtitle1 });
            texts.Inlines.Add(new Hyperlink()
            {
                Inlines = { new Run { Text = Lang._Dialogs.CookbookLocateSubtitle2, FontWeight = FontWeights.Bold, Foreground = CollapseUIExt.GetApplicationResource<Brush>("AccentColor") } },
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

        public static async Task<ContentDialogResult> Dialog_ExistingInstallationSteam(UIElement Content, string gamePath) =>
            await SpawnDialog(
                        Lang._Dialogs.ExistingInstallSteamTitle,
                        string.Format(Lang._Dialogs.ExistingInstallSteamSubtitle, gamePath),
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.YesMigrateIt,
                        Lang._Misc.NoKeepInstallIt,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
            );


        public static async Task<ContentDialogResult> Dialog_MigrationChoiceDialog(UIElement Content, string existingGamePath, string gameTitle, string gameRegion, string launcherName,
            MigrateFromLauncherType migrateFromLauncherType)
        {
            if (migrateFromLauncherType != MigrateFromLauncherType.Official)
            {
                switch (migrateFromLauncherType)
                {
                    case MigrateFromLauncherType.BetterHi3Launcher:
                        return await Dialog_ExistingInstallationBetterLauncher(Content, existingGamePath);
                    case MigrateFromLauncherType.Steam:
                        return await Dialog_ExistingInstallationSteam(Content, existingGamePath);
                    default:
                        throw new InvalidOperationException($"Dialog is not supported for unknown migration!");
                }
            }

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

        public static async Task<ContentDialogResult> Dialog_ExistingDownload(UIElement Content, double partialLength, double contentLength) =>
            await SpawnDialog(
                        Lang._Dialogs.InstallDataDownloadResumeTitle,
                        string.Format(Lang._Dialogs.InstallDataDownloadResumeSubtitle,
                                      SummarizeSizeSimple(partialLength),
                                      SummarizeSizeSimple(contentLength)),
                        Content,
                        Lang._Misc.Cancel,
                        Lang._Misc.YesResume,
                        Lang._Misc.NoStartFromBeginning,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Warning
                );

        public static async Task<ContentDialogResult> Dialog_InsufficientDriveSpace(UIElement Content, long DriveFreeSpace, double RequiredSpace, string DriveLetter) =>
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

        public static async Task<ContentDialogResult> Dialog_OOBEVideoBackgroundPreviewUnavailable(UIElement Content) =>
            await SpawnDialog(
                        Lang._OOBEStartUpMenu.VideoBackgroundPreviewUnavailableHeader,
                        Lang._OOBEStartUpMenu.VideoBackgroundPreviewUnavailableDescription,
                        Content,
                        null,
                        Lang._Misc.OkayHappy,
                        null,
                        ContentDialogButton.Primary,
                        ContentDialogTheme.Informational
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

        #region Playtime Dialogs
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

        public static async void Dialog_InvalidPlaytime(UIElement Content, int elapsedSeconds = 0)
        {
            StackPanel stack = CollapseUIExt.CreateStackPanel();

            stack.AddElementToStackPanel(
                new TextBlock() { Text = Lang._Dialogs.InvalidPlaytimeSubtitle1, TextWrapping = TextWrapping.Wrap }.WithMargin(0d, 4d),
                new TextBlock() { Text = Lang._Dialogs.InvalidPlaytimeSubtitle2, TextWrapping = TextWrapping.Wrap }.WithMargin(0d, 4d),
                new TextBlock() { Text = string.Format(Lang._HomePage.GamePlaytime_Display, elapsedSeconds / 3600, elapsedSeconds % 3600 / 60), FontWeight = FontWeights.Bold }.WithMargin(0d, 4d).WithHorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock() { Text = Lang._Dialogs.InvalidPlaytimeSubtitle3, TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold }.WithMargin(0d, 4d, 0d, -2d).WithHorizontalAlignment(HorizontalAlignment.Center)
                );

            await SpawnDialog(
                        Lang._Dialogs.InvalidPlaytimeTitle,
                        stack,
                        Content,
                        Lang._Misc.Close,
                        dialogTheme: ContentDialogTheme.Warning
            );
        }
        #endregion

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
        
        public static async Task<ContentDialogResult> Dialog_QuickSettingsChangeBG(UIElement Content)
        {
            StackPanel mainStack        = CollapseUIExt.CreateStackPanel();
            StackPanel mainStackContent = CollapseUIExt.CreateStackPanel();

            CheckBox enableBG     = new CheckBox();
            Button   selectBGPath = new Button();
            
            enableBG.Content     = Lang._SettingsPage.AppBG_Checkbox;
            enableBG.IsChecked   = false;
            selectBGPath.Content = Lang._Misc.Select;

            selectBGPath.Visibility = Visibility.Collapsed;

            string bgPathLocation = "";

            enableBG.Checked += (sender, _) =>
              {
                  if (sender is CheckBox enableBgCheckBox)
                  {
                      selectBGPath.Visibility = Visibility.Visible;
                  }
              };

            enableBG.Unchecked += (sender, _) =>
              {
                  if (sender is CheckBox enableBgCheckBox)
                  {
                      selectBGPath.Visibility = Visibility.Collapsed;
                  }
              };

            mainStack.AddElementToStackPanel(mainStackContent);
            mainStackContent.AddElementToStackPanel(enableBG, selectBGPath);
            
            selectBGPath.Click += async (_, _) =>
                                  {
                                      bgPathLocation =
                                          await FileDialogNative.GetFilePicker(ImageLoaderHelper.SupportedImageFormats, Lang._Dialogs
                                                                                  .QuickSettingsChangeBGSelectImgDialogTitle);
                                      if (!string.IsNullOrEmpty(bgPathLocation))
                                      {
                                          // Load image
                                          ImageLoaderHelper.LoadImage(bgPathLocation);
                                          // FIXME: This will crash if the user loads a video
                                          
                                      }
                                  };
            
            return await SpawnDialog(
                                     Lang._Dialogs.QuickSettingsChangeBGTitle,
                                     mainStack,
                                     Content,
                                     Lang._Misc.NoCancel,
                                     Lang._Misc.OkayHappy
                                    );
        }

        public static async Task<ContentDialogResult> Dialog_GenericWarning(UIElement Content)
        {
            return await SpawnDialog(
                Lang._UnhandledExceptionPage.UnhandledTitle4,
                Lang._UnhandledExceptionPage.UnhandledSubtitle4,
                Content,
                Lang._Misc.Okay,
                null,
                null,
                ContentDialogButton.Primary,
                ContentDialogTheme.Warning
            );

        }

        public static async Task<ContentDialogResult> Dialog_ShowUnhandledExceptionMenu(UIElement Content)
        {
            async void CopyTextToClipboard(object sender, RoutedEventArgs e)
            {
                InvokeProp.CopyStringToClipboard(ErrorSender.ExceptionContent);
                if (sender is Button btn && btn.Content != null && btn.Content is Panel panel)
                {
                    FontIcon fontIcon = panel.Children[0] as FontIcon;
                    TextBlock textBlock = panel.Children[1] as TextBlock;

                    string lastGlyph = fontIcon.Glyph;
                    string lastText = textBlock.Text;

                    fontIcon.Glyph = "";
                    textBlock.Text = Lang._UnhandledExceptionPage.CopyClipboardBtn2;
                    btn.IsEnabled = false;

                    await Task.Delay(1000);

                    fontIcon.Glyph = lastGlyph;
                    textBlock.Text = lastText;
                    btn.IsEnabled = true;
                }
            }

            Button copyButton = null;

            try
            {
                string exceptionContent = ErrorSender.ExceptionContent;
                string title = ErrorSender.ExceptionTitle;
                string subtitle = ErrorSender.ExceptionSubtitle;

                bool isShowBackButton = (ErrorSender.ExceptionType == ErrorType.Connection) && (WindowUtility.CurrentWindow as MainWindow).rootFrame.CanGoBack;

                Grid rootGrid = CollapseUIExt.CreateGrid()
                    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                    .WithVerticalAlignment(VerticalAlignment.Stretch)
                    .WithRows(GridLength.Auto, new(1, GridUnitType.Star), GridLength.Auto);

                _ = rootGrid.AddElementToGridRow(new TextBlock
                {
                    Text = subtitle,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.Medium
                }, 0);
                _ = rootGrid.AddElementToGridRow(new TextBox
                {
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 300,
                    AcceptsReturn = true,
                    Text = exceptionContent
                }, 1).WithMargin(0d, 8d)
                     .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                     .WithVerticalAlignment(VerticalAlignment.Stretch);

                copyButton = rootGrid.AddElementToGridRow(
                    CollapseUIExt.CreateButtonWithIcon<Button>(
                        text:           Lang._UnhandledExceptionPage!.CopyClipboardBtn1,
                        iconGlyph:      "",
                        iconFontFamily: "FontAwesomeSolid",
                        buttonStyle:    "AccentButtonStyle"
                    ), 2)
                    .WithHorizontalAlignment(HorizontalAlignment.Center);
                copyButton.Click += CopyTextToClipboard;

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
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;
            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmSubtitle1 }.WithMargin(0d, 2d, 0d, 4d).WithHorizontalAlignment(HorizontalAlignment.Center));
            
            TextBlock pathText = new TextBlock { TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0, 4d).WithHorizontalAlignment(HorizontalAlignment.Center);
            pathText.AddTextBlockLine(path, FontWeights.Bold);
            
            panel.AddElementToStackPanel(
                pathText,
                new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmSubtitle2, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 4d).WithHorizontalAlignment(HorizontalAlignment.Center));

            CheckBox playOnLoad = panel.AddElementToStackPanel(new CheckBox() {
                Content = new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmCheckBox, TextWrapping = TextWrapping.WrapWholeWords }
            }.WithMargin(0d, 4d, 0d, -8d).WithHorizontalAlignment(HorizontalAlignment.Center));
        
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

            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;
            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle1 }.WithMargin(0d, 2d, 0d, 4d).WithHorizontalAlignment(HorizontalAlignment.Center));

            TextBlock pathText = new TextBlock { HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.WrapWholeWords, Margin = new Thickness(0, 4, 0, 4) };
            pathText.AddTextBlockLine(message: Lang._Dialogs.ShortcutCreationSuccessSubtitle2);
            pathText.AddTextBlockLine(message: path, FontWeights.Bold);
            panel.AddElementToStackPanel(pathText);

            if (play)
            {
                panel.AddElementToStackPanel(
                    new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle3, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap }.WithMargin(0d, 8d, 0d, 4d),
                    new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle4, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d),
                    new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle5, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d));
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
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;

            panel.AddElementToStackPanel(
                new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationConfirmSubtitle1, TextWrapping = TextWrapping.WrapWholeWords }.WithHorizontalAlignment(HorizontalAlignment.Center).WithMargin(0d, 4d, 0d, 2d),
                new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationConfirmSubtitle2, TextWrapping = TextWrapping.WrapWholeWords }.WithHorizontalAlignment(HorizontalAlignment.Center).WithMargin(0d, 2d, 0d, 4d));

            CheckBox playOnLoad = panel.AddElementToStackPanel(new CheckBox()
            {
                Content = new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationConfirmCheckBox, TextWrapping = TextWrapping.Wrap }
            }.WithHorizontalAlignment(HorizontalAlignment.Center).WithMargin(0d, 4d, 0d, -8d));

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
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;

            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle1, TextWrapping = TextWrapping.WrapWholeWords }.WithHorizontalAlignment(HorizontalAlignment.Center).WithMargin(0d, 2d, 0d, 4d),
                                         new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle2, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 8d, 0d, 4d));
            
            if (play) panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle3, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 2d),
                                                   new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle7, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 2d));

            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle5, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 2d),
                                         new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle4, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 2d),
                                         new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle6, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 4d));

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
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 350d;
            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationFailureSubtitle, TextWrapping = TextWrapping.Wrap }.WithMargin(0d, 2d, 0d, 4d));

            return await SpawnDialog(
                Lang._Dialogs.SteamShortcutCreationFailureTitle,
                panel,
                Content,
                Lang._Misc.Close,
                dialogTheme: ContentDialogTheme.Error
                );
        }
        #endregion

        internal static async Task<ContentDialogResult> Dialog_DownloadSettings(UIElement Content, GamePresetProperty currentGameProperty)
        {
            ToggleSwitch startAfterInstall = new ToggleSwitch
            {
                IsOn = currentGameProperty._GameInstall.StartAfterInstall,
                OffContent = Lang._Misc.Disabled,
                OnContent = Lang._Misc.Enabled
            };
            startAfterInstall.Toggled += (_, _) => currentGameProperty._GameInstall.StartAfterInstall = startAfterInstall.IsOn;

            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.AddElementToStackPanel(
                new TextBlock { Text = Lang._Dialogs.DownloadSettingsOption1 }.WithMargin(0d, 0d, 0d, 4d),
                startAfterInstall
                );

            return await SpawnDialog(
                Lang._Dialogs.DownloadSettingsTitle,
                panel,
                Content,
                Lang._Misc.Close
            );
        }

        private static IAsyncOperation<ContentDialogResult> CurrentSpawnedDialogTask = null;

        public static async ValueTask<ContentDialogResult> SpawnDialog(
            string title, object content, UIElement Content,
            string closeText = null, string primaryText = null,
            string secondaryText = null, ContentDialogButton defaultButton = ContentDialogButton.Primary,
            ContentDialogTheme dialogTheme = ContentDialogTheme.Informational)
        {
            return await Content.DispatcherQueue.EnqueueAsync(async() =>
            {
                // Create a new instance of dialog
                ContentDialogCollapse dialog = new ContentDialogCollapse(dialogTheme)
                {
                    Title = title,
                    Content = content,
                    CloseButtonText = closeText,
                    PrimaryButtonText = primaryText,
                    SecondaryButtonText = secondaryText,
                    DefaultButton = defaultButton,
                    Style = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                    XamlRoot = (WindowUtility.CurrentWindow is MainWindow mainWindow) ? mainWindow.Content.XamlRoot : Content.XamlRoot
                };

                // Queue and spawn the dialog instance
                return await dialog.QueueAndSpawnDialog();
            });
        }

        public static async ValueTask<ContentDialogResult> QueueAndSpawnDialog(this ContentDialog dialog)
        {
            // If a dialog is currently spawned, then await until the task is completed
            while (CurrentSpawnedDialogTask != null && CurrentSpawnedDialogTask.Status == AsyncStatus.Started) await Task.Delay(200);

            // Set the theme of the content
            if (WindowUtility.CurrentWindow is MainWindow window)
            {
                if (dialog is ContentDialogCollapse contentDialogCollapse)
                    window.ContentDialog = contentDialogCollapse;

                dialog.RequestedTheme = InnerLauncherConfig.IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            }

            // Assign the dialog to the global task
            CurrentSpawnedDialogTask = dialog is ContentDialogCollapse dialogCollapse ? dialogCollapse.ShowAsync() : (dialog is ContentDialogOverlay overlapCollapse ? overlapCollapse.ShowAsync() : dialog.ShowAsync());
            // Spawn and await for the result
            ContentDialogResult dialogResult = await CurrentSpawnedDialogTask;
            return dialogResult; // Return the result
        }
    }
}
