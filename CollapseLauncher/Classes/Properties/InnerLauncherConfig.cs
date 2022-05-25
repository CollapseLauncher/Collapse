using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows;

using Windows.UI;

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Composition.SystemBackdrops;

using Newtonsoft.Json;

using Hi3Helper.Shared.Region;
using Hi3Helper.Shared.ClassStruct;

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

        public static IntPtr m_windowHandle;
        public static AppWindow m_AppWindow;
        public static OverlappedPresenter m_presenter;
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
            NotificationPush LocalNotificationData  = JsonConvert.DeserializeObject<NotificationPush>(Data);
            if (NotificationData != null)
            {
                NotificationData.AppPushIgnoreMsgIds = LocalNotificationData.AppPushIgnoreMsgIds;
                NotificationData.RegionPushIgnoreMsgIds = LocalNotificationData.RegionPushIgnoreMsgIds;
            }
            NotificationData.EliminatePushList();
        }
    }
}
