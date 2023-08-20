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
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
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
            OOBEState
        }

        public static AppMode m_appMode;
        public static Arguments m_arguments = new Arguments();
        public static ushort[] w_windowsVersionNumbers;
        public static Window m_window;
        public static bool m_windowSupportCustomTitle = false;
        public static IntPtr m_windowHandle;
        public static Rect m_windowPosSize;
        public static Microsoft.UI.WindowId m_windowID;
        public static AppWindow m_appWindow;
        public static OverlappedPresenter m_presenter;
        public static Size m_actualMainFrameSize;
        public static double m_appDPIScale;
        public static string m_appCurrentFrameName;
        public static ApplicationTheme CurrentRequestedAppTheme;
        public static AppThemeMode CurrentAppTheme;
        public static Color SystemAppTheme;
        public static NotificationPush NotificationData;
        public static bool IsCustomBG = false;
        public static bool IsSkippingUpdateCheck = false;
        public static GameVersion AppCurrentVersion;
        public static MainPage m_mainPage;

        public static ApplicationTheme GetAppTheme()
        {
            AppThemeMode AppTheme = CurrentAppTheme;

            switch (AppTheme)
            {
                case AppThemeMode.Light:
                    return ApplicationTheme.Light;
                case AppThemeMode.Dark:
                    return ApplicationTheme.Dark;
                default:
                    if (SystemAppTheme.ToString() == "#FFFFFFFF")
                        return ApplicationTheme.Light;
                    else
                        return ApplicationTheme.Dark;
            }
        }

        public static string GetComboBoxGameRegionValue(object obj)
        {
            StackPanel Value = (StackPanel)obj;
            TextBlock TextBlock = (TextBlock)Value.Children.FirstOrDefault();
            return TextBlock.Text;
        }

        public static List<StackPanel> BuildGameRegionListUI(string GameCategory)
        {
            List<StackPanel> list = new List<StackPanel>();
            foreach (string region in ConfigV2GameRegions)
            {
                PresetConfigV2 config = ConfigV2.MetadataV2[GameCategory][region];
                StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Children.Add(new TextBlock { Text = region });
                if (config.IsExperimental)
                {
                    Grid expTag = new Grid
                    {
                        Padding = new Thickness(4, 0, 4, 0),
                        Margin = new Thickness(4, 3, 0, 0),
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        CornerRadius = new CornerRadius(4),
                        Background = new SolidColorBrush(GetAppTheme() == ApplicationTheme.Dark ?
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
                        Foreground = new SolidColorBrush(GetAppTheme() == ApplicationTheme.Dark ?
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

        private static string GetGameChannelLabel(GameChannel channel) => channel switch
        {
            GameChannel.Beta => "BETA",
            GameChannel.DevRelease => "DEV",
            _ => "EXPER"
        };

        public static void SaveLocalNotificationData()
        {
            NotificationPush LocalNotificationData = new NotificationPush
            {
                AppPushIgnoreMsgIds = NotificationData.AppPushIgnoreMsgIds,
                RegionPushIgnoreMsgIds = NotificationData.RegionPushIgnoreMsgIds
            };
            File.WriteAllText(AppNotifIgnoreFile,
                JsonSerializer.Serialize(LocalNotificationData, typeof(NotificationPush), InternalAppJSONContext.Default));
        }

        public static void LoadLocalNotificationData()
        {
            if (!File.Exists(AppNotifIgnoreFile))
                File.WriteAllText(AppNotifIgnoreFile,
                    JsonSerializer.Serialize(new NotificationPush(),
                    typeof(NotificationPush),
                    InternalAppJSONContext.Default));

            string Data = File.ReadAllText(AppNotifIgnoreFile);
            NotificationPush LocalNotificationData = (NotificationPush)JsonSerializer.Deserialize(Data, typeof(NotificationPush), InternalAppJSONContext.Default);
            NotificationData.AppPushIgnoreMsgIds = LocalNotificationData.AppPushIgnoreMsgIds;
            NotificationData.RegionPushIgnoreMsgIds = LocalNotificationData.RegionPushIgnoreMsgIds;
            NotificationData.CurrentShowMsgIds = LocalNotificationData.CurrentShowMsgIds;
            NotificationData.EliminatePushList();
        }

        public static async Task<bool> CheckForNewConfigV2()
        {
            Stamp ConfigStamp = null;

            try
            {
                using (Http _http = new Http())
                using (Stream s = new MemoryStream())
                {
                    await FallbackCDNUtil.DownloadCDNFallbackContent(_http, s, string.Format(AppGameConfigV2URLPrefix, (IsPreview ? "preview" : "stable") + "stamp"), default).ConfigureAwait(false);
                    s.Position = 0;
                    ConfigStamp = (Stamp)JsonSerializer.Deserialize(s, typeof(Stamp), CoreLibraryJSONContext.Default);
#if DEBUG
                    LogWriteLine($"Checking for metadata update...\r\n" +
                        $"  LocalStamp  : {ConfigV2LastUpdate}\r\n" +
                        $"  RemoteStamp : {ConfigStamp?.LastUpdated}", LogType.Warning, true);
#endif
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Failed while checking for new metadata!\r\n{ex}", LogType.Error, true);
                return false;
            }

            return ConfigV2LastUpdate < ConfigStamp?.LastUpdated;
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

            using (FileStream fs = new FileStream(output, FileMode.Create, FileAccess.Write))
            {
                await FallbackCDNUtil.DownloadCDNFallbackContent(_httpClient, fs, URL, default).ConfigureAwait(false);
            }
        }
    }
}
