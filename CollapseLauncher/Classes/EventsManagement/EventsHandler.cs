using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable CheckNamespace
// ReSharper disable AssignNullToNotNullAttribute

namespace CollapseLauncher
{
    #region LauncherUpdateRegion
    internal static class LauncherUpdateWatcher
    {
        
    #pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
        public static string               UpdateChannelName = "stable";
        public static AppUpdateVersionProp UpdateProperty;
        public static bool                 isUpdateCooldownActive;
    #pragma warning restore CS0649 // Field is never assigned to, and will always have its default value
        
        private static LauncherUpdateInvoker invoker = new();
        public static void GetStatus(LauncherUpdateProperty e) => invoker!.GetStatus(e);
        
        public static bool isMetered
        {
            get
            {
                NetworkCostType currentNetCostType = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost()?.NetworkCostType ?? NetworkCostType.Fixed;
                return !(currentNetCostType == NetworkCostType.Unrestricted || currentNetCostType == NetworkCostType.Unknown);
            }
        }
    }

    public class AppUpdateVersionProp
    {
        [JsonPropertyName("f")]
        public List<AppUpdateVersionFileProp> FileList { get; set; }

        [JsonPropertyName("forceUpdate")]
        public bool IsForceUpdate { get; set; }

        [JsonIgnore]
        public DateTime? TimeLocalTime
        {
            get => DateTimeOffset.FromUnixTimeSeconds(UnixTime).DateTime.ToLocalTime();
        }

        [JsonPropertyName("time")]
        public long UnixTime { get; set; }

        [JsonIgnore]
        public GameVersion? Version
        {
            get
            {
                if (!GameVersion.TryParse(VersionString, out GameVersion? result))
                    return null;

                return result;
            }
        }

        [JsonPropertyName("ver")]
        public string VersionString { get; set; }
    }

    public class AppUpdateVersionFileProp
    {
        [JsonPropertyName("p")] public string FilePath { get; set; }
        [JsonPropertyName("crc")] public string FileMD5Hash { get; set; }
        [JsonPropertyName("s")] public long FileSize { get; set; }
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
        public bool QuitFromUpdateMenu { get; set; }
    }
    #endregion
    #region ThemeChangeRegion
    internal static class ThemeChanger
    {
        static ThemeChangerInvoker invoker = new ThemeChangerInvoker();
        public static void ChangeTheme(ElementTheme e)
        {
            CurrentAppTheme = e switch
            {
                ElementTheme.Light => AppThemeMode.Light,
                ElementTheme.Default => AppThemeMode.Default,
                _ => AppThemeMode.Dark
            };

            SetAppConfigValue("ThemeMode", CurrentAppTheme.ToString());
            invoker!.ChangeTheme(e);
        }
    }

    internal class ThemeChangerInvoker
    {
        public static event EventHandler<ThemeProperty> ThemeEvent;
        public void ChangeTheme(ElementTheme e) => ThemeEvent?.Invoke(this, new ThemeProperty(e));
    }

    internal class ThemeProperty
    {
        internal ThemeProperty(ElementTheme e) => Theme = e;
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public ElementTheme Theme { get; private set; }
    }
    #endregion
    #region ErrorSenderRegion
    public enum ErrorType { Unhandled, GameError, Connection, Warning, DiskCrc }

    internal static class ErrorSender
    {
        static ErrorSenderInvoker invoker = new ErrorSenderInvoker();
        public static string ExceptionContent;
        public static ErrorType ExceptionType;
        public static string ExceptionTitle;
        public static string ExceptionSubtitle;

        public static void SendException(Exception e, ErrorType eT = ErrorType.Unhandled)
        {
            SentryHelper.ExceptionHandler(e);
            invoker!.SendException(e, eT);
        } 
        public static void SendWarning(Exception e, ErrorType eT = ErrorType.Warning) =>
            invoker!.SendException(e, eT);
        public static void SendExceptionWithoutPage(Exception e, ErrorType eT = ErrorType.Unhandled)
        {
            ExceptionContent = e!.ToString();
            ExceptionType = eT;
            SetPageTitle(eT);
        }

        public static void SetPageTitle(ErrorType errorType)
        {
            var _locUnhandledException = Lang!._UnhandledExceptionPage!;
            switch (errorType)
            {
                case ErrorType.Unhandled:
                    ExceptionTitle    = _locUnhandledException.UnhandledTitle1;
                    ExceptionSubtitle = _locUnhandledException.UnhandledTitle1;
                    break;
                case ErrorType.Connection:
                    ExceptionTitle    = _locUnhandledException.UnhandledTitle2;
                    ExceptionSubtitle = _locUnhandledException.UnhandledSubtitle2;
                    break;
                case ErrorType.GameError:
                    ExceptionTitle    = _locUnhandledException.UnhandledTitle3;
                    ExceptionSubtitle = _locUnhandledException.UnhandledSubtitle3;
                    break;
                case ErrorType.Warning:
                    ExceptionTitle    = _locUnhandledException.UnhandledTitle4;
                    ExceptionSubtitle = _locUnhandledException.UnhandledSubtitle4;
                    break;
                case ErrorType.DiskCrc:
                    ExceptionTitle    = _locUnhandledException.UnhandledTitleDiskCrc;
                    ExceptionSubtitle = _locUnhandledException.UnhandledSubDiskCrc;
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
            Exception                    = e;
            ExceptionString              = e?.ToString() ?? String.Empty;
            ErrorSender.ExceptionContent = ExceptionString;
            ErrorSender.ExceptionType    = errorType;
            ErrorSender.SetPageTitle(errorType);
        }
        public Exception Exception { get; private set; }
        public string ExceptionString { get; private set; }
    }
    #endregion
    #region MainFrameRegion
    internal static class MainFrameChanger
    {
        private static Type currentWindow;
        private static Type currentPage;
        static MainFrameChangerInvoker invoker = new MainFrameChangerInvoker();
        public static void GoBackWindowFrame() => invoker!.GoBackWindowFrame();
        public static void ChangeWindowFrame(Type toPage) => ChangeWindowFrame(toPage, new DrillInNavigationTransitionInfo());
        public static void ChangeWindowFrame(Type toPage, NavigationTransitionInfo transition)
        {
            if (toPage == null)
                throw new NullReferenceException("Argument: toPage cannot be null!");

            currentWindow = toPage;
            invoker!.ChangeWindowFrame(toPage, transition);
        }

        public static void GoBackMainFrame() => invoker!.GoBackMainFrame();
        public static void ChangeMainFrame(Type toPage) => ChangeMainFrame(toPage, new DrillInNavigationTransitionInfo());
        public static void ChangeMainFrame(Type toPage, NavigationTransitionInfo transition)
        {
            if (toPage == null)
                throw new NullReferenceException("Argument: toPage cannot be null!");

            currentPage = toPage;
            invoker!.ChangeMainFrame(toPage, transition);
        }

        public static void ReloadCurrentWindowFrame() => ChangeWindowFrame(currentWindow);
        public static void ReloadCurrentMainFrame() => ChangeMainFrame(currentPage);
    }

    internal class MainFrameChangerInvoker
    {
        public static event EventHandler<MainFrameProperties> WindowFrameEvent;
        public static event EventHandler<MainFrameProperties> FrameEvent;
        public static event EventHandler WindowFrameGoBackEvent;
        public static event EventHandler FrameGoBackEvent;
        public void ChangeWindowFrame(Type e, NavigationTransitionInfo eT) => WindowFrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
        public void ChangeMainFrame(Type e, NavigationTransitionInfo eT) => FrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
        public void GoBackWindowFrame() => WindowFrameGoBackEvent?.Invoke(this, null);
        public void GoBackMainFrame() => FrameGoBackEvent?.Invoke(this, null);
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
        public static void SendNotification(NotificationInvokerProp e) => invoker!.SendNotification(e);
        public static void SendCustomNotification(int tagID, InfoBar infoBarUI) => invoker!.SendNotification(new NotificationInvokerProp
        {
            IsCustomNotif = true,
            CustomNotifAction = NotificationCustomAction.Add,
            Notification = new NotificationProp
            {
                MsgId = tagID,
            },
            OtherContent = infoBarUI
        });
        public static void RemoveCustomNotification(int tagID) => invoker!.SendNotification(new NotificationInvokerProp
        {
            IsCustomNotif = true,
            CustomNotifAction = NotificationCustomAction.Remove,
            Notification = new NotificationProp
            {
                MsgId = tagID,
            }
        });
    }

    internal class NotificationInvoker
    {
        public static event EventHandler<NotificationInvokerProp> EventInvoker;
        public void SendNotification(NotificationInvokerProp e) => EventInvoker?.Invoke(this, e);
    }

    public enum NotificationCustomAction { Add, Remove }
    public class NotificationInvokerProp
    {
        public TypedEventHandler<InfoBar, object> CloseAction { get; set; }
        public FrameworkElement OtherContent { get; set; }
        public NotificationProp Notification { get; set; }
        public bool IsAppNotif { get; set; } = true;
        public bool IsCustomNotif { get; set; }
        public NotificationCustomAction CustomNotifAction { get; set; }

    }
    #endregion
    #region BackgroundRegion
    internal static class BackgroundImgChanger
    {
        static BackgroundImgChangerInvoker invoker = new();
        public static void ChangeBackground(string ImgPath, Action ActionAfterLoaded,
            bool IsCustom = true, bool IsForceRecreateCache = false, bool IsRequestInit = false)
        {
            invoker!.ChangeBackground(ImgPath, ActionAfterLoaded, IsCustom, IsForceRecreateCache, IsRequestInit);
        }
        public static void ToggleBackground(bool Hide) => invoker!.ToggleBackground(Hide);
    }

    internal class BackgroundImgChangerInvoker
    {
        public static event EventHandler<BackgroundImgProperty> ImgEvent;
        public static event EventHandler<bool> IsImageHide;

        public void ChangeBackground(string ImgPath, Action ActionAfterLoaded,
                                     bool IsCustom, bool IsForceRecreateCache = false, bool IsRequestInit = false)
        {
            ImgEvent?.Invoke(this, new BackgroundImgProperty(ImgPath, IsCustom, IsForceRecreateCache, IsRequestInit, ActionAfterLoaded));
        }

        public void ToggleBackground(bool Hide) => IsImageHide?.Invoke(this, Hide);
    }

    internal class BackgroundImgProperty
    {
        internal BackgroundImgProperty(string ImgPath, bool IsCustom, bool IsForceRecreateCache, bool IsRequestInit, Action ActionAfterLoaded)
        {
            this.ImgPath              = ImgPath;
            this.IsCustom             = IsCustom;
            this.IsForceRecreateCache = IsForceRecreateCache;
            this.IsRequestInit        = IsRequestInit;
            this.ActionAfterLoaded    = ActionAfterLoaded;
        }

        public Action ActionAfterLoaded { get; set; }
        public bool IsRequestInit { get; set; }
        public bool IsForceRecreateCache { get; set; }
        public string ImgPath { get; private set; }
        public bool IsCustom { get; private set; }
    }
    #endregion
    #region SpawnWebView2Region
    internal static class SpawnWebView2
    {
        static SpawnWebView2Invoker invoker = new SpawnWebView2Invoker();
        public static void SpawnWebView2Window(string URL, UIElement parentUI)
        {
            if (GetAppConfigValue("UseExternalBrowser").ToBool())
            {
                if (string.IsNullOrEmpty(URL)) return;
                parentUI!.DispatcherQueue!.TryEnqueue(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = URL,
                        UseShellExecute = true,
                    });
                });
            }
            else invoker!.SpawnWebView2Window(URL);
        }
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
        public static void ShowLoading(string Title, string Subtitle, bool Hide = false) => invoker!.ShowLoading(Hide, Title, Subtitle);
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
        None,
        Full,
        Default
    }

    internal static class ChangeTitleDragArea
    {
        static ChangeTitleDragAreaInvoker invoker = new ChangeTitleDragAreaInvoker();
        public static void Change(DragAreaTemplate Template) => invoker!.Change(Template);
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
    #region UpdateBindings
    internal static class UpdateBindings
    {
        static UpdateBindingsInvoker invoker = new UpdateBindingsInvoker();
        public static void Update() => invoker!.Update();
    }

    internal class UpdateBindingsInvoker
    {
        private static EventArgs DummyArgs = new();
        public static event EventHandler UpdateEvents;
        public void Update() => UpdateEvents?.Invoke(this, DummyArgs!);
    }
    #endregion
}
