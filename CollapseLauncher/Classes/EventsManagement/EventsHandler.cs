using CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
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
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable CheckNamespace
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Global

namespace CollapseLauncher
{
    #region LauncherUpdateRegion
    internal static class LauncherUpdateWatcher
    {
        private static readonly LauncherUpdateInvoker Invoker = new();
        public static           void                  GetStatus(LauncherUpdateProperty e) => Invoker!.GetStatus(e);
        
        public static bool IsMetered
        {
            get
            {
                NetworkCostType currentNetCostType = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost()?.NetworkCostType ?? NetworkCostType.Fixed;
                return currentNetCostType is not (NetworkCostType.Unrestricted or NetworkCostType.Unknown);
            }
        }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(AppUpdateVersionProp))]
    internal sealed partial class AppUpdateVersionPropJsonContext : JsonSerializerContext;
    
    public sealed class AppUpdateVersionProp
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
                return !GameVersion.TryParse(VersionString, null, out GameVersion result) ? null : result;
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
        public bool IsUpdateAvailable { get; init; }
        public GameVersion NewVersionName { get; init; }
        public bool QuitFromUpdateMenu { get; init; }
    }
    #endregion
    #region ThemeChangeRegion
    internal static class ThemeChanger
    {
        private static readonly ThemeChangerInvoker Invoker = new();
        public static void ChangeTheme(ElementTheme e)
        {
            CurrentAppTheme = e switch
            {
                ElementTheme.Light => AppThemeMode.Light,
                ElementTheme.Default => AppThemeMode.Default,
                _ => AppThemeMode.Dark
            };

            SetAppConfigValue("ThemeMode", CurrentAppTheme.ToString());
            Invoker.ChangeTheme(e);
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
        private static readonly ErrorSenderInvoker Invoker = new();
        public static           Exception          Exception;
        public static           string             ExceptionContent;
        public static           ErrorType          ExceptionType;
        public static           string             ExceptionTitle;
        public static           string             ExceptionSubtitle;
        public static           Guid               SentryErrorId;

        public static void SendException(Exception e, ErrorType eT = ErrorType.Unhandled, bool isSendToSentry = true)
        {
            // Reset previous Sentry ID 
            SentryErrorId = Guid.Empty;
            Exception = e;
            var sentryGuid = Guid.Empty;
            if (isSendToSentry)
                sentryGuid = SentryHelper.ExceptionHandler(e, eT == ErrorType.Unhandled ? 
                                                  SentryHelper.ExceptionType.UnhandledOther : SentryHelper.ExceptionType.Handled);
            SentryErrorId = sentryGuid;
            Invoker.SendException(e, eT);
        } 
        
        public static void SendWarning(Exception e, ErrorType eT = ErrorType.Warning) =>
            Invoker.SendException(Exception = e, eT);
        
        public static void SendExceptionWithoutPage(Exception e, ErrorType eT = ErrorType.Unhandled)
        {
            SentryHelper.ExceptionHandler(e, eT == ErrorType.Unhandled ? SentryHelper.ExceptionType.UnhandledOther : SentryHelper.ExceptionType.Handled);
            Exception = e;
            ExceptionContent = e!.ToString();
            ExceptionType = eT;
            SetPageTitle(eT);
        }

        public static void SetPageTitle(ErrorType errorType)
        {
            var locUnhandledException = Lang!._UnhandledExceptionPage!;
            switch (errorType)
            {
                case ErrorType.Unhandled:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle1;
                    ExceptionSubtitle = locUnhandledException.UnhandledTitle1;
                    break;
                case ErrorType.Connection:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle2;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubtitle2;
                    break;
                case ErrorType.GameError:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle3;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubtitle3;
                    break;
                case ErrorType.Warning:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle4;
                    ExceptionSubtitle = locUnhandledException.WarningSubtitle;
                    break;
                case ErrorType.DiskCrc:
                    ExceptionTitle    = locUnhandledException.UnhandledTitleDiskCrc;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubDiskCrc;
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
            ExceptionString              = e?.ToString() ?? string.Empty;
            ErrorSender.ExceptionContent = ExceptionString;
            ErrorSender.ExceptionType    = errorType;
            ErrorSender.SetPageTitle(errorType);
        }
        public  Exception Exception       { get; private set; }
        private string    ExceptionString { get; }
    }
    #endregion
    #region MainFrameRegion
    internal static class MainFrameChanger
    {
        private static          Type                    _currentWindow;
        private static          Type                    _currentPage;
        private static readonly MainFrameChangerInvoker Invoker = new();
        public static void GoBackWindowFrame() => Invoker.GoBackWindowFrame();
        public static void ChangeWindowFrame(Type toPage, bool requireCacheReset = false) => ChangeWindowFrame(toPage, new DrillInNavigationTransitionInfo());
        public static void ChangeWindowFrame(Type toPage, NavigationTransitionInfo transition, bool requireCacheReset = false)
        {
            _currentWindow = toPage ?? throw new ArgumentException("Cannot navigate to a null page!");
            Invoker.ChangeWindowFrame(toPage, transition, requireCacheReset);
        }

        public static void GoBackMainFrame() => Invoker.GoBackMainFrame();
        public static void ChangeMainFrame(Type toPage, bool requireCacheReset = false) => ChangeMainFrame(toPage, new DrillInNavigationTransitionInfo(), requireCacheReset);
        public static void ChangeMainFrame(Type toPage, NavigationTransitionInfo transition, bool requireCacheReset = false)
        {
            _currentPage = toPage ?? throw new ArgumentException("Cannot navigate to a null page!");
            Invoker!.ChangeMainFrame(toPage, transition, requireCacheReset);
        }

        public static void ReloadCurrentWindowFrame() => ChangeWindowFrame(_currentWindow);
        public static void ReloadCurrentMainFrame() => ChangeMainFrame(_currentPage);
    }

    internal class MainFrameChangerInvoker
    {
        public static event EventHandler<MainFrameProperties> WindowFrameEvent;
        public static event EventHandler<MainFrameProperties> FrameEvent;
        public static event EventHandler WindowFrameGoBackEvent;
        public static event EventHandler FrameGoBackEvent;
        public void ChangeWindowFrame(Type e, NavigationTransitionInfo eT, bool requireCacheReset = false) => WindowFrameEvent?.Invoke(this, new MainFrameProperties(e, eT, requireCacheReset));
        public void ChangeMainFrame(Type e, NavigationTransitionInfo eT, bool requireCacheReset = false) => FrameEvent?.Invoke(this, new MainFrameProperties(e, eT, requireCacheReset));
        public void GoBackWindowFrame() => WindowFrameGoBackEvent?.Invoke(this, null);
        public void GoBackMainFrame() => FrameGoBackEvent?.Invoke(this, null);
    }

    internal class MainFrameProperties
    {
        internal MainFrameProperties(Type frameTo, NavigationTransitionInfo transition, bool requireCacheReset)
        {
            FrameTo           = frameTo;
            Transition        = transition;
            RequireCacheReset = requireCacheReset;
        }
        public Type FrameTo { get; private set; }
        public NavigationTransitionInfo Transition { get; private set; }
        public bool RequireCacheReset { get; private set; }
    }
    #endregion
    #region NotificationPushRegion
    internal static class NotificationSender
    {
        private static readonly NotificationInvoker Invoker = new();
        public static           void                SendNotification(NotificationInvokerProp e) => Invoker.SendNotification(e);
        public static void SendCustomNotification(int tagID, InfoBar infoBarUI) => Invoker.SendNotification(new NotificationInvokerProp
        {
            IsCustomNotif = true,
            CustomNotifAction = NotificationCustomAction.Add,
            Notification = new NotificationProp
            {
                MsgId = tagID
            },
            OtherContent = infoBarUI
        });
        public static void RemoveCustomNotification(int tagID) => Invoker.SendNotification(new NotificationInvokerProp
        {
            IsCustomNotif = true,
            CustomNotifAction = NotificationCustomAction.Remove,
            Notification = new NotificationProp
            {
                MsgId = tagID
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
        public TypedEventHandler<InfoBar, object> CloseAction { get; init; }
        public FrameworkElement OtherContent { get; set; }
        public NotificationProp Notification { get; init; }
        public bool IsAppNotif { get; init; } = true;
        public bool IsCustomNotif { get; init; }
        public NotificationCustomAction CustomNotifAction { get; init; }

    }
    #endregion
    #region SpawnWebView2Region
    internal static class SpawnWebView2
    {
        private static readonly SpawnWebView2Invoker Invoker = new();
        public static void SpawnWebView2Window(string url, UIElement parentUI)
        {
            if (GetAppConfigValue("UseExternalBrowser").ToBool())
            {
                if (string.IsNullOrEmpty(url)) return;
                parentUI!.DispatcherQueue!.TryEnqueue(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                });
            }
            else Invoker!.SpawnWebView2Window(url);
        }
    }

    internal class SpawnWebView2Invoker
    {
        public static event EventHandler<SpawnWebView2Property> SpawnEvent;
        public void SpawnWebView2Window(string url) => SpawnEvent?.Invoke(this, new SpawnWebView2Property(url));
    }

    internal class SpawnWebView2Property
    {
        internal SpawnWebView2Property(string url) => URL = url;

        public string URL { get; }
    }
    #endregion
    #region ShowLoadingPage
    internal static class ShowLoadingPage
    {
        private static readonly ShowLoadingPageInvoker Invoker = new();
        public static           void                   ShowLoading(string title, string subtitle, bool hide = false) => Invoker.ShowLoading(hide, title, subtitle);
    }

    internal class ShowLoadingPageInvoker
    {
        public static event EventHandler<ShowLoadingPageProperty> PageEvent;
        public void ShowLoading(bool hide, string title, string subtitle) => PageEvent?.Invoke(this, new ShowLoadingPageProperty(hide, title, subtitle));
    }

    internal class ShowLoadingPageProperty
    {
        internal ShowLoadingPageProperty(bool hide, string title, string subtitle)
        {
            Hide = hide;
            Title = title;
            Subtitle = subtitle;
        }
        public bool Hide { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }
    }
    #endregion
    #region ChangeTitleDragArea
    [Flags]
    public enum DragAreaTemplate
    {
        None = 0,
        Full = 1,
        Default = 2,

        OverlayOpened = 4
    }

    internal static class ChangeTitleDragArea
    {
        private static readonly ChangeTitleDragAreaInvoker Invoker = new();
        public static DragAreaTemplate CurrentDragAreaType;

        public static void UpdateLayout() => Change(CurrentDragAreaType);

        public static void Change(DragAreaTemplate template)
        {
            // Ensure to keep the overlay opened even if the drag area type is changed.
            if (CurrentDragAreaType.HasFlag(DragAreaTemplate.OverlayOpened) &&
                FullPageOverlay.CurrentlyOpenedOverlays.Count > 0)
            {
                template |= DragAreaTemplate.OverlayOpened;
            }

            Invoker.Change(CurrentDragAreaType = template);
        }
    }

    internal class ChangeTitleDragAreaInvoker
    {
        public static event EventHandler<ChangeTitleDragAreaProperty> TitleBarEvent;
        public void Change(DragAreaTemplate template) => TitleBarEvent?.Invoke(this, new ChangeTitleDragAreaProperty(template));
    }

    internal class ChangeTitleDragAreaProperty
    {
        internal ChangeTitleDragAreaProperty(DragAreaTemplate template)
        {
            Template = template;
        }

        public DragAreaTemplate Template { get; private set; }
    }
    #endregion
    #region UpdateBindings
    internal static class UpdateBindings
    {
        private static readonly UpdateBindingsInvoker Invoker = new();
        public static   void                  Update() => Invoker!.Update();
    }

    internal class UpdateBindingsInvoker
    {
        private static readonly EventArgs DummyArgs = EventArgs.Empty;
        public static event EventHandler  UpdateEvents;
        public void                       Update() => UpdateEvents?.Invoke(this, DummyArgs);
    }
    #endregion
}
