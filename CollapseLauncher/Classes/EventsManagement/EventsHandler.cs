using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;

using Windows.Foundation;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

using Newtonsoft.Json;

using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    #region LauncherUpdateRegion
    internal static class LauncherUpdateWatcher
    {
        public static string UpdateChannelName;
        public static Prop UpdateProperty;
        private static LauncherUpdateInvoker invoker = new LauncherUpdateInvoker();
        public static void GetStatus(LauncherUpdateProperty e) => invoker.GetStatus(e);
        public static async void StartCheckUpdate()
        {
            UpdateChannelName = AppConfig.IsPreview ? "preview" : "stable";
            string ChannelURL = string.Format(UpdateRepoChannel + "{0}/", UpdateChannelName);
            string CurrentVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            while (true)
            {
                if (!(GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? true) || ForceInvokeUpdate)
                {
                    try
                    {
                        MemoryStream RemoteData = new MemoryStream();
                        await new HttpClientHelper().DownloadFileAsync(ChannelURL + "fileindex.json", RemoteData, new CancellationToken());
                        string UpdateJSON = Encoding.UTF8.GetString(RemoteData.ToArray());
                        UpdateProperty = JsonConvert.DeserializeObject<Prop>(UpdateJSON);

                        if (CompareIsNewer(CurrentVersion, UpdateProperty.ver))
                            GetStatus(new LauncherUpdateProperty { IsUpdateAvailable = true, NewVersionName = UpdateProperty.ver });
                        else
                            GetStatus(new LauncherUpdateProperty { IsUpdateAvailable = false, NewVersionName = UpdateProperty.ver });

                        ForceInvokeUpdate = false;
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Update check has failed! Will retry in 15 mins.\r\n{ex}", LogType.Error, true);
                    }
                }
                // Delay for 15 mins
                await Task.Delay(900 * 1000);
            }
        }

        public static bool CompareIsNewer(string Input, string Output)
        {
            byte[] LocalVersion = Input.Split('.')
                .Select(x => byte.Parse(x))
                .ToArray();

            byte[] RemoteVersion = Output.Split('.')
                .Select(x => byte.Parse(x))
                .ToArray();

            if (RemoteVersion[0] > LocalVersion[0]) return true;
            if (RemoteVersion[1] > LocalVersion[1]) return true;
            if (RemoteVersion[2] > LocalVersion[2]) return true;
            if (RemoteVersion[3] > LocalVersion[3]) return true;

            return false;
        }

        public class Prop
        {
            public string ver { get; set; }
            public long time { get; set; }
            public List<fileProp> f { get; set; }
        }

        public class fileProp
        {
            public string p { get; set; }
            public string crc { get; set; }
            public long s { get; set; }
        }
    }

    internal class LauncherUpdateInvoker
    {
        public static event EventHandler<LauncherUpdateProperty> UpdateEvent;
        public void GetStatus(LauncherUpdateProperty e) => UpdateEvent?.Invoke(this, e);
    }

    internal class LauncherUpdateProperty
    {
        public bool IsUpdateAvailable { get; set; }
        public string NewVersionName { get; set; }
        public bool QuitFromUpdateMenu { get; set; } = false;
    }
    #endregion
    #region ThemeChangeRegion
    internal static class ThemeChanger
    {
        static ThemeChangerInvoker invoker = new ThemeChangerInvoker();
        public static void ChangeTheme(ApplicationTheme e) => invoker.ChangeTheme(e);
    }

    internal class ThemeChangerInvoker
    {
        public static event EventHandler<ThemeProperty> ThemeEvent;
        public void ChangeTheme(ApplicationTheme e) => ThemeEvent?.Invoke(this, new ThemeProperty(e));
    }

    internal class ThemeProperty
    {
        internal ThemeProperty(ApplicationTheme e) => Theme = e;
        public ApplicationTheme Theme { get; private set; }
    }
    #endregion
    #region ErrorSenderRegion
    public enum ErrorType { Unhandled, GameError }

    internal static class ErrorSender
    {
        static ErrorSenderInvoker invoker = new ErrorSenderInvoker();
        public static string ExceptionContent;
        public static string ExceptionTitle;
        public static string ExceptionSubtitle;
        public static void SendException(Exception e, ErrorType eT = ErrorType.Unhandled) => invoker.SendException(e, eT);
    }

    internal class ErrorSenderInvoker
    {
        public static event EventHandler<ErrorProperties> ExceptionEvent;
        public void SendException(Exception e, ErrorType eT) => ExceptionEvent?.Invoke(this, new ErrorProperties(e, eT));
    }

    internal class ErrorProperties
    {
        internal ErrorProperties(Exception e, ErrorType errorType)
        {
            Exception = e;
            ExceptionString = e.ToString();
            ErrorSender.ExceptionContent = ExceptionString;

            switch (errorType)
            {
                case ErrorType.Unhandled:
                    ErrorSender.ExceptionTitle = "Unhandled Error";
                    ErrorSender.ExceptionSubtitle = "An Unhandled Error has occured with an Exception Throw below:";
                    break;
                case ErrorType.GameError:
                    ErrorSender.ExceptionTitle = "Game Crashed";
                    ErrorSender.ExceptionSubtitle = "The game has crashed with error details below:";
                    break;
            }
        }
        public Exception Exception { get; private set; }
        public string ExceptionString { get; private set; }
    }
    #endregion
    #region MainFrameRegion
    internal static class MainFrameChanger
    {
        static MainFrameChangerInvoker invoker = new MainFrameChangerInvoker();
        public static void ChangeWindowFrame(Type e) => invoker.ChangeWindowFrame(e, new DrillInNavigationTransitionInfo());
        public static void ChangeWindowFrame(Type e, NavigationTransitionInfo eT) => invoker.ChangeWindowFrame(e, eT);
        public static void ChangeMainFrame(Type e) => invoker.ChangeMainFrame(e, new DrillInNavigationTransitionInfo());
        public static void ChangeMainFrame(Type e, NavigationTransitionInfo eT) => invoker.ChangeMainFrame(e, eT);
    }

    internal class MainFrameChangerInvoker
    {
        public static event EventHandler<MainFrameProperties> WindowFrameEvent;
        public static event EventHandler<MainFrameProperties> FrameEvent;
        public void ChangeWindowFrame(Type e, NavigationTransitionInfo eT) => WindowFrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
        public void ChangeMainFrame(Type e, NavigationTransitionInfo eT) => FrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
    }

    internal class MainFrameProperties
    {
        internal MainFrameProperties(Type FrameTo, NavigationTransitionInfo Transition)
        {
            this.FrameTo = FrameTo;
            this.Transition = Transition;
        }
        public Type FrameTo { get; private set; }
        public NavigationTransitionInfo Transition { get; private set; }
    }
    #endregion
    #region NotificationPushRegion

    internal static class NotificationSender
    {
        static NotificationInvoker invoker = new NotificationInvoker();
        public static void SendNotification(NotificationInvokerProp e) => invoker.SendNotification(e);
    }

    internal class NotificationInvoker
    {
        public static event EventHandler<NotificationInvokerProp> EventInvoker;
        public void SendNotification(NotificationInvokerProp e) => EventInvoker?.Invoke(this, e);
    }

    public class NotificationInvokerProp
    {
        public TypedEventHandler<InfoBar, object> CloseAction { get; set; } = null;
        public UIElement OtherContent { get; set; } = null;
        public NotificationProp Notification { get; set; }
        public bool IsAppNotif { get; set; } = true;
    }
    #endregion
}
