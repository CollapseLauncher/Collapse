using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;

using Windows.UI;

using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

using Newtonsoft.Json;

using Hi3Helper.Shared.Region;
using Hi3Helper.Shared.ClassStruct;

namespace CollapseLauncher
{
    public static class AppConfig
    {
        public static IntPtr m_windowHandle;
        public static AppWindow m_AppWindow;
        public static OverlappedPresenter m_presenter;
        public static bool IsPreview = false;
        public static bool IsAppThemeNeedRestart = false;
        public static bool IsFirstInstall = false;
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
