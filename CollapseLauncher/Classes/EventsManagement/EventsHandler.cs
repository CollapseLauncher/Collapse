﻿using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Squirrel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Foundation;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    #region LauncherUpdateRegion
    internal static class LauncherUpdateWatcher
    {
        public static string UpdateChannelName;
        public static AppUpdateVersionProp UpdateProperty;
        private static LauncherUpdateInvoker invoker = new LauncherUpdateInvoker();
        public static void GetStatus(LauncherUpdateProperty e) => invoker.GetStatus(e);
        public static async void StartCheckUpdate()
        {
            UpdateChannelName = IsPreview ? "preview" : "stable";

            while (true)
            {
                if (!(GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? true) || ForceInvokeUpdate)
                {
                    try
                    {
                        using (Updater updater = new Updater(UpdateChannelName))
                        {
                            UpdateInfo info = await updater.StartCheck();
                            GameVersion RemoteVersion = new GameVersion(info.FutureReleaseEntry.Version.Version);

                            AppUpdateVersionProp miscMetadata = await GetUpdateMetadata();
                            UpdateProperty = new AppUpdateVersionProp { ver = RemoteVersion.VersionString, time = miscMetadata.time };

                            if (CompareVersion(AppCurrentVersion, RemoteVersion))
                                GetStatus(new LauncherUpdateProperty { IsUpdateAvailable = true, NewVersionName = RemoteVersion });
                            else
                                GetStatus(new LauncherUpdateProperty { IsUpdateAvailable = false, NewVersionName = RemoteVersion });
                        }

                        ForceInvokeUpdate = false;
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"Update check has failed! Will retry in 15 mins.\r\n{ex}", LogType.Error, true);
                    }
                }

                // Delay for 15 minutes
                await Task.Delay(900 * 1000);
            }
        }

        private static async ValueTask<AppUpdateVersionProp> GetUpdateMetadata()
        {
            string relativePath = ConverterTool.CombineURLFromString(UpdateChannelName, "fileindex.json");

            using (Http client = new Http(true))
            using (MemoryStream ms = new MemoryStream())
            {
                await FallbackCDNUtil.DownloadCDNFallbackContent(client, ms, relativePath, default);
                ms.Position = 0;

                return (AppUpdateVersionProp)JsonSerializer.Deserialize(ms, typeof(AppUpdateVersionProp), AppUpdateVersionPropContext.Default);
            }
        }

        public static bool CompareVersion(GameVersion? CurrentVer, GameVersion? ComparedVer)
        {
            if (CurrentVer == null || ComparedVer == null) return false;
            return CurrentVer.Value.ToVersion() < ComparedVer.Value.ToVersion();
        }
    }

    public class AppUpdateVersionProp
    {
        public string ver { get; set; }
        public long time { get; set; }
        public List<AppUpdateVersionFileProp> f { get; set; }
    }

    public class AppUpdateVersionFileProp
    {
        public string p { get; set; }
        public string crc { get; set; }
        public long s { get; set; }
    }

    internal class LauncherUpdateInvoker
    {
        public static event EventHandler<LauncherUpdateProperty> UpdateEvent;
        public void GetStatus(LauncherUpdateProperty e) => UpdateEvent?.Invoke(this, e);
    }

    internal class LauncherUpdateProperty
    {
        public bool IsUpdateAvailable { get; set; }
        public GameVersion NewVersionName { get; set; }
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
    public enum ErrorType { Unhandled, GameError, Connection }

    internal static class ErrorSender
    {
        static ErrorSenderInvoker invoker = new ErrorSenderInvoker();
        public static string ExceptionContent;
        public static ErrorType ExceptionType;
        public static string ExceptionTitle;
        public static string ExceptionSubtitle;
        public static void SendException(Exception e, ErrorType eT = ErrorType.Unhandled) => invoker.SendException(e, eT);
        public static void SendExceptionWithoutPage(Exception e, ErrorType eT = ErrorType.Unhandled)
        {
            ExceptionContent = e.ToString();
            ExceptionType = eT;
            SetPageTitle(eT);
        }

        public static void SetPageTitle(ErrorType errorType)
        {
            switch (errorType)
            {
                case ErrorType.Unhandled:
                    ExceptionTitle = Lang._UnhandledExceptionPage.UnhandledTitle1;
                    ExceptionSubtitle = Lang._UnhandledExceptionPage.UnhandledTitle1;
                    break;
                case ErrorType.Connection:
                    ExceptionTitle = Lang._UnhandledExceptionPage.UnhandledTitle2;
                    ExceptionSubtitle = Lang._UnhandledExceptionPage.UnhandledSubtitle2;
                    break;
                case ErrorType.GameError:
                    ExceptionTitle = Lang._UnhandledExceptionPage.UnhandledTitle3;
                    ExceptionSubtitle = Lang._UnhandledExceptionPage.UnhandledSubtitle3;
                    break;
            }
        }
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
            ErrorSender.ExceptionType = errorType;
            ErrorSender.SetPageTitle(errorType);
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
        public async Task WaitForBackgroundToLoad() => await Task.Run(() => { while (!property.IsImageLoaded) { } });
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
        internal SpawnWebView2Property(string URL) => this.URL = URL;

        public string URL { get; set; }
    }
    #endregion
    #region ShowLoadingPage
    internal static class ShowLoadingPage
    {
        static ShowLoadingPageInvoker invoker = new ShowLoadingPageInvoker();
        public static void ShowLoading(string Title, string Subtitle, bool Hide = false) => invoker.ShowLoading(Hide, Title, Subtitle);
    }

    internal class ShowLoadingPageInvoker
    {
        public static event EventHandler<ShowLoadingPageProperty> PageEvent;
        public void ShowLoading(bool Hide, string Title, string Subtitle) => PageEvent?.Invoke(this, new ShowLoadingPageProperty(Hide, Title, Subtitle));
    }

    internal class ShowLoadingPageProperty
    {
        internal ShowLoadingPageProperty(bool Hide, string Title, string Subtitle)
        {
            this.Hide = Hide;
            this.Title = Title;
            this.Subtitle = Subtitle;
        }
        public bool Hide { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }
    }
    #endregion
    #region ChangeTitleDragArea
    public enum DragAreaTemplate
    {
        Full,
        Default
    }

    internal static class ChangeTitleDragArea
    {
        static ChangeTitleDragAreaInvoker invoker = new ChangeTitleDragAreaInvoker();
        public static void Change(DragAreaTemplate Template) => invoker.Change(Template);
    }

    internal class ChangeTitleDragAreaInvoker
    {
        public static event EventHandler<ChangeTitleDragAreaProperty> TitleBarEvent;
        public void Change(DragAreaTemplate Template) => TitleBarEvent?.Invoke(this, new ChangeTitleDragAreaProperty(Template));
    }

    internal class ChangeTitleDragAreaProperty
    {
        internal ChangeTitleDragAreaProperty(DragAreaTemplate Template)
        {
            this.Template = Template;
        }

        public DragAreaTemplate Template { get; private set; }
    }
    #endregion
}
