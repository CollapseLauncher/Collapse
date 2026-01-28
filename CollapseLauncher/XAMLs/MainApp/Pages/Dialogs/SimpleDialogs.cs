using CollapseLauncher.CustomControls;
using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.InstallManager.Base;
using CollapseLauncher.Interfaces;
using CollapseLauncher.XAMLs.Theme.CustomControls.UserFeedbackDialog;
using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.WinRT.WindowsCodec;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
using CollapseUIExt = CollapseLauncher.Extension.UIElementExtensions;
using DispatcherQueueExtensions = CollapseLauncher.Extension.DispatcherQueueExtensions;

// ReSharper disable StringLiteralTypo
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

        public static async Task<(HashSet<string>?, string?)> Dialog_ChooseAudioLanguageChoice(
            Dictionary<string, string> langDict, string defaultLocaleCode = "ja-jp")
        {
            if (!langDict.ContainsKey(defaultLocaleCode))
            {
                throw new
                    KeyNotFoundException($"Default locale code: {defaultLocaleCode} is not found within langDict argument");
            }

            // Naive approach to lookup default index value
            StackPanel parentPanel = CollapseUIExt.CreateStackPanel();

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

            ContentDialogCollapse dialog = new(ContentDialogTheme.Warning)
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

            List<CheckBox> checkboxes = [];

            InputCursor inputCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
            foreach ((string localeId, string language) in langDict)
            {
                Grid checkBoxGrid = CollapseUIExt.CreateGrid()
                                                 .WithColumns(new GridLength(1, GridUnitType.Star),
                                                              new GridLength(1, GridUnitType.Auto))
                                                 .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                                 .WithMargin(0, 0, 0, 8);

                CheckBox checkBox = new()
                {
                    Content                    = checkBoxGrid,
                    HorizontalAlignment        = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment          = VerticalAlignment.Center,
                    VerticalContentAlignment   = VerticalAlignment.Center,
                    Tag                        = localeId
                };
                checkboxes.Add(checkBox);

                TextBlock useAsDefaultText = new()
                {
                    Text = Lang._Misc.UseAsDefault,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    HorizontalTextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Opacity = 0.5,
                    Name = "UseAsDefaultLabel"
                };
                useAsDefaultText.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                Grid iconTextGrid = CollapseUIExt.CreateIconTextGrid(language,
                                                                     "\uf1ab",
                                                                     iconSize: 14,
                                                                     textSize: 14,
                                                                     iconFontFamily: "FontAwesomeSolid")
                                                 .WithOpacity(0.5);
                iconTextGrid.Name = "IconText";
                iconTextGrid.EnableSingleImplicitAnimation(VisualPropertyType.Opacity);
                iconTextGrid.VerticalAlignment = VerticalAlignment.Center;

                checkBoxGrid.AddElementToGridColumn(iconTextGrid, 0);
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
                                Brush>("AudioLanguageSelectionRadioButtonBrush"),
                    Tag = localeId
                }
               .WithHorizontalAlignment(HorizontalAlignment.Stretch)
               .WithVerticalAlignment(VerticalAlignment.Center)
               .WithCursor(inputCursor);

                defaultChoiceRadioButton.Items.Add(radioButton);

                // Check the radio button and check box if the localeId is equal to default
                if (localeId.Equals(defaultLocaleCode, StringComparison.OrdinalIgnoreCase))
                {
                    checkBox.IsChecked                    = true;
                    defaultChoiceRadioButton.SelectedItem = radioButton;

                    iconTextGrid.Opacity = 1;
                }

                radioButton.Checked += (sender, _) =>
                {
                    RadioButton? radioButtonLocal = sender as RadioButton;
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
                    thisIconText.Opacity = 1;
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
                    thisIconText.Opacity = 0.5;
                };

                checkBox.Checked += (sender, _) =>
                {
                    CheckBox thisCheckBox = (CheckBox)sender;
                    radioButton.IsEnabled = true;

                    dialog.IsPrimaryButtonEnabled         =   IsHasAnyChoices();
                    defaultChoiceRadioButton.SelectedItem ??= radioButton;

                    if (thisCheckBox?.FindDescendant("IconText") is Grid thisIconText)
                    {
                        thisIconText.Opacity = 1;
                    }
                };

                checkBox.Unchecked += (sender, _) =>
                {
                    CheckBox thisCheckBox = (CheckBox)sender;
                    radioButton.IsChecked = false;

                    if (thisCheckBox?.FindDescendant("IconText") is Grid thisIconText)
                    {
                        thisIconText.Opacity = 0.5;
                    }

                    bool isHasAnyChoices = IsHasAnyChoices();
                    dialog.IsPrimaryButtonEnabled = isHasAnyChoices;

                    if (defaultChoiceRadioButton.SelectedItem != null || !isHasAnyChoices)
                    {
                        return;
                    }

                    for (int index = 0; index < checkboxes.Count; index++)
                    {
                        CheckBox otherCheckbox = checkboxes[index];
                        if (!(otherCheckbox.IsChecked ?? false))
                        {
                            continue;
                        }

                        defaultChoiceRadioButton.SelectedIndex = index;
                        break;
                    }
                };
            }

            ContentDialogResult dialogResult = await dialog.ShowAsync();
            if (dialogResult == ContentDialogResult.None ||
                defaultChoiceRadioButton.SelectedIndex < 0 ||
                defaultChoiceRadioButton.SelectedItem as RadioButton is not { Tag: string selectedDefaultVoLocaleId })
            {
                return (null, null);
            }

            HashSet<string> selectedVoLocaleIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (string selectedVoLocateId in checkboxes.Where(x => x.IsChecked ?? false)
                                                    .Select(x => x.Tag)
                                                    .OfType<string>())
            {
                selectedVoLocaleIds.Add(selectedVoLocateId);
            }

            return (selectedVoLocaleIds, selectedDefaultVoLocaleId);

            bool IsHasAnyChoices() => checkboxes.Any(x => x.IsChecked ?? false);
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

        private static Task<ContentDialogResult> Dialog_ExistingInstallationBetterLauncher(
            string gamePath, bool isHasOnlyMigrateOption)
        {
            return SpawnDialog(Lang._Dialogs.ExistingInstallBHI3LTitle,
                               string.Format(Lang._Dialogs.ExistingInstallBHI3LSubtitle, gamePath),
                               null,
                               Lang._Misc.Cancel,
                               Lang._Misc.YesMigrateIt,
                               isHasOnlyMigrateOption ? null : Lang._Misc.NoKeepInstallIt);
        }

        private static Task<ContentDialogResult> Dialog_ExistingInstallationSteam(
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
            if (migrateFromLauncherType != MigrateFromLauncherType.Official &&
                migrateFromLauncherType != MigrateFromLauncherType.Plugin)
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
                               $"{Lang._Dialogs.EnsureExitSubtitle} {Lang._Dialogs.EnsureExitSubtitle2}",
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

        public static Task<ContentDialogResult> Dialog_StarRailABTestingWarning()
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

        public static async Task<ContentDialogResult> Dialog_ShowUnhandledExceptionMenu(bool isUserFeedbackSent = false)
        {
            Button? copyButton = null;

            try
            {
                string    exceptionContent = ErrorSender.ExceptionContent;
                ErrorType exceptionType    = ErrorSender.ExceptionType;
                string    title            = ErrorSender.ExceptionTitle;
                string    subtitle         = ErrorSender.ExceptionSubtitle;

                Grid rootGrid = CollapseUIExt.CreateGrid()
                                             .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                                             .WithVerticalAlignment(VerticalAlignment.Stretch)
                                             .WithRows(GridLength.Auto, new GridLength(1, GridUnitType.Star),
                                                       GridLength.Auto)
                                             .WithColumns(GridLength.Auto, new GridLength(1, GridUnitType.Star));

                _ = rootGrid.AddElementToGridRowColumn(CollapseUIExt.Create<TextBlock>(x =>
                                                       {
                                                           x.Text         = subtitle;
                                                           x.TextWrapping = TextWrapping.Wrap;
                                                           x.FontWeight   = FontWeights.Medium;
                                                       }), 0, 0, 0, 2);
                _ = rootGrid.AddElementToGridRowColumn(CollapseUIExt.Create<TextBox>(x =>
                {
                    x.IsReadOnly    = true;
                    x.TextWrapping  = TextWrapping.Wrap;
                    x.MaxHeight     = 300;
                    x.AcceptsReturn = true;
                    x.Text = exceptionContent;
                }), 1, 0, 0, 2)
                            .WithMargin(0d, 8d)
                            .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                            .WithVerticalAlignment(VerticalAlignment.Stretch);

                copyButton = rootGrid.AddElementToGridRow(CollapseUIExt.CreateButtonWithIcon<Button>(
                                                               Lang._UnhandledExceptionPage!.CopyClipboardBtn1,
                                                               "",
                                                               "FontAwesomeSolid",
                                                               "AccentButtonStyle"
                                                              ).WithHorizontalAlignment(
                                                               HorizontalAlignment.Left
                                                              ), 2);
                copyButton.Click += CopyTextToClipboard;

                var btnText = isUserFeedbackSent ? Lang._Misc.ExceptionFeedbackBtn_FeedbackSent :
                    ErrorSender.SentryErrorId == Guid.Empty
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

                CollapseUIExt.RunFunctionFromUIThread(() =>
                {
                    if (ErrorSender.SentryErrorId == Guid.Empty || isUserFeedbackSent)
                    {
                        submitFeedbackButton.IsEnabled = false;
                    }

                    submitFeedbackButton.Click += SubmitFeedbackButton_Click;
                });
                // TODO: Change button content after feedback is submitted

                ContentDialogResult result = await SpawnDialog(title, rootGrid, null,
                                                               Lang._UnhandledExceptionPage.GoBackPageBtn1,
                                                               null,
                                                               null,
                                                               ContentDialogButton.Close,
                                                               exceptionType switch {
                                                                   ErrorType.Warning => ContentDialogTheme.Warning,
                                                                   _ => ContentDialogTheme.Error
                                                               },
                                                               OnLoadedDialog
                                                               );

                return result;

                void OnLoadedDialog(object? sender, RoutedEventArgs e)
                    => submitFeedbackButton.SetTag(sender);
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                throw;
            }
            finally
            {
                CollapseUIExt.RunFunctionFromUIThread(() =>
                {
                    if (copyButton != null)
                    {
                        copyButton.Click -= CopyTextToClipboard;
                    }
                });
            }
        }

        public static async Task<ContentDialogResult> Dialog_RestartLauncher()
        {
            TextBlock content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap
            }.AddTextBlockLine(Lang._Dialogs.LauncherRestartSubtitle1)
             .AddTextBlockNewLine(2)
             .AddTextBlockLine(Lang._Dialogs.LauncherRestartSubtitle2);

            ContentDialogResult result = await SpawnDialog(Lang._Dialogs.LauncherRestartTitle,
                                                           content,
                                                           null,
                                                           Lang._Misc.NoCancel,
                                                           Lang._Misc.YesImReallySure,
                                                           null,
                                                           ContentDialogButton.Primary,
                                                           ContentDialogTheme.Warning);

            return result;
        }

        public static Task<ContentDialogResult> Dialog_SelectCustomBackgroundParallaxPixels()
        {
            NumberBox numberBox = new()
            {
                MinWidth                = 200,
                Maximum                 = 128,
                Minimum                 = 2,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                SmallChange             = 2,
                LargeChange             = 8,
                HorizontalAlignment     = HorizontalAlignment.Center
            };

            numberBox.BindProperty(ImageBackgroundManager.Shared,
                                   nameof(ImageBackgroundManager.Shared.GlobalBackgroundParallaxPixelShift),
                                   NumberBox.ValueProperty,
                                   BindingMode.TwoWay);


            return SpawnDialog(string.Format(Lang._Dialogs.BgContextMenu_ParallaxPixelShiftCustomDialogTitle,
                                             numberBox.Minimum,
                                             numberBox.Maximum),
                               numberBox,
                               null,
                               Lang._Misc.Okay);
        }

        // ReSharper disable once AsyncVoidMethod
        private static async void SubmitFeedbackButton_Click(object sender, RoutedEventArgs e)
        {
            bool isFeedbackSent = false;
            if (sender is not Button { Tag: ContentDialog contentDialog })
            {
                return;
            }

            try
            {
                contentDialog.Hide();

                string exceptionContent = UserFeedbackTemplate.FeedbackTemplate;
                string exceptionTitle   = $"{Lang._Misc.ExceptionFeedbackTitle} {ErrorSender.ExceptionTitle}";

                UserFeedbackDialog  feedbackDialog = new UserFeedbackDialog(contentDialog.XamlRoot)
                {
                    Title   = exceptionTitle,
                    IsTitleReadOnly = true,
                    Message = exceptionContent
                };
                UserFeedbackResult? feedbackResult = await feedbackDialog.ShowAsync();

                if (feedbackResult is null)
                {
                    return;
                }
                
                string? feedbackLoadingTitle = Lang._Misc.Feedback;

                LoadingMessageHelper.Initialize();
                LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSending);
                LoadingMessageHelper.ShowLoadingFrame();
                
                UserFeedbackTemplate.UserFeedbackTemplateResult? parsedFeedback = UserFeedbackTemplate.ParseTemplate(feedbackResult);
                if (parsedFeedback == null)
                {
                    Logger.LogWriteLine("Failed to parse feedback template! Not sending feedback", LogType.Error, true);
                    
                    LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSendFailure);
                    await Task.Delay(1000);
                    LoadingMessageHelper.HideLoadingFrame();
                }
                else
                {
                    if (SentryHelper.SendExceptionFeedback(ErrorSender.SentryErrorId, parsedFeedback.Email,
                                                           parsedFeedback.User, parsedFeedback.Message))
                    {
                        // Hide the loading message after 200ms
                        await Task.Delay(500);
                        LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSent);
                        await Task.Delay(1000);
                        LoadingMessageHelper.HideLoadingFrame();
                        isFeedbackSent = true;
                    }
                    else
                    {
                        await Task.Delay(250);
                        LoadingMessageHelper.SetMessage(feedbackLoadingTitle, Lang._Misc.FeedbackSendFailure);
                        await Task.Delay(1000);
                        LoadingMessageHelper.HideLoadingFrame();
                    }
                }
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            }
            finally
            {
                await Dialog_ShowUnhandledExceptionMenu(isFeedbackSent);
            }
        }

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

        #region Background Image Dialogs

        public static async Task Dialog_SpawnMediaExtensionNotSupportedDialog(string filePath)
        {
            TextBlock textBlock = CollapseUIExt.CreateTextBlock()
                                               .AddTextBlockLine(Lang._Dialogs.Media_ExtNotSupported1)
                                               .AddTextBlockNewLine(2)
                                               .AddTextBlockLine(string.Format(Lang._Dialogs.Media_ExtNotSupported2, filePath));
            await SpawnDialog(Lang._Dialogs.Media_ExtNotSupportedTitle,
                              textBlock,
                              null,
                              Lang._Misc.OkaySad,
                              defaultButton: ContentDialogButton.Close,
                              dialogTheme: ContentDialogTheme.Error);
        }

        public static async Task Dialog_SpawnImageNotSupportedDialog(string filePath)
        {
            TextBlock textBlock = CollapseUIExt.CreateTextBlock();
            textBlock.AddTextBlockLine(Lang._Dialogs.Media_ImageWICNotSupported1)
                     .AddTextBlockNewLine(2)
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_ExtNotSupported2, filePath));
            await SpawnDialog(Lang._Dialogs.Media_ImageWICNotSupportedTitle,
                              textBlock,
                              null,
                              Lang._Misc.OkaySad,
                              defaultButton: ContentDialogButton.Close,
                              dialogTheme: ContentDialogTheme.Error);
        }

        public static async Task<bool> Dialog_SpawnVideoNotSupportedDialog(
            string filePath,
            bool   canPlayVideo,
            bool   canPlayAudio,
            Guid   videoCodecGuid,
            Guid   audioCodecGuid)
        {
            WindowsCodecHelper.TryGetFourCCString(in videoCodecGuid,
                                                  out string? videoCodecString);

            videoCodecString ??= Lang._Dialogs.Media_VideoMFNotSupportedFormatTypeUnknown;

            string useInternalMfLocale = Lang._Dialogs.Media_VideoMFNotSupportedInstallMFCodecsBtn;
            string useFfmpegLocale     = Lang._Dialogs.Media_VideoMFNotSupportedInstallFFmpegBtn;

            TextBlock textBlock = CollapseUIExt.CreateTextBlock();

            textBlock.AddTextBlockLine(Lang._Dialogs.Media_VideoMFNotSupported1)
                     .AddTextBlockNewLine(2)
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_ExtNotSupported2, filePath), size: 11, weight: FontWeights.Bold)
                     .AddTextBlockNewLine()
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_VideoMFNotSupported2, videoCodecString), size: 11, weight: FontWeights.Bold)
                     .AddTextBlockNewLine()
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_VideoMFNotSupported3, videoCodecGuid), size: 11, weight: FontWeights.Bold)
                     .AddTextBlockNewLine()
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_VideoMFNotSupported4, audioCodecGuid), size: 11, weight: FontWeights.Bold)
                     .AddTextBlockNewLine()
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_VideoMFNotSupported5, canPlayVideo, canPlayAudio), size: 11, weight: FontWeights.Bold)
                     .AddTextBlockNewLine(2)
                     .AddTextBlockLine(string.Format(Lang._Dialogs.Media_VideoMFNotSupported6, useInternalMfLocale, useFfmpegLocale))
                     .AddTextBlockNewLine(2)
                     .AddTextBlockLine(Lang._Dialogs.Media_VideoMFNotSupported7, size: 11, weight: FontWeights.Bold)
                     .AddTextBlockNewLine()
                     .AddTextBlockLine(Lang._Dialogs.Media_VideoMFNotSupported8, size: 11);

            StackPanel panel = CollapseUIExt.CreateStackPanel();
            panel.AddElementToStackPanel(textBlock);

            Button buttonIconCopyDetails = CollapseUIExt.CreateButtonWithIcon<Button>(Lang._Dialogs.Media_VideoMFNotSupportedCopyDetailsBtn, textSize: 12d, textWeight: FontWeights.Bold)
                                                        .WithHorizontalAlignment(HorizontalAlignment.Left)
                                                        .WithMargin(0, 16, 0, 0);
            panel.AddElementToStackPanel(buttonIconCopyDetails);
            buttonIconCopyDetails.Click += ButtonIconCopyDetailsOnClick;
            buttonIconCopyDetails.Unloaded += ButtonIconCopyDetailsOnUnloaded;

            ContentDialogResult result = await SpawnDialog(Lang._Dialogs.Media_VideoMFNotSupportedTitle,
                                                           panel,
                                                           null,
                                                           Lang._Misc.Close,
                                                           useFfmpegLocale,
                                                           useInternalMfLocale,
                                                           defaultButton: ContentDialogButton.Primary,
                                                           dialogTheme: ContentDialogTheme.Error);

            switch (result)
            {
                case ContentDialogResult.Primary:
                    return await Dialog_SpawnFfmpegInstallDialog();
                case ContentDialogResult.Secondary:
                    return await Dialog_SpawnMediaFoundationCodecInstallDialog();
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
                buttonIconCopyDetails.Click -= ButtonIconCopyDetailsOnClick;
                buttonIconCopyDetails.Unloaded -= ButtonIconCopyDetailsOnUnloaded;
            }
        }

        internal static async Task<bool> Dialog_SpawnMediaFoundationCodecInstallDialog()
        {
            string dialogConfirmInstall = Lang._Dialogs.Media_VideoMFCodecPrepareInstallBtn;

        StartOver:
            ContentDialogResult result =
                await SpawnDialog(Lang._Dialogs.Media_VideoMFCodecPrepareTitle,
                                  CollapseUIExt.CreateTextBlock()
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecPrepare1)
                                               .AddTextBlockNewLine(2)
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecPrepare2)
                                               .AddTextBlockNewLine()
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecPrepare3, FontWeights.Bold)
                                               .AddTextBlockNewLine()
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecPrepare4, FontWeights.Bold)
                                               .AddTextBlockNewLine(2)
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecPrepare5)
                                               .AddTextBlockLine(dialogConfirmInstall, FontWeights.Bold)
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecPrepare6),
                                  primaryText: dialogConfirmInstall,
                                  closeText: Lang._Misc.Cancel,
                                  defaultButton: ContentDialogButton.Primary,
                                  dialogTheme: ContentDialogTheme.Warning);

            if (result == ContentDialogResult.None)
            {
                return false;
            }

#pragma warning disable IDE0063
            using (CancellationTokenSourceWrapper tokenSource = new())
#pragma warning restore IDE0063
            {
                WindowsCodecInstaller codecInstaller = new(Directory.GetCurrentDirectory(), tokenSource);
                if (!await Dialog_SpawnCodecDownloadInstallDialog(Lang._Dialogs.Media_VideoMFCodecInstallingTitle,
                                                                  codecInstaller, tokenSource))
                {
                    goto StartOver;
                }
            }

            await SpawnDialog(Lang._Dialogs.Media_VideoMFCodecInstalledTitle,
                              CollapseUIExt.CreateTextBlock()
                                           .AddTextBlockLine(Lang._Dialogs.Media_VideoMFCodecInstalled1),
                              closeText: Lang._Misc.OkayHappy,
                              dialogTheme: ContentDialogTheme.Success);

            return true;
        }

        internal static async Task<bool> Dialog_SpawnFfmpegInstallDialog()
        {
            string dialogConfirmInstall        = Lang._Dialogs.Media_VideoFFmpegCodecPrepareInstallBtn;
            string dialogLocateExistingInstall = Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateBtn;

        StartOver:
            ContentDialogResult result =
            await SpawnDialog(Lang._Dialogs.Media_VideoFFmpegCodecPrepareTitle,
                              CollapseUIExt.CreateTextBlock()
                                           .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepare1)
                                           .AddTextBlockNewLine(2)
                                           .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepare2)
                                           .AddTextBlockLine(dialogConfirmInstall, FontWeights.Bold)
                                           .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepare3)
                                           .AddTextBlockLine(dialogLocateExistingInstall, FontWeights.Bold)
                                           .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepare4),
                                  primaryText: dialogConfirmInstall,
                                  secondaryText: dialogLocateExistingInstall,
                                  closeText: Lang._Misc.Cancel,
                                  defaultButton: ContentDialogButton.Primary,
                                  dialogTheme: ContentDialogTheme.Warning);

            if (result == ContentDialogResult.None)
            {
                return false;
            }

            string? foundFfmpegDir = null;
            if (result == ContentDialogResult.Secondary)
            {
                string ffmpegDir = await FileDialogNative.GetFolderPicker(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateDialog);
                if (string.IsNullOrEmpty(ffmpegDir))
                {
                    goto StartOver;
                }

                foundFfmpegDir = ImageBackgroundManager.FindFfmpegInstallFolder(ffmpegDir);
                if (string.IsNullOrEmpty(foundFfmpegDir))
                {
                    await SpawnDialog(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateFailedTitle,
                                      CollapseUIExt.CreateTextBlock()
                                                   .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateFailed1)
                                                   .AddTextBlockNewLine(2)
                                                   .AddTextBlockLine(ffmpegDir, FontWeights.Bold, 12)
                                                   .AddTextBlockNewLine(2)
                                                   .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateFailed2, FontWeights.Bold, 12)
                                                   .AddTextBlockNewLine()
                                                   .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateFailed3, true, size: 12)
                                                   .AddTextBlockLine(string.Join(", ", ImageBackgroundManager.GetFfmpegRequiredDllFilenames()), FontWeights.Bold, 12),
                                      closeText: Lang._Misc.Okay,
                                      dialogTheme: ContentDialogTheme.Error);
                    goto StartOver;
                }

                await SpawnDialog(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateSuccessTitle,
                                  CollapseUIExt.CreateTextBlock()
                                               .AddTextBlockLine(Lang._Dialogs.Media_VideoFFmpegCodecPrepareLocateSuccess1)
                                               .AddTextBlockNewLine(2)
                                               .AddTextBlockLine(foundFfmpegDir, FontWeights.Bold, 12),
                                  closeText: Lang._Misc.OkayHappy,
                                  dialogTheme: ContentDialogTheme.Success);
            }


            if (!await Dialog_SpawnLicenseAgreementDialog(Path.Combine(Directory.GetCurrentDirectory(), @"Assets\Licenses\FFmpeg"),
                                                          Path.Combine(Directory.GetCurrentDirectory(), @"Assets\Licenses\FFmpegInteropX")))
            {
                goto StartOver;
            }

            if (!string.IsNullOrEmpty(foundFfmpegDir))
            {
                if (!ImageBackgroundManager.TryLinkFfmpegLibrary(foundFfmpegDir,
                                                                 Directory.GetCurrentDirectory(),
                                                                 out Exception? ex))
                {
                    ErrorSender.SendException(ex);
                    SentryHelper.ExceptionHandler(ex);
                    Logger.LogWriteLine($"An error has occurred while trying to link FFmpeg library: {ex}",
                                        LogType.Error,
                                        true);
                    return false;
                }
            }

            if (result == ContentDialogResult.Primary)
            {
                foundFfmpegDir = null;

                using CancellationTokenSourceWrapper tokenSource    = new();
                FFmpegCodecInstaller                 codecInstaller = new(Directory.GetCurrentDirectory(), tokenSource);
                if (!await Dialog_SpawnCodecDownloadInstallDialog(Lang._Dialogs.Media_VideoFFmpegCodecInstallingTitle, codecInstaller, tokenSource))
                {
                    goto StartOver;
                }
            }

            ImageBackgroundManager.CustomFfmpegPath = foundFfmpegDir;
            return true;
        }

        internal static async Task<bool> Dialog_SpawnCodecDownloadInstallDialog(
            string                         title,
            ICodecExtensionInstaller       installer,
            CancellationTokenSourceWrapper tokenSource)
        {
            ProgressBase codecInstaller = (installer as ProgressBase)!;

            Grid grid = CollapseUIExt.CreateGrid()
                                     .WithColumns(default,
                                                  new GridLength(1, GridUnitType.Auto))
                                     .WithRows(default,
                                               default,
                                               default)
                                     .WithHorizontalAlignment(HorizontalAlignment.Stretch);

            CollapseUIExt.RunFunctionFromUIThread(CreateElement);

            ContentDialogCollapse dialog = CollapseUIExt
               .Create<ContentDialogCollapse>(x =>
                                              {
                                                  x.Title                  = title;
                                                  x.IsPrimaryButtonEnabled = false;
                                                  x.CloseButtonText        = Lang._Misc.Cancel;
                                                  x.DefaultButton          = ContentDialogButton.Close;
                                                  x.Content                = grid;
                                              });

            TaskCompletionSource tcsDialog = new();
            _ = StartInstaller(tcsDialog, tokenSource.Token);

            // ReSharper disable once AsyncVoidLambda
            CollapseUIExt.RunFunctionFromUIThread(async () =>
                                                  {
                                                      try
                                                      {
                                                          await dialog.QueueAndSpawnDialog();
                                                          // ReSharper disable once AccessToDisposedClosure
                                                          await tokenSource.CancelAsync();
                                                      }
                                                      catch
                                                      {
                                                          // ignored
                                                      }
                                                  });

            try
            {
                await tcsDialog.Task;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (Exception e)
            {
                SentryHelper.ExceptionHandler(e);
                ErrorSender.SendException(e);
                Logger.LogWriteLine($"An error occurred while trying to install FFmpeg package: {e}",
                                    LogType.Error,
                                    true);
                return false;
            }

            return true;

            void CreateElement()
            {
                // -- Installer Status
                TextBlock installerStatus = new()
                {
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Width        = 480d,
                    Margin       = new Thickness(0, 0, 0, 8)
                };
                installerStatus.BindProperty(codecInstaller.Status,
                                             nameof(codecInstaller.Status.ActivityAll),
                                             TextBlock.TextProperty,
                                             BindingMode.OneWay);
                grid.Children.Add(installerStatus);

                Grid.SetRow(installerStatus, 0);
                Grid.SetColumn(installerStatus, 0);
                Grid.SetColumnSpan(installerStatus, 2);

                // -- Installer Progress Bar
                ProgressBar installerProgressBar = new()
                {
                    Maximum = 100d,
                    Width   = 480d
                };
                installerProgressBar.BindProperty(codecInstaller.Progress,
                                                  nameof(codecInstaller.Progress.ProgressAllPercentage),
                                                  RangeBase.ValueProperty,
                                                  BindingMode.OneWay);
                grid.Children.Add(installerProgressBar);

                Grid.SetRow(installerProgressBar, 2);
                Grid.SetColumn(installerProgressBar, 0);
                Grid.SetColumnSpan(installerProgressBar, 2);

                // -- Installer Text Left Indicator
                TextBlock textBlockLeftIndicator = new()
                {
                    Text                    = "- / -",
                    HorizontalTextAlignment = TextAlignment.Left,
                    HorizontalAlignment     = HorizontalAlignment.Left,
                    FontSize                = 12,
                    FontWeight              = FontWeights.Bold,
                    Margin                  = new Thickness(0, 0, 0, 8)
                };
                codecInstaller.ProgressChanged += LeftInstallerOnProgressChanged;
                grid.Children.Add(textBlockLeftIndicator);

                Grid.SetRow(textBlockLeftIndicator, 1);
                Grid.SetColumn(textBlockLeftIndicator, 0);

                // -- Installer Text Right Indicator
                TextBlock textBlockRightIndicator = new()
                {
                    Text                    = "(N/A) -%",
                    HorizontalTextAlignment = TextAlignment.Right,
                    HorizontalAlignment     = HorizontalAlignment.Right,
                    FontSize                = 12,
                    FontWeight              = FontWeights.Bold,
                    Margin                  = new Thickness(0, 0, 0, 8)
                };
                codecInstaller.ProgressChanged += RightInstallerOnProgressChanged;
                grid.Children.Add(textBlockRightIndicator);

                Grid.SetRow(textBlockRightIndicator, 1);
                Grid.SetColumn(textBlockRightIndicator, 1);

                return;

                void LeftInstallerOnProgressChanged(object? sender, TotalPerFileProgress e)
                {
                    CollapseUIExt.RunFunctionFromUIThread(() =>
                                                          {
                                                              textBlockLeftIndicator.Text = $"{SummarizeSizeSimple(e.ProgressAllSizeCurrent)} / {SummarizeSizeSimple(e.ProgressAllSizeTotal)}";
                                                          });
                }

                void RightInstallerOnProgressChanged(object? sender, TotalPerFileProgress e)
                {
                    CollapseUIExt.RunFunctionFromUIThread(() =>
                                                          {
                                                              textBlockRightIndicator.Text = $"({string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(e.ProgressAllSpeed))}) {e.ProgressAllPercentage}%";
                                                          });
                }
            }

            async Task StartInstaller(TaskCompletionSource tcs, CancellationToken token = default)
            {
                try
                {
                    await installer.Start();
                    tcs.SetResult();
                }
                catch (OperationCanceledException)
                {
                    tcs.SetCanceled(token);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
                finally
                {
                    HideDialog();
                }
            }

            void HideDialog() => DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(dialog.Hide);
        }

        internal static async Task<bool> Dialog_SpawnLicenseAgreementDialog(params string[] licenseDirsToView)
        {
            string[] availableLicenseFiles = licenseDirsToView.SelectMany(EnumerateLicenses)
                                                              .ToArray();

            foreach ((int index, string licenseFile) in availableLicenseFiles.Index())
            {
                string directory        = Path.GetDirectoryName(licenseFile)!;
                string ownerName        = Path.GetFileName(directory);
                string licenseFileName  = Path.GetFileNameWithoutExtension(licenseFile);
                string licenseName      = licenseFileName.GetSplit(1, '_').ToString();
                string homepageFilePath = Path.Combine(directory, "Homepage");

                StackPanel panel = CollapseUIExt.CreateStackPanel();

                TextBlock preambleTitle = CollapseUIExt.Create<TextBlock>(x =>
                {
                    x.Text                    = Lang._Dialogs.Agreement_ThirdPartyAgreementPreambleTitle;
                    x.FontSize                = 28;
                    x.HorizontalAlignment     = HorizontalAlignment.Center;
                    x.HorizontalTextAlignment = TextAlignment.Center;
                    x.Margin                  = new Thickness(0, 0, 0, 16);
                });
                panel.AddElementToStackPanel(preambleTitle);
                TextBlock preambleText = CollapseUIExt.CreateTextBlock(fontSize: 12, fontFamilyName: "Consolas", textAlignment: TextAlignment.Center)
                                                      .WithMargin(0, 0, 0, 16)
                                                      .WithHorizontalAlignment(HorizontalAlignment.Center);
                preambleText
                   .AddTextBlockLine(Lang._Dialogs.Agreement_ThirdPartyAgreementPreamble1, true, size: 12)
                   .AddTextBlockLine(Lang._Dialogs.Agreement_ThirdPartyAgreementPreamble2, size: 12)
                   .AddTextBlockNewLine(2)
                   .AddTextBlockLine(Lang._Dialogs.Agreement_ThirdPartyAgreementPreamble3, size: 12)
                   .AddTextBlockLine(Lang._Misc.IAcceptAgreement,                          FontWeights.Bold, size: 12)
                   .AddTextBlockLine(Lang._Dialogs.Agreement_ThirdPartyAgreementPreamble4, size: 12)
                   .AddTextBlockLine(Lang._Misc.IDoNotAcceptAgreement,                     FontWeights.Bold, size: 12)
                   .AddTextBlockLine(Lang._Dialogs.Agreement_ThirdPartyAgreementPreamble5, size: 12);
                panel.AddElementToStackPanel(preambleText);

                string dialogTitle =
                    $"[{index + 1}/{availableLicenseFiles.Length}] {Lang._OOBEAgreementMenu.AgreementTitle} {ownerName}";

                TextBlock contentTitle = CollapseUIExt.Create<TextBlock>(x =>
                {
                    x.Text                    = ownerName;
                    x.FontSize                = 28;
                    x.HorizontalAlignment     = HorizontalAlignment.Center;
                    x.HorizontalTextAlignment = TextAlignment.Center;
                });
                panel.AddElementToStackPanel(contentTitle);

                TextBlock contentSubtitle = CollapseUIExt.Create<TextBlock>(x =>
                {
                    x.Text                    = licenseName;
                    x.FontSize                = 18;
                    x.FontWeight              = FontWeights.Bold;
                    x.HorizontalAlignment     = HorizontalAlignment.Center;
                    x.HorizontalTextAlignment = TextAlignment.Center;
                });
                panel.AddElementToStackPanel(contentSubtitle);

                if (File.Exists(homepageFilePath))
                {
                    string[] homepageUrls = await File.ReadAllLinesAsync(homepageFilePath);
                    foreach (string homepageUrl in homepageUrls.Where(x => !string.IsNullOrEmpty(x) && !string.IsNullOrWhiteSpace(x)))
                    {
                        TextBlock homepageTextBlock = CollapseUIExt.Create<TextBlock>(x =>
                        {
                            CollapseUIExt.RunFunctionFromUIThread(() =>
                            {
                                Hyperlink homepageHyperlink = new();
                                homepageHyperlink.Click += HomepageHyperlinkOnClick;

                                homepageHyperlink.Inlines.Add(new Run { Text = homepageUrl });
                                x.Inlines.Add(homepageHyperlink);
                                x.HorizontalAlignment = HorizontalAlignment.Center;
                                x.HorizontalTextAlignment = TextAlignment.Center;

                                return;

                                void HomepageHyperlinkOnClick(Hyperlink sender, HyperlinkClickEventArgs args)
                                {
                                    using Process? proc = Process.Start(new ProcessStartInfo
                                    {
                                        FileName        = homepageUrl,
                                        UseShellExecute = true
                                    });
                                }
                            });
                        });

                        panel.AddElementToStackPanel(homepageTextBlock);
                    }
                }

                string licenseContent = await File.ReadAllTextAsync(licenseFile);
                TextBlock licenseTextBox = CollapseUIExt.Create<TextBlock>(x =>
                {
                    x.Text                    = licenseContent;
                    x.FontFamily              = new FontFamily("Consolas");
                    x.Margin                  = new Thickness(0, 16, 0, 0);
                    x.HorizontalAlignment     = HorizontalAlignment.Center;
                    x.HorizontalTextAlignment = TextAlignment.Left;
                    x.FontSize                = 12;
                });
                panel.AddElementToStackPanel(licenseTextBox);

                ContentDialogResult result = await
                    SpawnDialog(dialogTitle,
                                panel,
                                closeText: Lang._Misc.IDoNotAcceptAgreement,
                                primaryText: Lang._Misc.IAcceptAgreement,
                                defaultButton: ContentDialogButton.Primary,
                                dialogTheme: ContentDialogTheme.Informational);
                if (result == ContentDialogResult.None)
                {
                    return false;
                }
            }

            return true;

            static IEnumerable<string> EnumerateLicenses(string directoryPath)
            {
                foreach (string licensePath in Directory
                                              .EnumerateFiles(directoryPath,
                                                              "LICENSE_*.txt",
                                                              SearchOption.TopDirectoryOnly))
                {
                    yield return licensePath;
                }
            }
        }

        #endregion

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
                                         }.WithMargin(0d, 2d, 0d, 1d),
                                         new TextBlock
                                         {
                                             Text = Lang._Dialogs.SteamShortcutCreationSuccessSubtitle8,
                                             TextWrapping = TextWrapping.WrapWholeWords
                                         }.WithMargin(0d, 1d, 0d, 4d));

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
            return SpawnDialog(Lang._Dialogs.DownloadSettingsTitle,
                               new DownloadSettings(currentGameProperty),
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
                                                            XamlRoot? xamlRoot =
                                                                WindowUtility.CurrentWindow is MainWindow
                                                                    mainWindow
                                                                    ? mainWindow.Content.XamlRoot
                                                                    : parentUI?.XamlRoot;

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
                                                                    XamlRoot = xamlRoot
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
            // Wait until the SharedXamlRoot OR dialog.XamlRoot is not null
            // Prevent crash on startup where the UI is not ready yet
            var timeout = TimeSpan.FromSeconds(10);
            var actStart = DateTime.Now;
            while (SharedXamlRoot is null && dialog.XamlRoot is null && DateTime.Now - actStart < timeout)
            {
                await Task.Delay(200);
            }

            if (SharedXamlRoot is null && dialog.XamlRoot is null)
            {
                var msg = $"[SimpleDialogs::QueueAndSpawnDialog] Failed to spawn dialog {dialog.Title} " +
                          $"due to XamlRoot is null after waiting for {timeout.TotalSeconds} seconds";
                Logger.LogWriteLine(msg, LogType.Warning, true);
                SentryHelper.ExceptionHandler(new TimeoutException(msg));
                return ContentDialogResult.None;
            }
            
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