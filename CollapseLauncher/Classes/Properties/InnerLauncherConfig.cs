using Hi3Helper;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
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
using Windows.Foundation;
using Windows.UI;
using static Hi3Helper.Preset.ConfigV2Store;

namespace CollapseLauncher
{
    public static class InnerLauncherConfig
    {
        public enum BackdropType
        {
            Mica,
            DesktopAcrylic,
            DefaultColor,
        }

        public enum AppMode
        {
            Launcher,
            Updater,
            ElevateUpdater,
            Reindex,
            InvokerMigrate,
            InvokerTakeOwnership,
            InvokerMoveSteam,
            Hi3CacheUpdater
        }

        public enum AppReleaseChannel
        {
            Stable,
            Preview,
            StablePortable,
            PreviewPortable
        }

        public static AppMode m_appMode;
        public static Arguments m_arguments = new Arguments();
#if !DISABLE_COM
        public static BackdropManagement m_backDrop;
        public static WindowsSystemDispatcherQueueHelper m_wsdqHelper;
#endif
        public static BackdropType m_currentBackdrop;
        public static MicaController m_micaController;
        public static DesktopAcrylicController m_acrylicController;
        public static SystemBackdropConfiguration m_configurationSource;


        public static Window m_window;
        public static IntPtr m_windowHandle;
        public static Rect m_windowPosSize;
        public static WindowId m_windowID;
        public static AppWindow m_appWindow;
        public static OverlappedPresenter m_presenter;
        public static Size m_actualMainFrameSize;
        public static double m_appDPIScale;
        public static string m_appCurrentFrameName;
        public static ApplicationTheme CurrentRequestedAppTheme;
        public static AppThemeMode CurrentAppTheme;
        public static Color SystemAppTheme;
        public static NotificationPush NotificationData;

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
                GameChannel chan = config.GameChannel;
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
                        Text = "EXPER",
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

        public static void SaveLocalNotificationData()
        {
            NotificationPush LocalNotificationData = new NotificationPush
            {
                AppPushIgnoreMsgIds = NotificationData.AppPushIgnoreMsgIds,
                RegionPushIgnoreMsgIds = NotificationData.RegionPushIgnoreMsgIds
            };
            File.WriteAllText(LauncherConfig.AppNotifIgnoreFile,
                JsonSerializer.Serialize(LocalNotificationData, typeof(NotificationPush), NotificationPushContext.Default));
        }

        public static void LoadLocalNotificationData()
        {
            if (!File.Exists(LauncherConfig.AppNotifIgnoreFile))
                File.WriteAllText(LauncherConfig.AppNotifIgnoreFile,
                    JsonSerializer.Serialize(new NotificationPush
                    { AppPushIgnoreMsgIds = new List<int>(), RegionPushIgnoreMsgIds = new List<int>() },
                    typeof(NotificationPush),
                    NotificationPushContext.Default));

            string Data = File.ReadAllText(LauncherConfig.AppNotifIgnoreFile);
            NotificationPush LocalNotificationData = (NotificationPush)JsonSerializer.Deserialize(Data, typeof(NotificationPush), NotificationPushContext.Default);
            if (NotificationData != null)
            {
                NotificationData.AppPushIgnoreMsgIds = LocalNotificationData.AppPushIgnoreMsgIds;
                NotificationData.RegionPushIgnoreMsgIds = LocalNotificationData.RegionPushIgnoreMsgIds;
            }
            NotificationData.EliminatePushList();
        }
    }
}
