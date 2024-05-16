﻿using CollapseLauncher.DiscordPresence;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Windows.UI;
using static Hi3Helper.InvokeProp;
using static Hi3Helper.Shared.Region.LauncherConfig;

#nullable enable
namespace CollapseLauncher
{
    public static class InnerLauncherConfig
    {
        public enum AppReleaseChannel
        {
            Stable,
            Preview
        }

        public enum AppMode
        {
            Launcher,
            Updater,
            ElevateUpdater,
            InvokerMigrate,
            InvokerTakeOwnership,
            InvokerMoveSteam,
            Hi3CacheUpdater,
            OOBEState,
            StartOnTray
        }

        public static AppMode              m_appMode;
        public static Arguments            m_arguments = new();
        public static bool                 m_isWindows11;
        public static IntPtr               m_oldWndProc;
        public static Delegate?            m_newWndProcDelegate;
        public static HandlerRoutine?      m_consoleCtrlHandler;
        public static MainPage?            m_mainPage;
        public static HomePage?            m_homePage;
        public static bool                 m_windowSupportCustomTitle = false;
        public static Size                 m_actualMainFrameSize;
        public static string?              m_appCurrentFrameName;
        public static NotificationPush?    NotificationData;
        public static bool                 IsCustomBG            = false;
        public static bool                 IsSkippingUpdateCheck = false;
        public static AppThemeMode         CurrentAppTheme;
    #if !DISABLEDISCORD
    #pragma warning disable CA2211
        public static DiscordPresenceManager? AppDiscordPresence;
    #pragma warning restore CA2211
    #endif
        public static bool IsAppThemeLight =>
            CurrentAppTheme switch
            {
                AppThemeMode.Dark => false,
                AppThemeMode.Light => true,
                _ => !ShouldAppsUseDarkMode()
            };

        public static string? GetComboBoxGameRegionValue(object obj)
        {
            StackPanel value     = (StackPanel)obj;
            TextBlock? textBlock = value.Children.FirstOrDefault() as TextBlock;
            return textBlock?.Text;
        }

        public static List<StackPanel> BuildGameTitleListUI()
        {
            List<StackPanel> list = [];
            foreach (string? title in LauncherMetadataHelper.GetGameNameCollection()!)
            {
                StackPanel panel              = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                TextBlock  gameTitleTextBlock = panel.AddElementToStackPanel(new TextBlock { Text = title });
                TextBlock? gameTitleTranslatedTextBlock =
                    GetGameTitleRegionTranslationTextBlock(ref gameTitleTextBlock, Locale.Lang._GameClientTitles);

                if (gameTitleTranslatedTextBlock != null)
                {
                    panel.AddElementToStackPanel(gameTitleTranslatedTextBlock);
                }

                list.Add(panel);
            }

            return list;
        }

        public static List<StackPanel> BuildGameRegionListUI(string?        gameCategory,
                                                             List<string?>? gameCategoryList = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(gameCategory);
            gameCategoryList ??= LauncherMetadataHelper.GetGameRegionCollection(gameCategory);
            List<StackPanel> list = [];
            if (gameCategoryList == null)
            {
                return list;
            }

            foreach (string? region in gameCategoryList)
            {
                if (region == null)
                    throw new NullReferenceException($"Region name is empty!");

                PresetConfig? config              = LauncherMetadataHelper.LauncherMetadataConfig?[gameCategory]?[region];
                StackPanel    panel               = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                TextBlock     gameRegionTextBlock = panel.AddElementToStackPanel(new TextBlock { Text = region });
                TextBlock? gameRegionTranslatedTextBlock =
                    GetGameTitleRegionTranslationTextBlock(ref gameRegionTextBlock, Locale.Lang._GameClientRegions);

                panel.AddElementToStackPanel(gameRegionTranslatedTextBlock);

                if (config != null && config.Channel != GameChannel.Stable)
                {
                    Grid expTag = UIElementExtensions.CreateGrid()
                                                     .WithPadding(4d, 0d)
                                                     .WithMargin(4d, 3d, 0d, 0d)
                                                     .WithCornerRadius(4d)
                                                     .WithHorizontalAlignment(HorizontalAlignment.Left)
                                                     .WithVerticalAlignment(VerticalAlignment.Stretch)
                                                     .WithBackground(new SolidColorBrush(!IsAppThemeLight
                                                                         ? Color.FromArgb(255, 255, 255,
                                                                             255)
                                                                         : Color.FromArgb(255, 40, 40,
                                                                             40)));

                    expTag.AddElementToGridRow(new TextBlock
                    {
                        Text                = GetGameChannelLabel(config.Channel),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                        FontSize            = 10,
                        Margin              = new Thickness(0, -2, 0, 0),
                        Foreground = new SolidColorBrush(!IsAppThemeLight
                                                             ? Color.FromArgb(255, 40,  40,  40)
                                                             : Color.FromArgb(255, 255, 255, 255)),
                        FontWeight = FontWeights.Bold
                    }, 0);
                    panel.AddElementToStackPanel(expTag);
                }

                list.Add(panel);
            }

            return list;
        }

        public static TextBlock? GetGameTitleRegionTranslationTextBlock(ref TextBlock originalTextBlock,
                                                                        Dictionary<string, string?>?
                                                                            translationDictionary)
        {
            // Get the original region string
            string? originalString = originalTextBlock.Text;

            // If the originalTextBlock or regionString is null or empty, then return null
            if (string.IsNullOrEmpty(originalString))
            {
                return null;
            }

            // Check if the translation is available. If not, then return null
            if (translationDictionary != null && (translationDictionary.Count == 0
                                                  || !translationDictionary.ContainsKey(originalString)))
            {
                return null;
            }

            // If it exists in the translation, then return the TextBlock and set the original
            // TextBlock visibility to collapse
            string? translatedString = translationDictionary?[originalString];
            originalTextBlock.Visibility = Visibility.Collapsed;
            TextBlock translatedTextBlock = new TextBlock { Text = translatedString };
            if (translatedString is not { Length: > 15 })
            {
                return translatedTextBlock;
            }

            translatedTextBlock.TextWrapping = TextWrapping.Wrap;
            translatedTextBlock.TextTrimming = TextTrimming.WordEllipsis;

            return translatedTextBlock;
        }

        public static string? GetGameTitleRegionTranslationString(string?                      originalString,
                                                                  Dictionary<string, string?>? translationDictionary)
        {
            // Check if the region translation is available. If not, then return null
            if (originalString != null && translationDictionary != null && (translationDictionary.Count == 0
                                                                            || !translationDictionary
                                                                               .ContainsKey(originalString)))
            {
                return originalString;
            }

            // If the key exist, then return the translated string
            return originalString != null ? translationDictionary?[originalString] : null;
        }

        private static string GetGameChannelLabel(GameChannel channel)
        {
            return channel switch
                   {
                       GameChannel.Beta => "BETA",
                       GameChannel.DevRelease => "DEV",
                       _ => "PREVIEW"
                   };
        }

        public static void SaveLocalNotificationData()
        {
            NotificationPush localNotificationData = new NotificationPush
            {
                AppPushIgnoreMsgIds    = NotificationData?.AppPushIgnoreMsgIds,
                RegionPushIgnoreMsgIds = NotificationData?.RegionPushIgnoreMsgIds
            };
            File.WriteAllText(AppNotifIgnoreFile,
                              localNotificationData.Serialize(InternalAppJSONContext.Default));
        }

        public static void LoadLocalNotificationData()
        {
            if (!File.Exists(AppNotifIgnoreFile))
            {
                File.WriteAllText(AppNotifIgnoreFile,
                                  new NotificationPush()
                                     .Serialize(InternalAppJSONContext.Default));
            }

            string data = File.ReadAllText(AppNotifIgnoreFile);
            NotificationPush? localNotificationData =
                data.Deserialize<NotificationPush>(InternalAppJSONContext.Default);
            if (NotificationData == null)
            {
                return;
            }

            NotificationData.AppPushIgnoreMsgIds    = localNotificationData?.AppPushIgnoreMsgIds;
            NotificationData.RegionPushIgnoreMsgIds = localNotificationData?.RegionPushIgnoreMsgIds;
            NotificationData.CurrentShowMsgIds      = localNotificationData?.CurrentShowMsgIds;
            NotificationData.EliminatePushList();
        }
    }
}