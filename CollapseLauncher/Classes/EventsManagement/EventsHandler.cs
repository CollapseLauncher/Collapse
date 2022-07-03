using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Hi3Helper.Http;
using static Hi3Helper.Data.ConverterTool;
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
            UpdateChannelName = IsPreview ? "preview" : "stable";
            string ChannelURL = string.Format(UpdateRepoChannel + "{0}/", UpdateChannelName);

            while (true)
            {
                if (!(GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? true) || ForceInvokeUpdate)
                {
                    try
                    {
                        using (MemoryStream RemoteData = new MemoryStream())
                        {
                            await new Http().DownloadStream(ChannelURL + "fileindex.json", RemoteData, new CancellationToken());
                            string UpdateJSON = Encoding.UTF8.GetString(RemoteData.ToArray());
                            UpdateProperty = JsonConvert.DeserializeObject<Prop>(UpdateJSON);
                        }

                        if (CompareVersion(AppCurrentVersion, UpdateProperty.ver))
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
                // Delay for 1 hour
                await Task.Delay(3600 * 1000);
            }
        }

        public static bool CompareVersion(string CurrentVer, string ComparedVer)
        {
            if (CurrentVer == null || ComparedVer == null) return false;

            uint concatLocalVer = 0, concatRemoteVer = 0;

            byte[] LocalVersion = CurrentVer.Split('.')
                .Select(x => byte.Parse(x))
                .ToArray();

            byte[] RemoteVersion = ComparedVer.Split('.')
                .Select(x => byte.Parse(x))
                .ToArray();

            for (int i = 0; i < LocalVersion.Length; i++)
            {
                concatLocalVer = ConcatUint(concatLocalVer, LocalVersion[i]);
                concatRemoteVer = ConcatUint(concatRemoteVer, RemoteVersion[i]);
            }

            return concatRemoteVer > concatLocalVer;
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
    #region BackgroundRegion
    internal static class BackgroundImgChanger
    {
        static BackgroundImgChangerInvoker invoker = new BackgroundImgChangerInvoker();
        public static async Task WaitForBackgroundToLoad() => await invoker.WaitForBackgroundToLoad();
        public static void ChangeBackground(string ImgPath, bool IsCustom = true) => invoker.ChangeBackground(ImgPath, IsCustom);
    }

    internal class BackgroundImgChangerInvoker
    {
        public static event EventHandler<BackgroundImgProperty> ImgEvent;
        BackgroundImgProperty property;
        public async Task WaitForBackgroundToLoad()
        {
            while (!property.IsImageLoaded)
                await Task.Delay(33);
        }
        public void ChangeBackground(string ImgPath, bool IsCustom) => ImgEvent?.Invoke(this, property = new BackgroundImgProperty(ImgPath, IsCustom));
    }

    internal class BackgroundImgProperty
    {
        internal BackgroundImgProperty(string ImgPath, bool IsCustom)
        {
            this.ImgPath = ImgPath;
            this.IsCustom = IsCustom;
        }

        public bool IsImageLoaded { get; set; } = false;
        public string ImgPath { get; private set; }
        public bool IsCustom { get; private set; }
    }
    #endregion
    #region SpawnWebView2Region
    internal static class SpawnWebView2
    {
        static SpawnWebView2Invoker invoker = new SpawnWebView2Invoker();
        public static void SpawnWebView2Window(string URL) => invoker.SpawnWebView2Window(URL);
    }

    internal class SpawnWebView2Invoker
    {
        public static event EventHandler<SpawnWebView2Property> SpawnEvent;
        public void SpawnWebView2Window(string URL) => SpawnEvent?.Invoke(this, new SpawnWebView2Property(URL));
    }

    internal class SpawnWebView2Property
    {
        internal SpawnWebView2Property(string URL) => this.URL = new Uri(URL);

        public Uri URL { get; set; }
    }
    #endregion
}
