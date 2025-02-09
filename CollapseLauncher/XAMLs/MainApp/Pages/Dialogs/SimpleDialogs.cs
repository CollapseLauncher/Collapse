using CollapseLauncher.CustomControls;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.XAMLs.Theme.CustomControls.UserFeedbackDialog;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.Native.ManagedTools;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
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

// ReSharper disable CommentTypo
// ReSharper disable LoopCanBeConvertedToQuery
// ReSharper disable IdentifierTypo
// ReSharper disable SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault

#nullable enable
namespace CollapseLauncher.Dialogs
{
    public static class SimpleDialogs
    {
        private static IAsyncOperation<ContentDialogResult>? _currentSpawnedDialogTask;
        private static DispatcherQueue?                      _sharedDispatcherQueue;

        private static XamlRoot? SharedXamlRoot => field ??=
            WindowUtility.CurrentWindow is MainWindow mainWindow ? mainWindow.Content.XamlRoot : null;

        public static Task<ContentDialogResult> Dialog_DeltaPatchFileDetected(string sourceVer, string targetVer)
        {
            return SpawnDialog(Lang._Dialogs.DeltaPatchDetectedTitle,
                               string.Format(Lang._Dialogs.DeltaPatchDetectedSubtitle, sourceVer, targetVer),
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.Yes,
                               Lang._Misc.No,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_PreDownloadPackageVerified()
        {
            return SpawnDialog(Lang._Dialogs.PreloadVerifiedTitle,
                               Lang._Dialogs.PreloadVerifiedSubtitle,
                               null,
                               Lang._Misc.Close,
                               null,
                               null,
                               ContentDialogButton.Secondary,
                               ContentDialogTheme.Success);
        }

        public static Task<ContentDialogResult> Dialog_PreviousDeltaPatchInstallFailed()
        {
            return SpawnDialog(Lang._Dialogs.DeltaPatchPrevFailedTitle,
                               Lang._Dialogs.DeltaPatchPrevFailedSubtitle,
                               null,
                               null,
                               Lang._Misc.Yes,
                               Lang._Misc.No,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Error);
        }

        public static Task<ContentDialogResult> Dialog_PreviousGameConversionFailed()
        {
            return SpawnDialog(Lang._Dialogs.GameConversionPrevFailedTitle,
                               Lang._Dialogs.GameConversionPrevFailedSubtitle,
                               null,
                               null,
                               Lang._Misc.Yes,
                               Lang._Misc.No,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Error);
        }

        public static Task<ContentDialogResult> Dialog_InstallationLocation()
        {
            return SpawnDialog(Lang._Dialogs.LocateInstallTitle,
                               Lang._Dialogs.LocateInstallSubtitle,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.UseDefaultDir,
                               Lang._Misc.LocateDir);
        }

        public static Task<ContentDialogResult> Dialog_OpenExecutable()
        {
            return SpawnDialog(Lang._Dialogs.LocateExePathTitle,
                               Lang._Dialogs.LocateExePathSubtitle,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.LocateExecutable,
                               Lang._Misc.OpenDownloadPage);
        }

        public static Task<ContentDialogResult> Dialog_InsufficientWritePermission(string path)
        {
            return SpawnDialog(Lang._Dialogs.UnauthorizedDirTitle,
                               string.Format(Lang._Dialogs.UnauthorizedDirSubtitle, path),
                               null,
                               Lang._Misc.Okay);
        }

        public static async Task<(Dictionary<string, string>?, string?)> Dialog_ChooseAudioLanguageChoice(
            Dictionary<string, string> langDict, string defaultLocaleCode = "ja-jp")
        {
            bool[] choices = new bool[langDict.Count];
            if (!langDict.ContainsKey(defaultLocaleCode))
            {
                throw new
                    KeyNotFoundException($"Default locale code: {defaultLocaleCode} is not found within langDict argument");
            }

            List<string> localeCodeList = langDict.Keys.ToList();
            List<string> langList       = langDict.Values.ToList();

            // Naive approach to lookup default index value
            string? refLocaleCode =
                localeCodeList.FirstOrDefault(x => x.Equals(defaultLocaleCode, StringComparison.OrdinalIgnoreCase));
            int        defaultIndex    = localeCodeList.IndexOf(refLocaleCode ?? "");
            int        choiceAsDefault = defaultIndex;
            StackPanel parentPanel     = CollapseUIExt.CreateStackPanel();

            parentPanel.AddElementToStackPanel(new TextBlock
            {
                Text                = Lang._Dialogs.ChooseAudioLangSubtitle,
                TextWrapping        = TextWrapping.Wrap,
                FontWeight          = FontWeights.Medium,
                Margin              = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            }.WithMargin(0d, 0d, 0d, 16d));
            parentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

            RadioButtons defaultChoiceRadioButton = new RadioButtons()
               .WithHorizontalAlignment(HorizontalAlignment.Center);
            defaultChoiceRadioButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            parentPanel.AddElementToStackPanel(defaultChoiceRadioButton);

            ContentDialogCollapse dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
            {
                Title               = Lang._Dialogs.ChooseAudioLangTitle,
                Content             = parentPanel,
                CloseButtonText     = Lang._Misc.Cancel,
                PrimaryButtonText   = Lang._Misc.Next,
                SecondaryButtonText = null,
                DefaultButton       = ContentDialogButton.Primary,
                Style               = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                XamlRoot            = SharedXamlRoot
            };

            InputCursor inputCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            for (int i = 0; i < langList.Count; i++)
            {
                Grid checkBoxGrid = CollapseUIExt.CreateGrid()
                                                 .WithColumns(new GridLength(1, GridUnitType.Star),
                                                              new GridLength(1, GridUnitType.Auto))
                                                 .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                                 .WithMargin(0, 0, 0, 8);

                CheckBox checkBox = new CheckBox
                {
                    Content                    = checkBoxGrid,
                    HorizontalAlignment        = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment          = VerticalAlignment.Center,
                    VerticalContentAlignment   = VerticalAlignment.Center
                };

                TextBlock useAsDefaultText = new TextBlock
                {
                    Text                    = Lang._Misc.UseAsDefault,
                    HorizontalAlignment     = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right,
                    VerticalAlignment       = VerticalAlignment.Top,
                    Opacity                 = 0.5,
                    Name                    = "UseAsDefaultLabel"
                };
                useAsDefaultText.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                Grid iconTextGrid = CollapseUIExt.CreateIconTextGrid(
                                                                     langList[i],
                                                                     "\uf1ab",
                                                                     iconSize: 14,
                                                                     textSize: 14,
                                                                     iconFontFamily: "FontAwesomeSolid")
                                                 .WithOpacity(0.5);
                iconTextGrid.Name = "IconText";
                iconTextGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                iconTextGrid.VerticalAlignment = VerticalAlignment.Center;

                checkBoxGrid.AddElementToGridColumn(iconTextGrid,     0);
                checkBoxGrid.AddElementToGridColumn(useAsDefaultText, 1);

                RadioButton radioButton = new RadioButton
                                          {
                                              Content = checkBox,
                                              Style =
                                                  CollapseUIExt
                                                     .GetApplicationResource<
                                                          Style>("AudioLanguageSelectionRadioButtonStyle"),
                                              Background =
                                                  CollapseUIExt
                                                     .GetApplicationResource<
                                                          Brush>("AudioLanguageSelectionRadioButtonBrush")
                                          }
                                         .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                         .WithVerticalAlignment(VerticalAlignment.Center)
                                         .WithCursor(inputCursor);

                defaultChoiceRadioButton.Items.Add(radioButton);

                radioButton.Tag = i;
                checkBox.Tag    = i;

                radioButton.Checked += (sender, _) =>
                                       {
                                           RadioButton? radioButtonLocal = sender as RadioButton;
                                           choiceAsDefault    = (int)(radioButtonLocal?.Tag ?? 0);
                                           checkBox.IsChecked = true;

                                           if (radioButtonLocal?.FindDescendant("UseAsDefaultLabel") is TextBlock
                                               textBlockLocal)
                                           {
                                               textBlockLocal.Opacity = 1;
                                           }
                                       };

                radioButton.Unchecked += (sender, _) =>
                                         {
                                             RadioButton? radioButtonLocal = sender as RadioButton;
                                             if (radioButtonLocal?.FindDescendant("UseAsDefaultLabel") is TextBlock
                                                 textBlockLocal)
                                             {
                                                 textBlockLocal.Opacity = 0.5;
                                             }
                                         };

                radioButton.PointerEntered += (sender, _) =>
                                              {
                                                  RadioButton? radioButtonLocal = sender as RadioButton;
                                                  TextBlock? textBlockLocal =
                                                      radioButtonLocal
                                                        ?.FindDescendant("UseAsDefaultLabel") as TextBlock;

                                                  CheckBox? thisCheckBox = radioButtonLocal?.Content as CheckBox;
                                                  Grid? thisIconText = thisCheckBox?.FindDescendant("IconText") as Grid;
                                                  if (textBlockLocal is null || thisIconText is null ||
                                                      (thisCheckBox?.IsChecked ?? false))
                                                  {
                                                      return;
                                                  }

                                                  textBlockLocal.Opacity = 1;
                                                  thisIconText.Opacity   = 1;
                                              };

                radioButton.PointerExited += (sender, _) =>
                                             {
                                                 RadioButton? radioButtonLocal = sender as RadioButton;
                                                 TextBlock? textBlockLocal =
                                                     radioButtonLocal?.FindDescendant("UseAsDefaultLabel") as TextBlock;

                                                 CheckBox? thisCheckBox = radioButtonLocal?.Content as CheckBox;
                                                 Grid? thisIconText = thisCheckBox?.FindDescendant("IconText") as Grid;
                                                 if (textBlockLocal is null || thisIconText is null ||
                                                     (thisCheckBox?.IsChecked ?? false))
                                                 {
                                                     return;
                                                 }

                                                 textBlockLocal.Opacity = 0.5;
                                                 thisIconText.Opacity   = 0.5;
                                             };

                if (i == defaultIndex)
                {
                    choices[i]                             = true;
                    checkBox.IsChecked                     = true;
                    defaultChoiceRadioButton.SelectedIndex = i;
                    iconTextGrid.Opacity                   = 1;
                }

                checkBox.Checked += (sender, _) =>
                                    {
                                        CheckBox? thisCheckBox = sender as CheckBox;
                                        int       thisIndex    = (int)(thisCheckBox?.Tag ?? 0);
                                        choices[thisIndex]    = true;
                                        radioButton.IsEnabled = true;

                                        bool isHasAnyChoices = choices.Any(x => x);
                                        dialog.IsPrimaryButtonEnabled = isHasAnyChoices;
                                        if (defaultChoiceRadioButton.SelectedIndex < 0)
                                        {
                                            defaultChoiceRadioButton.SelectedIndex = thisIndex;
                                        }

                                        if (thisCheckBox?.FindDescendant("IconText") is Grid thisIconText)
                                        {
                                            thisIconText.Opacity = 1;
                                        }
                                    };
                checkBox.Unchecked += (sender, _) =>
                                      {
                                          CheckBox? thisCheckBox = sender as CheckBox;
                                          int       thisIndex    = (int)(thisCheckBox?.Tag ?? 0);
                                          choices[thisIndex]    = false;
                                          radioButton.IsChecked = false;

                                          if (thisCheckBox?.FindDescendant("IconText") is Grid thisIconText)
                                          {
                                              thisIconText.Opacity = 0.5;
                                          }

                                          bool isHasAnyChoices = choices.Any(x => x);
                                          dialog.IsPrimaryButtonEnabled = isHasAnyChoices;

                                          // TODO: Find a better way rather than this SPAGHEETTTTT CODE
                                          if (defaultChoiceRadioButton.SelectedIndex >= 0 || !isHasAnyChoices)
                                          {
                                              return;
                                          }

                                          for (int index = 0; index < choices.Length; index++)
                                          {
                                              if (!choices[index])
                                              {
                                                  continue;
                                              }

                                              defaultChoiceRadioButton.SelectedIndex = index;
                                              break;
                                          }
                                      };
            }

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.None)
            {
                return (null, null);
            }

            Dictionary<string, string> returnDictionary = new();
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i])
                {
                    returnDictionary.Add(localeCodeList[i], langList[i]);
                }
            }

            return (returnDictionary, localeCodeList[choiceAsDefault]);
        }

        public static async Task<(List<int>?, int)> Dialog_ChooseAudioLanguageChoice(
            List<string> langList, int defaultIndex = 2)
        {
            bool[]     choices         = new bool[langList.Count];
            int        choiceAsDefault = defaultIndex;
            StackPanel parentPanel     = CollapseUIExt.CreateStackPanel();

            parentPanel.AddElementToStackPanel(new TextBlock
            {
                Text                = Lang._Dialogs.ChooseAudioLangSubtitle,
                TextWrapping        = TextWrapping.Wrap,
                FontWeight          = FontWeights.Medium,
                Margin              = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center
            }.WithMargin(0d, 0d, 0d, 16d));
            parentPanel.HorizontalAlignment = HorizontalAlignment.Stretch;

            RadioButtons defaultChoiceRadioButton = new RadioButtons()
               .WithHorizontalAlignment(HorizontalAlignment.Center);
            defaultChoiceRadioButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;

            parentPanel.AddElementToStackPanel(defaultChoiceRadioButton);

            ContentDialogCollapse dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
            {
                Title               = Lang._Dialogs.ChooseAudioLangTitle,
                Content             = parentPanel,
                CloseButtonText     = Lang._Misc.Cancel,
                PrimaryButtonText   = Lang._Misc.Next,
                SecondaryButtonText = null,
                DefaultButton       = ContentDialogButton.Primary,
                Style               = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                XamlRoot = WindowUtility.CurrentWindow is MainWindow mainWindow
                    ? mainWindow.Content.XamlRoot
                    : throw new NullReferenceException("WindowUtility.CurrentWindow cannot be null!")
            };

            InputCursor inputCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            for (int i = 0; i < langList.Count; i++)
            {
                Grid checkBoxGrid = CollapseUIExt.CreateGrid()
                                                 .WithColumns(new GridLength(1, GridUnitType.Star),
                                                              new GridLength(1, GridUnitType.Auto))
                                                 .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                                 .WithMargin(0, 0, 0, 8);

                CheckBox checkBox = new CheckBox
                {
                    Content                    = checkBoxGrid,
                    HorizontalAlignment        = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment          = VerticalAlignment.Center,
                    VerticalContentAlignment   = VerticalAlignment.Center
                };

                TextBlock useAsDefaultText = new TextBlock
                {
                    Text                    = Lang._Misc.UseAsDefault,
                    HorizontalAlignment     = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right,
                    VerticalAlignment       = VerticalAlignment.Top,
                    Opacity                 = 0.5,
                    Name                    = "UseAsDefaultLabel"
                };
                useAsDefaultText.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                Grid iconTextGrid = CollapseUIExt.CreateIconTextGrid(
                                                                     langList[i],
                                                                     "\uf1ab",
                                                                     iconSize: 14,
                                                                     textSize: 14,
                                                                     iconFontFamily: "FontAwesomeSolid")
                                                 .WithOpacity(0.5);
                iconTextGrid.Name = "IconText";
                iconTextGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                iconTextGrid.VerticalAlignment = VerticalAlignment.Center;

                checkBoxGrid.AddElementToGridColumn(iconTextGrid,     0);
                checkBoxGrid.AddElementToGridColumn(useAsDefaultText, 1);

                RadioButton radioButton = new RadioButton
                                          {
                                              Content = checkBox,
                                              Style =
                                                  CollapseUIExt
                                                     .GetApplicationResource<
                                                          Style>("AudioLanguageSelectionRadioButtonStyle"),
                                              Background =
                                                  CollapseUIExt
                                                     .GetApplicationResource<
                                                          Brush>("AudioLanguageSelectionRadioButtonBrush")
                                          }
                                         .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                         .WithVerticalAlignment(VerticalAlignment.Center)
                                         .WithCursor(inputCursor);

                defaultChoiceRadioButton.Items.Add(radioButton);

                radioButton.Tag = i;
                checkBox.Tag    = i;

                radioButton.Checked += (sender, _) =>
                                       {
                                           RadioButton? radioButtonLocal = sender as RadioButton;
                                           choiceAsDefault    = (int)(radioButtonLocal?.Tag ?? 0);
                                           checkBox.IsChecked = true;

                                           if (radioButtonLocal?.FindDescendant("UseAsDefaultLabel") is TextBlock
                                               textBlockLocal)
                                           {
                                               textBlockLocal.Opacity = 1;
                                           }
                                       };

                radioButton.Unchecked += (sender, _) =>
                                         {
                                             RadioButton? radioButtonLocal = sender as RadioButton;
                                             if (radioButtonLocal?.FindDescendant("UseAsDefaultLabel") is TextBlock
                                                 textBlockLocal)
                                             {
                                                 textBlockLocal.Opacity = 0.5;
                                             }
                                         };

                radioButton.PointerEntered += (sender, _) =>
                                              {
                                                  RadioButton? radioButtonLocal = sender as RadioButton;
                                                  TextBlock? textBlockLocal =
                                                      radioButtonLocal
                                                        ?.FindDescendant("UseAsDefaultLabel") as TextBlock;

                                                  CheckBox? thisCheckBox = radioButtonLocal?.Content as CheckBox;
                                                  Grid? thisIconText = thisCheckBox?.FindDescendant("IconText") as Grid;
                                                  if (textBlockLocal is null || thisIconText is null ||
                                                      (thisCheckBox?.IsChecked ?? false))
                                                  {
                                                      return;
                                                  }

                                                  textBlockLocal.Opacity = 1;
                                                  thisIconText.Opacity   = 1;
                                              };

                radioButton.PointerExited += (sender, _) =>
                                             {
                                                 RadioButton? radioButtonLocal = sender as RadioButton;
                                                 TextBlock? textBlockLocal =
                                                     radioButtonLocal?.FindDescendant("UseAsDefaultLabel") as TextBlock;

                                                 CheckBox? thisCheckBox = radioButtonLocal?.Content as CheckBox;
                                                 Grid? thisIconText = thisCheckBox?.FindDescendant("IconText") as Grid;
                                                 if (textBlockLocal is null || thisIconText is null ||
                                                     (thisCheckBox?.IsChecked ?? false))
                                                 {
                                                     return;
                                                 }

                                                 textBlockLocal.Opacity = 0.5;
                                                 thisIconText.Opacity   = 0.5;
                                             };

                if (i == defaultIndex)
                {
                    choices[i]                             = true;
                    checkBox.IsChecked                     = true;
                    defaultChoiceRadioButton.SelectedIndex = i;
                    iconTextGrid.Opacity                   = 1;
                }

                checkBox.Checked += (sender, _) =>
                                    {
                                        CheckBox? thisCheckBox = sender as CheckBox;
                                        int       thisIndex    = (int)(thisCheckBox?.Tag ?? 0);
                                        choices[thisIndex]    = true;
                                        radioButton.IsEnabled = true;

                                        bool isHasAnyChoices = choices.Any(x => x);
                                        dialog.IsPrimaryButtonEnabled = isHasAnyChoices;
                                        if (defaultChoiceRadioButton.SelectedIndex < 0)
                                        {
                                            defaultChoiceRadioButton.SelectedIndex = thisIndex;
                                        }

                                        if (thisCheckBox?.FindDescendant("IconText") is Grid thisIconText)
                                        {
                                            thisIconText.Opacity = 1;
                                        }

                                        RadioButton? thisRadioButton = thisCheckBox?.Parent as RadioButton;
                                        TextBlock? textBlockLocal =
                                            thisRadioButton?.FindDescendant("UseAsDefaultLabel") as TextBlock;
                                        if (thisRadioButton is null || textBlockLocal is null)
                                        {
                                            return;
                                        }

                                        if (thisIndex != defaultChoiceRadioButton.SelectedIndex)
                                        {
                                            textBlockLocal.Opacity = 0.5;
                                        }
                                    };
                checkBox.Unchecked += (sender, _) =>
                                      {
                                          CheckBox? thisCheckBox = sender as CheckBox;
                                          int       thisIndex    = (int)(thisCheckBox?.Tag ?? 0);
                                          choices[thisIndex]    = false;
                                          radioButton.IsChecked = false;

                                          if (thisCheckBox?.FindDescendant("IconText") is Grid thisIconText)
                                          {
                                              thisIconText.Opacity = 0.5;
                                          }

                                          bool isHasAnyChoices = choices.Any(x => x);
                                          dialog.IsPrimaryButtonEnabled = isHasAnyChoices;

                                          // TODO: Find a better way rather than this SPAGHEETTTTT CODE
                                          if (defaultChoiceRadioButton.SelectedIndex >= 0 || !isHasAnyChoices)
                                          {
                                              return;
                                          }

                                          for (int index = 0; index < choices.Length; index++)
                                          {
                                              if (!choices[index])
                                              {
                                                  continue;
                                              }

                                              defaultChoiceRadioButton.SelectedIndex = index;
                                              break;
                                          }
                                      };
            }

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.None)
            {
                return (null, -1);
            }

            List<int> returnList = [];
            for (int i = 0; i < choices.Length; i++)
            {
                if (choices[i])
                {
                    returnList.Add(i);
                }
            }

            return (returnList, choiceAsDefault);
        }

        public static Task<ContentDialogResult> Dialog_GraphicsVeryHighWarning()
        {
            return SpawnDialog(Lang._Dialogs.ExtremeGraphicsSettingsWarnTitle,
                               Lang._Dialogs.ExtremeGraphicsSettingsWarnSubtitle,
                               null,
                               null,
                               Lang._Misc.YesIHaveBeefyPC,
                               Lang._Misc.No,
                               ContentDialogButton.Secondary,
                               ContentDialogTheme.Warning);
        }

        public static async Task<(ContentDialogResult, ComboBox, ComboBox)> Dialog_SelectGameConvertRecipe()
        {
            Dictionary<string, PresetConfig> convertibleRegions = new();

            if (!(LauncherMetadataHelper.LauncherMetadataConfig?
                   .TryGetValue(LauncherMetadataHelper.CurrentMetadataConfigGameName ?? "",
                                out Dictionary<string, PresetConfig>? configDictionary) ?? false))
            {
                throw new KeyNotFoundException("Game name not found in metadata config!");
            }

            foreach (KeyValuePair<string, PresetConfig> config in configDictionary!
                        .Where(x => x.Value.IsConvertible ?? false))
            {
                convertibleRegions.Add(config.Key, config.Value);
            }

            ContentDialogCollapse dialog     = new ContentDialogCollapse();
            ComboBox              targetGame = new ComboBox();

            var sourceGame = new ComboBox
            {
                Width = 200,
                ItemsSource =
                    InnerLauncherConfig.BuildGameRegionListUI(LauncherMetadataHelper.CurrentMetadataConfigGameName,
                                                              [.. convertibleRegions.Keys]),
                PlaceholderText = Lang._InstallConvert.SelectDialogSource,
                CornerRadius    = new CornerRadius(14)
            };
            sourceGame.SelectionChanged += SourceGameChangedArgs;
            targetGame = new ComboBox
            {
                Width           = 200,
                PlaceholderText = Lang._InstallConvert.SelectDialogTarget,
                IsEnabled       = false,
                CornerRadius    = new CornerRadius(14)
            };
            targetGame.SelectionChanged += TargetGameChangedArgs;

            StackPanel dialogContainer = CollapseUIExt.CreateStackPanel();
            StackPanel comboBoxContainer = CollapseUIExt.CreateStackPanel(Orientation.Horizontal)
                                                        .WithHorizontalAlignment(HorizontalAlignment.Center);
            comboBoxContainer.AddElementToStackPanel(
                                                     sourceGame,
                                                     new FontIcon
                                                         {
                                                             Glyph      = "",
                                                             FontFamily = FontCollections.FontAwesomeSolid,
                                                             Opacity    = 0.5f
                                                         }.WithVerticalAlignment(VerticalAlignment.Center)
                                                          .WithMargin(16d, 0d),
                                                     targetGame
                                                    );
            dialogContainer.AddElementToStackPanel(new TextBlock
            {
                Text         = Lang._InstallConvert.SelectDialogSubtitle,
                TextWrapping = TextWrapping.Wrap
            }.WithMargin(0d, 0d, 0d, 16d));
            dialogContainer.AddElementToStackPanel(comboBoxContainer);

            dialog = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title                    = Lang._InstallConvert.SelectDialogTitle,
                Content                  = dialogContainer,
                CloseButtonText          = null,
                PrimaryButtonText        = Lang._Misc.Cancel,
                SecondaryButtonText      = Lang._Misc.Next,
                IsSecondaryButtonEnabled = false,
                DefaultButton            = ContentDialogButton.Secondary,
                Background               = CollapseUIExt.GetApplicationResource<Brush>("DialogAcrylicBrush"),
                Style                    = CollapseUIExt.GetApplicationResource<Style>("CollapseContentDialogStyle"),
                XamlRoot                 = SharedXamlRoot
            };
            return (await dialog.QueueAndSpawnDialog(), sourceGame, targetGame);

            void SourceGameChangedArgs(object sender, SelectionChangedEventArgs _)
            {
                // ReSharper disable AccessToModifiedClosure
                targetGame.IsEnabled            = true;
                dialog.IsSecondaryButtonEnabled = false;
                targetGame.ItemsSource =
                    InnerLauncherConfig.BuildGameRegionListUI(LauncherMetadataHelper.CurrentMetadataConfigGameName,
                                                              InstallationConvert
                                                                 .GetConvertibleNameList(InnerLauncherConfig
                                                                     .GetComboBoxGameRegionValue((sender as
                                                                          ComboBox)!.SelectedItem)));
            }

            void TargetGameChangedArgs(object sender, SelectionChangedEventArgs _)
            {
                if ((sender as ComboBox)!.SelectedIndex != -1)
                {
                    dialog.IsSecondaryButtonEnabled = true;
                }
                // ReSharper restore AccessToModifiedClosure
            }
        }

        public static Task<ContentDialogResult> Dialog_LocateDownloadedConvertRecipe(string fileName)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap }
               .AddTextBlockLine(Lang._Dialogs.CookbookLocateSubtitle1);
            texts.Inlines.Add(new Hyperlink
            {
                Inlines =
                {
                    new Run
                    {
                        Text       = Lang._Dialogs.CookbookLocateSubtitle2, FontWeight = FontWeights.Bold,
                        Foreground = CollapseUIExt.GetApplicationResource<Brush>("AccentColor")
                    }
                },
                NavigateUri = new Uri("https://www.mediafire.com/folder/gb09r9fw0ndxb/Hi3ConversionRecipe")
            });
            texts.AddTextBlockLine(Lang._Dialogs.CookbookLocateSubtitle3)
                 .AddTextBlockLine($" {Lang._Misc.Next} ", FontWeights.Bold)
                 .AddTextBlockLine(Lang._Dialogs.CookbookLocateSubtitle5)
                 .AddTextBlockLine(Lang._Dialogs.CookbookLocateSubtitle6, FontWeights.Bold)
                 .AddTextBlockLine(Lang._Dialogs.CookbookLocateSubtitle7)
                 .AddTextBlockLine(fileName, FontWeights.Bold);

            return SpawnDialog(Lang._Dialogs.CookbookLocateTitle,
                               texts,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.Next);
        }

        public static Task<ContentDialogResult> Dialog_ChangeReleaseToChannel(string channelName)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap }
                             .AddTextBlockLine(Lang._Dialogs.ReleaseChannelChangeSubtitle1)
                             .AddTextBlockLine($" {channelName}", FontWeights.Bold)
                             .AddTextBlockNewLine(2)
                             .AddTextBlockLine(Lang._Dialogs.ReleaseChannelChangeSubtitle2, FontWeights.Bold, 18)
                             .AddTextBlockNewLine()
                             .AddTextBlockLine(Lang._Dialogs.ReleaseChannelChangeSubtitle3);

            return SpawnDialog(Lang._Dialogs.ReleaseChannelChangeTitle,
                               texts,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.OkayHappy,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_ForceUpdateOnChannel(string channelName)
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap }
                             .AddTextBlockLine(Lang._Dialogs.ForceUpdateCurrentInstallSubtitle1)
                             .AddTextBlockLine($" {channelName} ", FontWeights.Bold)
                             .AddTextBlockLine(Lang._Dialogs.ForceUpdateCurrentInstallSubtitle2)
                             .AddTextBlockNewLine(2)
                             .AddTextBlockLine(Lang._Dialogs.ReleaseChannelChangeSubtitle2, FontWeights.Bold, 18)
                             .AddTextBlockNewLine()
                             .AddTextBlockLine(Lang._Dialogs.ReleaseChannelChangeSubtitle3);

            return SpawnDialog(Lang._Dialogs.ForceUpdateCurrentInstallTitle,
                               texts,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.OkayHappy,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_ExistingInstallation(string actualLocation)
        {
            return SpawnDialog(Lang._Dialogs.ExistingInstallTitle,
                               string.Format(Lang._Dialogs.ExistingInstallSubtitle, actualLocation),
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.YesMigrateIt,
                               Lang._Misc.NoKeepInstallIt);
        }

        public static Task<ContentDialogResult> Dialog_ExistingInstallationBetterLauncher(
            string gamePath, bool isHasOnlyMigrateOption)
        {
            return SpawnDialog(Lang._Dialogs.ExistingInstallBHI3LTitle,
                               string.Format(Lang._Dialogs.ExistingInstallBHI3LSubtitle, gamePath),
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.YesMigrateIt,
                               isHasOnlyMigrateOption ? null : Lang._Misc.NoKeepInstallIt);
        }

        public static Task<ContentDialogResult> Dialog_ExistingInstallationSteam(
            string gamePath, bool isHasOnlyMigrateOption)
        {
            return SpawnDialog(Lang._Dialogs.ExistingInstallSteamTitle,
                               string.Format(Lang._Dialogs.ExistingInstallSteamSubtitle, gamePath),
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.YesMigrateIt,
                               isHasOnlyMigrateOption ? null : Lang._Misc.NoKeepInstallIt);
        }


        public static Task<ContentDialogResult> Dialog_MigrationChoiceDialog(
            string                  existingGamePath,        string gameTitle, string gameRegion, string launcherName,
            MigrateFromLauncherType migrateFromLauncherType, bool   isHasOnlyMigrateOption)
        {
            if (migrateFromLauncherType != MigrateFromLauncherType.Official)
            {
                return migrateFromLauncherType switch
                       {
                           MigrateFromLauncherType.BetterHi3Launcher =>
                               Dialog_ExistingInstallationBetterLauncher(existingGamePath, isHasOnlyMigrateOption),
                           MigrateFromLauncherType.Steam => Dialog_ExistingInstallationSteam(existingGamePath,
                               isHasOnlyMigrateOption),
                           _ => throw new InvalidOperationException("Dialog is not supported for unknown migration!")
                       };
            }

            string gameFullnameString =
                $"{InnerLauncherConfig.GetGameTitleRegionTranslationString(gameTitle, Lang._GameClientTitles)} - {InnerLauncherConfig.GetGameTitleRegionTranslationString(gameRegion, Lang._GameClientRegions)}";

            TextBlock contentTextBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };
            contentTextBlock.AddTextBlockLine(string.Format(Lang._Dialogs.MigrateExistingInstallChoiceSubtitle1,
                                                            launcherName));
            contentTextBlock.AddTextBlockNewLine(2);
            contentTextBlock.AddTextBlockLine(existingGamePath, FontWeights.SemiBold);
            contentTextBlock.AddTextBlockNewLine(2);
            contentTextBlock.AddTextBlockLine(string.Format(Lang._Dialogs.MigrateExistingInstallChoiceSubtitle2,
                                                            launcherName));

            return SpawnDialog(string.Format(Lang._Dialogs.MigrateExistingInstallChoiceTitle, gameFullnameString),
                               contentTextBlock,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.UseCurrentDir,
                               isHasOnlyMigrateOption ? null : Lang._Misc.MoveToDifferentDir);
        }

        public static Task<ContentDialogResult> Dialog_GameInstallationFileCorrupt(
            string sourceHash, string downloadedHash)
        {
            return SpawnDialog(Lang._Dialogs.InstallDataCorruptTitle,
                               string.Format(Lang._Dialogs.InstallDataCorruptSubtitle, sourceHash, downloadedHash),
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.YesRedownload,
                               Lang._Misc.ExtractAnyway,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Error);
        }

        public static Task<ContentDialogResult> Dialog_GameInstallCorruptedDataAnyway(string fileName, long fileSize)
        {
            TextBlock textBlock = new TextBlock
                                  {
                                      TextWrapping = TextWrapping.Wrap
                                  }
                                 .AddTextBlockLine(Lang._Dialogs.InstallCorruptDataAnywaySubtitle1)
                                 .AddTextBlockLine(string.Format(Lang._Dialogs.InstallCorruptDataAnywaySubtitle2, fileName, SummarizeSizeSimple(fileSize), fileSize),
                                                   FontWeights.SemiBold)
                                 .AddTextBlockLine(Lang._Dialogs.InstallCorruptDataAnywaySubtitle3);

            return SpawnDialog(Lang._Dialogs.InstallCorruptDataAnywayTitle,
                               textBlock,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.YesImReallySure,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_LocateFirstSetupFolder(string defaultAppFolder)
        {
            return SpawnDialog(Lang._StartupPage.ChooseFolderDialogTitle,
                               string.Format(Lang._StartupPage.ChooseFolderDialogSubtitle, defaultAppFolder),
                               null,
                               Lang._StartupPage.ChooseFolderDialogCancel,
                               Lang._StartupPage.ChooseFolderDialogPrimary,
                               Lang._StartupPage.ChooseFolderDialogSecondary);
        }

        public static Task<ContentDialogResult> Dialog_ExistingDownload(double partialLength, double contentLength)
        {
            return SpawnDialog(Lang._Dialogs.InstallDataDownloadResumeTitle,
                               string.Format(Lang._Dialogs.InstallDataDownloadResumeSubtitle,
                                             SummarizeSizeSimple(partialLength),
                                             SummarizeSizeSimple(contentLength)),
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.YesResume,
                               Lang._Misc.NoStartFromBeginning,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_InsufficientDriveSpace(
            long driveFreeSpace, double requiredSpace, string driveLetter)
        {
            return SpawnDialog(Lang._Dialogs.InsufficientDiskTitle,
                               string.Format(Lang._Dialogs.InsufficientDiskSubtitle,
                                             SummarizeSizeSimple(driveFreeSpace),
                                             SummarizeSizeSimple(requiredSpace),
                                             driveLetter),
                               null,
                               null,
                               Lang._Misc.Okay,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Error);
        }

        public static Task<ContentDialogResult> Dialog_WarningOperationNotCancellable()
        {
            TextBlock warningMessage = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap
                }.AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg1)
                 .AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg2, FontWeights.Bold)
                 .AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg3)
                 .AddTextBlockLine(Lang._Misc.Yes, FontWeights.SemiBold)
                 .AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg4)
                 .AddTextBlockLine(Lang._Misc.NoCancel, FontWeights.SemiBold)
                 .AddTextBlockLine(Lang._Dialogs.OperationWarningNotCancellableMsg5);

            return SpawnDialog(Lang._Dialogs.OperationWarningNotCancellableTitle,
                               warningMessage,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_RelocateFolder()
        {
            return SpawnDialog(Lang._Dialogs.RelocateFolderTitle,
                               string.Format(Lang._Dialogs.RelocateFolderSubtitle,
                                             GetAppConfigValue("GameFolder").ToString()),
                               null,
                               null,
                               Lang._Misc.YesRelocate,
                               Lang._Misc.Cancel);
        }

        public static Task<ContentDialogResult> Dialog_UninstallGame(string gameLocation, string region)
        {
            return SpawnDialog(string.Format(Lang._Dialogs.UninstallGameTitle, region),
                               string.Format(Lang._Dialogs.UninstallGameSubtitle,
                                             gameLocation),
                               null,
                               null,
                               Lang._Misc.Uninstall,
                               Lang._Misc.Cancel,
                               ContentDialogButton.Secondary,
                               ContentDialogTheme.Error);
        }

        public static Task<ContentDialogResult> Dialog_EnsureExit()
        {
            return SpawnDialog(Lang._Dialogs.EnsureExitTitle,
                               Lang._Dialogs.EnsureExitSubtitle,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Close,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_ClearMetadata()
        {
            return SpawnDialog(string.Format(Lang._SettingsPage.AppFiles_ClearMetadataDialog),
                               string.Format(Lang._SettingsPage.AppFiles_ClearMetadataDialogHelp),
                               null,
                               null,
                               Lang._Misc.Yes,
                               Lang._Misc.Cancel,
                               ContentDialogButton.Secondary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_NeedInstallMediaPackage()
        {
            return SpawnDialog(Lang._Dialogs.NeedInstallMediaPackTitle,
                               Lang._Dialogs.NeedInstallMediaPackSubtitle1 +
                               Lang._Dialogs.NeedInstallMediaPackSubtitle2,
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.Install,
                               Lang._Misc.Skip,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_OOBEVideoBackgroundPreviewUnavailable()
        {
            return SpawnDialog(Lang._OOBEStartUpMenu.VideoBackgroundPreviewUnavailableHeader,
                               Lang._OOBEStartUpMenu.VideoBackgroundPreviewUnavailableDescription,
                               null,
                               null,
                               Lang._Misc.OkayHappy);
        }

        public static Task<ContentDialogResult> Dialog_InstallMediaPackageFinished()
        {
            return SpawnDialog(Lang._Dialogs.InstallMediaPackCompleteTitle,
                               Lang._Dialogs.InstallMediaPackCompleteSubtitle,
                               null,
                               null,
                               Lang._Misc.OkayBackToMenu,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Success);
        }

        public static Task<ContentDialogResult> Dialog_StopGame()
        {
            return SpawnDialog(Lang._Dialogs.StopGameTitle,
                               Lang._Dialogs.StopGameSubtitle,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        #region Playtime Dialogs

        public static Task<ContentDialogResult> Dialog_ChangePlaytime()
        {
            return SpawnDialog(Lang._Dialogs.ChangePlaytimeTitle,
                               Lang._Dialogs.ChangePlaytimeSubtitle,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_ResetPlaytime()
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap };
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ResetPlaytimeSubtitle });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ResetPlaytimeSubtitle2, FontWeight = FontWeights.Bold });
            texts.Inlines.Add(new Run { Text = Lang._Dialogs.ResetPlaytimeSubtitle3 });

            return SpawnDialog(Lang._Dialogs.ResetPlaytimeTitle,
                               texts,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static async void Dialog_InvalidPlaytime(int elapsedSeconds = 0)
        {
            try
            {
                StackPanel stack = CollapseUIExt.CreateStackPanel();

                stack.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.InvalidPlaytimeSubtitle1, TextWrapping = TextWrapping.Wrap }.WithMargin(0d, 4d),
                                             new TextBlock
                                             {
                                                 Text         = Lang._Dialogs.InvalidPlaytimeSubtitle2,
                                                 TextWrapping = TextWrapping.Wrap
                                             }.WithMargin(0d, 4d),
                                             new TextBlock
                                             {
                                                 Text = string.Format(Lang._HomePage.GamePlaytime_Display,
                                                                      elapsedSeconds / 3600,
                                                                      elapsedSeconds % 3600 / 60),
                                                 FontWeight = FontWeights.Bold
                                             }.WithMargin(0d, 4d).WithHorizontalAlignment(HorizontalAlignment.Center),
                                             new TextBlock
                                                 {
                                                     Text         = Lang._Dialogs.InvalidPlaytimeSubtitle3,
                                                     TextWrapping = TextWrapping.Wrap, FontWeight = FontWeights.Bold
                                                 }.WithMargin(0d, 4d, 0d, -2d)
                                                  .WithHorizontalAlignment(HorizontalAlignment.Center)
                                            );

                await SpawnDialog(Lang._Dialogs.InvalidPlaytimeTitle,
                                  stack,
                                  null,
                                  Lang._Misc.Close,
                                  dialogTheme: ContentDialogTheme.Warning);
            }
            catch
            {
                // ignored
            }
        }

        #endregion

        public static Task<ContentDialogResult> Dialog_MeteredConnectionWarning()
        {
            TextBlock texts = new TextBlock { TextWrapping = TextWrapping.Wrap }
               .AddTextBlockLine(Lang._Dialogs.MeteredConnectionWarningSubtitle);

            return SpawnDialog(Lang._Dialogs.MeteredConnectionWarningTitle,
                               texts,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_ResetKeyboardShortcuts()
        {
            return SpawnDialog(Lang._Dialogs.ResetKbShortcutsTitle,
                               Lang._Dialogs.ResetKbShortcutsSubtitle,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_DbGenerateUid()
        {
            return SpawnDialog(Lang._Dialogs.DbGenerateUid_Title,
                               Lang._Dialogs.DbGenerateUid_Content,
                               null,
                               Lang._Misc.NoCancel,
                               Lang._Misc.Yes,
                               null,
                               ContentDialogButton.Close,
                               ContentDialogTheme.Warning);
        }

        public static Task<ContentDialogResult> Dialog_GenericWarning()
        {
            return SpawnDialog(Lang._UnhandledExceptionPage.UnhandledTitle4,
                               Lang._UnhandledExceptionPage.UnhandledSubtitle4,
                               null,
                               Lang._Misc.Okay,
                               null,
                               null,
                               ContentDialogButton.Primary,
                               ContentDialogTheme.Warning);
        }

        public static async Task<ContentDialogResult> Dialog_ShowUnhandledExceptionMenu()
        {
            Button? copyButton = null;

            try
            {
                string exceptionContent = ErrorSender.ExceptionContent;
                string title            = ErrorSender.ExceptionTitle;
                string subtitle         = ErrorSender.ExceptionSubtitle;

                Grid rootGrid = CollapseUIExt.CreateGrid()
                                             .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                             .WithVerticalAlignment(VerticalAlignment.Stretch)
                                             .WithRows(GridLength.Auto, new GridLength(1, GridUnitType.Star),
                                                       GridLength.Auto)
#if ENABLEUSERFEEDBACK
                                             .WithColumns(GridLength.Auto, new GridLength(1, GridUnitType.Star))
#endif
                                             ;

                _ = rootGrid.AddElementToGridRowColumn(new TextBlock
                {
                    Text         = subtitle,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight   = FontWeights.Medium
                }, 0, 0, 0, 2);
                _ = rootGrid.AddElementToGridRowColumn(new TextBox
                             {
                                 IsReadOnly    = true,
                                 TextWrapping  = TextWrapping.Wrap,
                                 MaxHeight     = 300,
                                 AcceptsReturn = true,
                                 Text          = exceptionContent
                             }, 1, 0, 0, 2).WithMargin(0d, 8d)
                            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                            .WithVerticalAlignment(VerticalAlignment.Stretch);

                copyButton = rootGrid.AddElementToGridRow(CollapseUIExt.CreateButtonWithIcon<Button>(
                                                               Lang._UnhandledExceptionPage!.CopyClipboardBtn1,
                                                               "",
                                                               "FontAwesomeSolid",
                                                               "AccentButtonStyle"
                                                              ).WithHorizontalAlignment(
                                                           #if ENABLEUSERFEEDBACK
                                                               HorizontalAlignment.Left
                                                           #else
                                                               HorizontalAlignment.Center
                                                           #endif
                                                              ), 2)
#if ENABLEUSERFEEDBACK
                                     .WithHorizontalAlignment(HorizontalAlignment.Stretch)
#endif
                                     ;
                copyButton.Click += CopyTextToClipboard;

#if ENABLEUSERFEEDBACK
                var btnText = ErrorSender.SentryErrorId == Guid.Empty
                    ? Lang._Misc.ExceptionFeedbackBtn_Unavailable
                    : Lang._Misc.ExceptionFeedbackBtn;

                Button submitFeedbackButton = rootGrid.AddElementToGridRowColumn(CollapseUIExt.CreateButtonWithIcon<Button>(
                    btnText,
                    "\ue594",
                    "FontAwesomeSolid",
                    "TransparentDefaultButtonStyle",
                    14,
                    10
                    ).WithMargin(8,0,0,0).WithHorizontalAlignment(HorizontalAlignment.Right),
                    2, 1);

                if (ErrorSender.SentryErrorId == Guid.Empty)
                {
                    submitFeedbackButton.IsEnabled = false;
                }
                
                submitFeedbackButton.Click += SubmitFeedbackButton_Click;
                // TODO: Change button content after feedback is submitted
#endif

                ContentDialogResult result = await SpawnDialog(title, rootGrid, null,
                                                               Lang._UnhandledExceptionPage.GoBackPageBtn1,
                                                               null,
                                                               null,
                                                               ContentDialogButton.Close,
                                                               ContentDialogTheme.Error
#if ENABLEUSERFEEDBACK
                                                               ,
                                                               OnLoadedDialog
#endif
                                                               );

                return result;

#if ENABLEUSERFEEDBACK
                void OnLoadedDialog(object? sender, RoutedEventArgs e)
                    => submitFeedbackButton.SetTag(sender);
#endif
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw;
            }
            finally
            {
                if (copyButton != null)
                {
                    copyButton.Click -= CopyTextToClipboard;
                }
            }
        }

#if ENABLEUSERFEEDBACK
        private static async void SubmitFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not Button { Tag: ContentDialog contentDialog })
                {
                    return;
                }

                contentDialog.Hide();

                var userTemplate  = Lang._Misc.ExceptionFeedbackTemplate_User;
                var emailTemplate = Lang._Misc.ExceptionFeedbackTemplate_Email;

                string exceptionContent = $"""
                                          {userTemplate} 
                                          {emailTemplate} 
                                          {Lang._Misc.ExceptionFeedbackTemplate_Message}
                                          ------------------------------------
                                          """;
                string exceptionTitle   = $"{Lang._Misc.ExceptionFeedbackTitle} {ErrorSender.ExceptionTitle}";

                UserFeedbackDialog  feedbackDialog = new UserFeedbackDialog(contentDialog.XamlRoot)
                {
                    Title   = exceptionTitle,
                    IsTitleReadOnly = true,
                    Message = exceptionContent
                };
                UserFeedbackResult? feedbackResult = await feedbackDialog.ShowAsync();
                // TODO: (Optional) Implement generic user feedback pathway (preferably when SentryErrorId is null
                // Using https://paste.mozilla.org/ 
                // API Documentation: https://docs.dpaste.org/api/
                // Though im not sure since user will still need to paste the link to us 🤷
                
                if (feedbackResult is not null)
                {
                    // Parse username and email
                    var msg = feedbackResult.Message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (msg.Length <= 4) return; // Do not send feedback if format is not correct
                    var user     = msg[0].Replace(userTemplate, "", StringComparison.InvariantCulture).Trim();
                    var email    = msg[1].Replace(userTemplate, "", StringComparison.InvariantCulture).Trim();
                    var feedback = msg.Length > 4 ? string.Join("\n", msg.Skip(4)).Trim() : null;
                    
                    if (string.IsNullOrEmpty(user)) user = "none";
                    
                    // Validate email
                    var addr = System.Net.Mail.MailAddress.TryCreate(email, out var address);
                    email = addr ? address!.Address : "user@collapselauncher.com";

                    if (string.IsNullOrEmpty(feedback)) return;

                    var feedbackContent = $"{feedback}\n\nRating: {feedbackResult.Rating}/5";

                    SentryHelper.SendExceptionFeedback(ErrorSender.SentryErrorId, email, user, feedbackContent);
                }

                await Dialog_ShowUnhandledExceptionMenu();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
        }
#endif

        private static async void CopyTextToClipboard(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.CopyStringToClipboard(ErrorSender.ExceptionContent, ILoggerHelper.GetILogger());
                if (sender is not Button { Content: Panel panel } btn)
                {
                    return;
                }

                FontIcon?  fontIcon  = panel.Children[0] as FontIcon;
                TextBlock? textBlock = panel.Children[1] as TextBlock;

                string lastGlyph = fontIcon!.Glyph;
                string lastText  = textBlock!.Text;

                fontIcon.Glyph = "";
                textBlock.Text = Lang._UnhandledExceptionPage.CopyClipboardBtn2;
                btn.IsEnabled  = false;

                await Task.Delay(1000);

                fontIcon.Glyph = lastGlyph;
                textBlock.Text = lastText;
                btn.IsEnabled  = true;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
        }

        #region Shortcut Creator Dialogs

        public static async Task<Tuple<ContentDialogResult, bool>> Dialog_ShortcutCreationConfirm(string path)
        {
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;
            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.ShortcutCreationConfirmSubtitle1 }
                                        .WithMargin(0d, 2d, 0d, 4d)
                                        .WithHorizontalAlignment(HorizontalAlignment.Center));

            TextBlock pathText = new TextBlock { TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0, 4d)
               .WithHorizontalAlignment(HorizontalAlignment.Center);
            pathText.AddTextBlockLine(path, FontWeights.Bold);

            panel.AddElementToStackPanel(
                                         pathText,
                                         new TextBlock
                                         {
                                             Text         = Lang._Dialogs.ShortcutCreationConfirmSubtitle2,
                                             TextWrapping = TextWrapping.WrapWholeWords
                                         }.WithMargin(0d, 4d).WithHorizontalAlignment(HorizontalAlignment.Center));

            CheckBox playOnLoad = panel.AddElementToStackPanel(new CheckBox
            {
                Content = new TextBlock
                    { Text = Lang._Dialogs.ShortcutCreationConfirmCheckBox, TextWrapping = TextWrapping.WrapWholeWords }
            }.WithMargin(0d, 4d, 0d, -8d).WithHorizontalAlignment(HorizontalAlignment.Center));

            ContentDialogResult result = await SpawnDialog(
                                                           Lang._Dialogs.ShortcutCreationConfirmTitle,
                                                           panel,
                                                           null,
                                                           Lang._Misc.Cancel,
                                                           Lang._Misc.YesContinue,
                                                           dialogTheme: ContentDialogTheme.Warning
                                                          );

            return new Tuple<ContentDialogResult, bool>(result, playOnLoad.IsChecked ?? false);
        }

        public static Task<ContentDialogResult> Dialog_ShortcutCreationSuccess(string path, bool play = false)
        {
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;
            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.ShortcutCreationSuccessSubtitle1 }
                                        .WithMargin(0d, 2d, 0d, 4d)
                                        .WithHorizontalAlignment(HorizontalAlignment.Center));

            TextBlock pathText = new TextBlock
            {
                HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.WrapWholeWords,
                Margin              = new Thickness(0, 4, 0, 4)
            };
            pathText.AddTextBlockLine(Lang._Dialogs.ShortcutCreationSuccessSubtitle2);
            pathText.AddTextBlockLine(path, FontWeights.Bold);
            panel.AddElementToStackPanel(pathText);

            if (play)
            {
                panel.AddElementToStackPanel(
                                             new TextBlock
                                             {
                                                 Text       = Lang._Dialogs.ShortcutCreationSuccessSubtitle3,
                                                 FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap
                                             }.WithMargin(0d, 8d, 0d, 4d),
                                             new TextBlock
                                             {
                                                 Text         = Lang._Dialogs.ShortcutCreationSuccessSubtitle4,
                                                 TextWrapping = TextWrapping.WrapWholeWords
                                             }.WithMargin(0d, 2d),
                                             new TextBlock
                                             {
                                                 Text         = Lang._Dialogs.ShortcutCreationSuccessSubtitle5,
                                                 TextWrapping = TextWrapping.WrapWholeWords
                                             }.WithMargin(0d, 2d));
            }

            return SpawnDialog(Lang._Dialogs.ShortcutCreationSuccessTitle,
                               panel,
                               null,
                               Lang._Misc.Close,
                               dialogTheme: ContentDialogTheme.Success);
        }

        public static async Task<Tuple<ContentDialogResult, bool>> Dialog_SteamShortcutCreationConfirm()
        {
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;

            panel.AddElementToStackPanel(
                                         new TextBlock
                                             {
                                                 Text         = Lang._Dialogs.SteamShortcutCreationConfirmSubtitle1,
                                                 TextWrapping = TextWrapping.WrapWholeWords
                                             }.WithHorizontalAlignment(HorizontalAlignment.Center)
                                              .WithMargin(0d, 4d, 0d, 2d),
                                         new TextBlock
                                             {
                                                 Text         = Lang._Dialogs.SteamShortcutCreationConfirmSubtitle2,
                                                 TextWrapping = TextWrapping.WrapWholeWords
                                             }.WithHorizontalAlignment(HorizontalAlignment.Center)
                                              .WithMargin(0d, 2d, 0d, 4d));

            CheckBox playOnLoad = panel.AddElementToStackPanel(new CheckBox
            {
                Content = new TextBlock
                    { Text = Lang._Dialogs.SteamShortcutCreationConfirmCheckBox, TextWrapping = TextWrapping.Wrap }
            }.WithHorizontalAlignment(HorizontalAlignment.Center).WithMargin(0d, 4d, 0d, -8d));

            ContentDialogResult result = await SpawnDialog(
                                                           Lang._Dialogs.SteamShortcutCreationConfirmTitle,
                                                           panel,
                                                           null,
                                                           Lang._Misc.Cancel,
                                                           Lang._Misc.YesContinue,
                                                           dialogTheme: ContentDialogTheme.Warning
                                                          );

            return new Tuple<ContentDialogResult, bool>(result, playOnLoad.IsChecked ?? false);
        }

        public static Task<ContentDialogResult> Dialog_SteamShortcutCreationSuccess(bool play = false)
        {
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 500d;

            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle1, TextWrapping = TextWrapping.WrapWholeWords }.WithHorizontalAlignment(HorizontalAlignment.Center).WithMargin(0d, 2d, 0d, 4d),
                                         new TextBlock
                                         {
                                             Text       = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle2,
                                             FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.WrapWholeWords
                                         }.WithMargin(0d, 8d, 0d, 4d));

            if (play)
            {
                panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle3, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 2d),
                                             new TextBlock
                                             {
                                                 Text         = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle7,
                                                 TextWrapping = TextWrapping.WrapWholeWords
                                             }.WithMargin(0d, 2d, 0d, 2d));
            }

            panel.AddElementToStackPanel(new TextBlock { Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle5, TextWrapping = TextWrapping.WrapWholeWords }.WithMargin(0d, 2d, 0d, 2d),
                                         new TextBlock
                                         {
                                             Text         = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle4,
                                             TextWrapping = TextWrapping.WrapWholeWords
                                         }.WithMargin(0d, 2d, 0d, 2d),
                                         new TextBlock
                                         {
                                             Text         = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle6,
                                             TextWrapping = TextWrapping.WrapWholeWords
                                         }.WithMargin(0d, 2d, 0d, 4d));

            return SpawnDialog(Lang._Dialogs.SteamShortcutCreationSuccessTitle,
                               panel,
                               null,
                               Lang._Misc.Close,
                               dialogTheme: ContentDialogTheme.Success);
        }

        public static Task<ContentDialogResult> Dialog_SteamShortcutCreationFailure()
        {
            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.MaxWidth = 350d;
            panel.AddElementToStackPanel(new TextBlock
            {
                Text = Lang._Dialogs.SteamShortcutCreationFailureSubtitle, TextWrapping = TextWrapping.Wrap
            }.WithMargin(0d, 2d, 0d, 4d));

            return SpawnDialog(Lang._Dialogs.SteamShortcutCreationFailureTitle,
                               panel,
                               null,
                               Lang._Misc.Close,
                               dialogTheme: ContentDialogTheme.Error);
        }

        #endregion

        internal static Task<ContentDialogResult> Dialog_DownloadSettings(GamePresetProperty currentGameProperty)
        {
            ToggleSwitch startAfterInstall = new ToggleSwitch
            {
                IsOn       = currentGameProperty.GameInstall.StartAfterInstall,
                OffContent = Lang._Misc.Disabled,
                OnContent  = Lang._Misc.Enabled
            };
            startAfterInstall.Toggled += (_, _) =>
                                         {
                                             currentGameProperty.GameInstall.StartAfterInstall = startAfterInstall.IsOn;
                                         };

            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.AddElementToStackPanel(
                                         new TextBlock
                                                 { Text = Lang._Dialogs.DownloadSettingsOption1 }
                                            .WithMargin(0d, 0d, 0d, 4d),
                                         startAfterInstall
                                        );

            return SpawnDialog(Lang._Dialogs.DownloadSettingsTitle,
                               panel,
                               null,
                               Lang._Misc.Close);
        }

        public static Task<ContentDialogResult> SpawnDialog(string?    title,
                                                            object?    content,
                                                            UIElement? parentUI      = null,
                                                            string?    closeText     = null,
                                                            string?    primaryText   = null,
                                                            string?    secondaryText = null,
                                                            ContentDialogButton defaultButton =
                                                                ContentDialogButton.Primary,
                                                            ContentDialogTheme dialogTheme =
                                                                ContentDialogTheme.Informational,
                                                            RoutedEventHandler? onLoaded = null)
        {
            _sharedDispatcherQueue ??=
                parentUI?.DispatcherQueue ??
                (WindowUtility.CurrentWindow as MainWindow)?.DispatcherQueue;

            return _sharedDispatcherQueue?.EnqueueAsync(async () =>
                                                        {
                                                            // Create a new instance of dialog
                                                            ContentDialogCollapse dialog =
                                                                new ContentDialogCollapse(dialogTheme)
                                                                {
                                                                    Title               = title,
                                                                    Content             = content,
                                                                    CloseButtonText     = closeText,
                                                                    PrimaryButtonText   = primaryText,
                                                                    SecondaryButtonText = secondaryText,
                                                                    DefaultButton       = defaultButton,
                                                                    Style =
                                                                        CollapseUIExt
                                                                           .GetApplicationResource<
                                                                                Style>("CollapseContentDialogStyle"),
                                                                    XamlRoot =
                                                                        WindowUtility.CurrentWindow is MainWindow
                                                                            mainWindow
                                                                            ? mainWindow.Content.XamlRoot
                                                                            : parentUI?.XamlRoot
                                                                };

                                                            try
                                                            {
                                                                if (onLoaded is not null)
                                                                    dialog.Loaded += onLoaded;

                                                                // Queue and spawn the dialog instance
                                                                return await dialog.QueueAndSpawnDialog();
                                                            }
                                                            finally
                                                            {
                                                                if (onLoaded is not null)
                                                                    dialog.Loaded -= onLoaded;
                                                            }
                                                        }) ?? Task.FromResult(ContentDialogResult.None);
        }

        public static async Task<ContentDialogResult> QueueAndSpawnDialog(this ContentDialog dialog)
        {
            // If a dialog is currently spawned, then wait until the task is completed
            while (_currentSpawnedDialogTask is { Status: AsyncStatus.Started })
            {
                await Task.Delay(200);
            }

            // Set the theme of the content
            if (WindowUtility.CurrentWindow is MainWindow window)
            {
                if (dialog is ContentDialogCollapse contentDialogCollapse)
                {
                    window.ContentDialog = contentDialogCollapse;
                }

                dialog.RequestedTheme = InnerLauncherConfig.IsAppThemeLight ? ElementTheme.Light : ElementTheme.Dark;
            }

            try
            {
                dialog.XamlRoot ??= SharedXamlRoot;
                dialog.Loaded += RecursivelySetDialogCursor;

                // Assign the dialog to the global task
                _currentSpawnedDialogTask = dialog switch
                {
                    ContentDialogCollapse dialogCollapse => dialogCollapse.ShowAsync(),
                    ContentDialogOverlay overlapCollapse => overlapCollapse.ShowAsync(),
                    _ => dialog.ShowAsync()
                };
                // Spawn and await for the result
                ContentDialogResult dialogResult = await _currentSpawnedDialogTask;
                return dialogResult; // Return the result
            }
            finally
            {
                dialog.Loaded -= RecursivelySetDialogCursor;
            }
        }

        private static void RecursivelySetDialogCursor(object sender, RoutedEventArgs args)
        {
            if (sender is not ContentDialog contentDialog)
            {
                return;
            }

            InputSystemCursor cursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            contentDialog.SetAllControlsCursorRecursive(cursor);

            Grid? parent = (contentDialog.Content as UIElement)?.FindAscendant("LayoutRoot", StringComparison.OrdinalIgnoreCase) as Grid;
            Grid? commandButtonGrid = parent?.FindDescendant("CommandSpace", StringComparison.OrdinalIgnoreCase) as Grid;
            commandButtonGrid?.SetAllControlsCursorRecursive(cursor);
        }
    }
}