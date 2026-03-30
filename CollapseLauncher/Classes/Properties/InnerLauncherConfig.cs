using CollapseLauncher.DiscordPresence;
using CollapseLauncher.Pages;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Win32.Native.LibraryImport;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable ConditionIsAlwaysTrueOrFalse
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo
#pragma warning disable IDE0130

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
            StartOnTray,
            GenerateVelopackMetadata
        }

        public static AppMode                   m_appMode;
        public static Arguments                 m_arguments = new();
        public static bool                      m_isWindows11;
        public static ConsoleControlHandler?    m_consoleCtrlHandler;
        public static MainPage?                 m_mainPage;
        public static HomePage?                 m_homePage;
        public static bool                      m_windowSupportCustomTitle = false;
        public static Size                      m_actualMainFrameSize;
        public static string?                   m_appCurrentFrameName;
        public static NotificationPush?         NotificationData;
        public static bool                      IsSkippingUpdateCheck = false;
        public static AppThemeMode              CurrentAppTheme;
#if !DISABLEDISCORD
        public static DiscordPresenceManager AppDiscordPresence
        {
            get
            {
                if (field != null) return field;

                bool isEnableDiscord = GetAppConfigValue("EnableDiscordRPC");
                field = new DiscordPresenceManager(isEnableDiscord);
                AppDiscordPresence.SetActivity(ActivityType.Idle);

                return field;
            }
        }
#endif
        public static bool IsAppThemeLight =>
            CurrentAppTheme switch
            {
                AppThemeMode.Dark => false,
                AppThemeMode.Light => true,
                _ => !PInvoke.ShouldAppsUseDarkMode()
            };

        public static void SaveLocalNotificationData()
        {
            NotificationPush localNotificationData = new NotificationPush
            {
                AppPushIgnoreMsgIds    = NotificationData?.AppPushIgnoreMsgIds,
                RegionPushIgnoreMsgIds = NotificationData?.RegionPushIgnoreMsgIds
            };
            File.WriteAllText(AppNotifIgnoreFile,
                              localNotificationData.Serialize(NotificationPushJsonContext.Default.NotificationPush, false));
        }

        public static async Task LoadLocalNotificationData()
        {
            FileStream? fileStream = null;

            bool forceCreate = false;
            while (true)
            {
                try
                {
                    fileStream = File.Open(AppNotifIgnoreFile, forceCreate ? FileMode.Create : FileMode.OpenOrCreate, FileAccess.ReadWrite);
                    if (fileStream.Length == 0)
                    {
                        await new NotificationPush()
                             .SerializeAsync(fileStream, NotificationPushJsonContext.Default.NotificationPush)
                             .ConfigureAwait(false);
                    }

                    fileStream.Position = 0;
                    NotificationPush? localNotificationData = await fileStream
                                                                   .DeserializeAsync(NotificationPushJsonContext.Default.NotificationPush)
                                                                   .ConfigureAwait(false);

                    if (NotificationData == null)
                    {
                        return;
                    }

                    NotificationData.AppPushIgnoreMsgIds    = localNotificationData?.AppPushIgnoreMsgIds;
                    NotificationData.RegionPushIgnoreMsgIds = localNotificationData?.RegionPushIgnoreMsgIds;
                    NotificationData.CurrentShowMsgIds      = localNotificationData?.CurrentShowMsgIds;
                    NotificationData.EliminatePushList();

                    return;
                }
                catch
                {
                    if (forceCreate)
                    {
                        throw;
                    }
                    forceCreate = true;
                }
                finally
                {
                    if (fileStream != null)
                    {
                        await fileStream.DisposeAsync();
                    }
                }
            }
        }
    }
}