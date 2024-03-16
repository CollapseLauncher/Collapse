using CollapseLauncher.Extension;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.ViewManagement;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;
using static Hi3Helper.Shared.Region.LauncherConfig;

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

        public static AppMode               m_appMode;
        public static Arguments             m_arguments = new Arguments();
        public static ushort[]              w_windowsVersionNumbers;
        public static bool                  m_isWindows11;
        public static Window                m_window;
        public static Microsoft.UI.WindowId m_windowID;
        public static Rect                  m_windowPosSize;
        public static IntPtr                m_windowHandle;
        public static IntPtr                m_oldWndProc;
        public static Delegate              m_newWndProcDelegate;
        public static AppWindow             m_appWindow;
        public static OverlappedPresenter   m_presenter;
        public static MainPage              m_mainPage;
        public static HomePage              m_homePage;
        public static bool                  m_windowSupportCustomTitle = false;
        public static Size                  m_actualMainFrameSize;
        public static double                m_appDPIScale;
        public static string                m_appCurrentFrameName;
        public static NotificationPush      NotificationData;
        public static bool                  IsCustomBG            = false;
        public static bool                  IsSkippingUpdateCheck = false;
        public static GameVersion           AppCurrentVersion;
        public static Color                 SystemAppTheme { get => new UISettings().GetColorValue(UIColorType.Background); }
        public static AppThemeMode          CurrentAppTheme;
        public static bool IsAppThemeLight
        {
            get => CurrentAppTheme switch
            {
                AppThemeMode.Dark => false,
                AppThemeMode.Light => true,
                _ => SystemAppTheme.ToString() == "#FFFFFFFF"
            };
        }

        public static string GetComboBoxGameRegionValue(object obj)
        {
            StackPanel Value = (StackPanel)obj;
            TextBlock TextBlock = (TextBlock)Value.Children.FirstOrDefault();
            return TextBlock.Text;
        }

        public static List<StackPanel> BuildGameTitleListUI()
        {
            List<StackPanel> list = new List<StackPanel>();
            foreach (string title in ConfigV2GameCategory)
            {
                StackPanel panel = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                TextBlock gameTitleTextBlock = panel.AddElementToStackPanel(new TextBlock { Text = title });
                TextBlock gameTitleTranslatedTextBlock = GetGameTitleRegionTranslationTextBlock(ref gameTitleTextBlock, Locale.Lang._GameClientTitles);

                if (gameTitleTranslatedTextBlock != null) panel.AddElementToStackPanel(gameTitleTranslatedTextBlock);
                list.Add(panel);
            }

            return list;
        }

        public static List<StackPanel> BuildGameRegionListUI(string GameCategory, List<string> GameCategoryList = null)
        {
            GameCategoryList ??= ConfigV2GameRegions;
            List<StackPanel> list = new List<StackPanel>();
            foreach (string region in GameCategoryList)
            {
                PresetConfigV2 config = ConfigV2.MetadataV2[GameCategory][region];
                StackPanel panel = UIElementExtensions.CreateStackPanel(Orientation.Horizontal);
                TextBlock gameRegionTextBlock = panel.AddElementToStackPanel(new TextBlock { Text = region });
                TextBlock gameRegionTranslatedTextBlock = GetGameTitleRegionTranslationTextBlock(ref gameRegionTextBlock, Locale.Lang._GameClientRegions);

                if (gameRegionTranslatedTextBlock != null) panel.AddElementToStackPanel(gameRegionTranslatedTextBlock);

                if (config.IsExperimental)
                {
                    Grid expTag =  new Grid
                    {
                        Padding = new Thickness(4, 0, 4, 0),
                        Margin = new Thickness(4, 3, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(!IsAppThemeLight ?
                            Color.FromArgb(255, 255, 255, 255) :
                            Color.FromArgb(255, 40, 40, 40))
                    };
                    expTag.Children.Add(new TextBlock
                    {
                        Text = GetGameChannelLabel(config.GameChannel),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 10,
                        Margin = new Thickness(0, -2, 0, 0),
                        Foreground = new SolidColorBrush(!IsAppThemeLight ?
                            Color.FromArgb(255, 40, 40, 40) :
                            Color.FromArgb(255, 255, 255, 255)),
                        FontWeight = FontWeights.Bold
                    });
                    panel.Children.Add(expTag);
                }

                list.Add(panel);
            }
            return list;
        }

        public static TextBlock GetGameTitleRegionTranslationTextBlock(ref TextBlock originalTextBlock, Dictionary<string, string> translationDictionary)
        {
            // Get the original region string
            string originalString = originalTextBlock?.Text;

            // If the originalTextBlock or regionString is null or empty, then return null
            if (originalTextBlock == null || string.IsNullOrEmpty(originalString)) return null;

            // Check if the translation is available. If not, then return null
            if (translationDictionary == null || translationDictionary.Count == 0
             || !translationDictionary.ContainsKey(originalString)) return null;

            // If it exists in the translation, then return the TextBlock and set the original
            // TextBlock visibility to collapse
            string translatedString = translationDictionary[originalString];
            originalTextBlock.Visibility = Visibility.Collapsed;
            TextBlock translatedTextBlock = new TextBlock { Text = translatedString };
            if (translatedString.Length > 15)
            {
                translatedTextBlock.TextWrapping = TextWrapping.Wrap;
                translatedTextBlock.TextTrimming = TextTrimming.WordEllipsis;
            }

            return translatedTextBlock;
        }

        public static string GetGameTitleRegionTranslationString(string originalString, Dictionary<string, string> translationDictionary)
        {
            // Check if the region translation is available. If not, then return null
            if (translationDictionary == null || translationDictionary.Count == 0
             || !translationDictionary.ContainsKey(originalString)) return originalString;

            // If the key exist, then return the translated string
            return translationDictionary[originalString];
        }

        private static string GetGameChannelLabel(GameChannel channel) => channel switch
        {
            GameChannel.Beta => "BETA",
            GameChannel.DevRelease => "DEV",
            _ => "PREVIEW"
        };

        public static void SaveLocalNotificationData()
        {
            NotificationPush LocalNotificationData = new NotificationPush
            {
                AppPushIgnoreMsgIds = NotificationData.AppPushIgnoreMsgIds,
                RegionPushIgnoreMsgIds = NotificationData.RegionPushIgnoreMsgIds
            };
            File.WriteAllText(AppNotifIgnoreFile,
                LocalNotificationData.Serialize(InternalAppJSONContext.Default));
        }

        public static void LoadLocalNotificationData()
        {
            if (!File.Exists(AppNotifIgnoreFile))
                File.WriteAllText(AppNotifIgnoreFile,
                    new NotificationPush()
                    .Serialize(InternalAppJSONContext.Default));

            string           Data                  = File.ReadAllText(AppNotifIgnoreFile);
            NotificationPush LocalNotificationData = Data.Deserialize<NotificationPush>(InternalAppJSONContext.Default);
            NotificationData.AppPushIgnoreMsgIds    = LocalNotificationData.AppPushIgnoreMsgIds;
            NotificationData.RegionPushIgnoreMsgIds = LocalNotificationData.RegionPushIgnoreMsgIds;
            NotificationData.CurrentShowMsgIds      = LocalNotificationData.CurrentShowMsgIds;
            NotificationData.EliminatePushList();
        }

        public static async Task<bool> CheckForNewConfigV2()
        {
            Stamp ConfigStamp = null;

            try
            {
                await using BridgedNetworkStream s = await FallbackCDNUtil.TryGetCDNFallbackStream(string.Format(AppGameConfigV2URLPrefix, (IsPreview ? "preview" : "stable") + "stamp"), default);
                ConfigStamp = await s.DeserializeAsync<Stamp>(CoreLibraryJSONContext.Default);
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while checking for new metadata!\r\n{ex}", LogType.Error, true);
                return false;
            }

            bool isMetadataOutdated = ConfigV2LastUpdate < ConfigStamp?.LastUpdated;
            LogWriteLine($"Checking for metadata update...\r\n" +
                         $"  LocalStamp  : {ConfigV2LastUpdate}\r\n" +
                         $"  RemoteStamp : {ConfigStamp?.LastUpdated}\r\n" +
                         $"  Out of date?: {isMetadataOutdated}", LogType.Warning, true);
            return isMetadataOutdated;
        }

        public static async Task DownloadConfigV2Files(bool Stamp, bool Content)
        {
            using (Http _httpClient = new Http())
            {
                if (!Directory.Exists(AppGameConfigMetadataFolder))
                    Directory.CreateDirectory(AppGameConfigMetadataFolder);

                if (Stamp) await GetConfigV2Content(_httpClient, "stamp", AppGameConfigV2StampPath);
                if (Content) await GetConfigV2Content(_httpClient, "config", AppGameConfigV2MetadataPath);
            }
        }

        private static async Task GetConfigV2Content(Http _httpClient, string prefix, string output)
        {
            string URL = string.Format(AppGameConfigV2URLPrefix, (IsPreview ? "preview" : "stable") + prefix);

            await using FileStream fs = new FileStream(output, FileMode.Create, FileAccess.Write);
            await using BridgedNetworkStream networkStream = await FallbackCDNUtil.TryGetCDNFallbackStream(URL, default);
            await networkStream.CopyToAsync(fs);
        }
    }
}
