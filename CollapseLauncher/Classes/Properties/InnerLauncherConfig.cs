using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.Foundation;
using Windows.UI;

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
            InvokerMoveSteam
        }

        public enum AppReleaseChannel
        {
            Stable,
            Preview
        }

        public static AppMode m_appMode;
        public static Arguments m_arguments = new Arguments();
        public static BackdropManagement m_backDrop;
        public static WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        public static BackdropType m_currentBackdrop;
        public static MicaController m_micaController;
        public static DesktopAcrylicController m_acrylicController;
        public static SystemBackdropConfiguration m_configurationSource;


        public static Window m_window;
        public static IntPtr m_windowHandle;
        public static AppWindow m_AppWindow;
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

        public static void SaveLocalNotificationData()
        {
            NotificationPush LocalNotificationData = new NotificationPush
            {
                AppPushIgnoreMsgIds = NotificationData.AppPushIgnoreMsgIds,
                RegionPushIgnoreMsgIds = NotificationData.RegionPushIgnoreMsgIds
            };
            File.WriteAllText(LauncherConfig.AppNotifIgnoreFile, JsonConvert.SerializeObject(LocalNotificationData, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
        }

        public static void LoadLocalNotificationData()
        {
            if (!File.Exists(LauncherConfig.AppNotifIgnoreFile))
                File.WriteAllText(LauncherConfig.AppNotifIgnoreFile, JsonConvert.SerializeObject(new NotificationPush
                { AppPushIgnoreMsgIds = new List<int>(), RegionPushIgnoreMsgIds = new List<int>() }, Formatting.None, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));

            string Data = File.ReadAllText(LauncherConfig.AppNotifIgnoreFile);
            NotificationPush LocalNotificationData = JsonConvert.DeserializeObject<NotificationPush>(Data);
            if (NotificationData != null)
            {
                NotificationData.AppPushIgnoreMsgIds = LocalNotificationData.AppPushIgnoreMsgIds;
                NotificationData.RegionPushIgnoreMsgIds = LocalNotificationData.RegionPushIgnoreMsgIds;
            }
            NotificationData.EliminatePushList();
        }
    }
}
