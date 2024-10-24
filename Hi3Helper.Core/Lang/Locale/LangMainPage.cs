using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region MainPage
        public sealed partial class LocalizationParams
        {
            public LangMainPage _MainPage { get; set; } = LangFallback?._MainPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangMainPage
            {
                public string PageTitle { get; set; } = LangFallback?._MainPage.PageTitle;
                public string RegionChangeConfirm { get; set; } = LangFallback?._MainPage.RegionChangeConfirm;
                public string RegionChangeConfirmBtn { get; set; } = LangFallback?._MainPage.RegionChangeConfirmBtn;
                public string RegionChangeWarnTitle { get; set; } = LangFallback?._MainPage.RegionChangeWarnTitle;
                public string RegionChangeWarnExper1 { get; set; } = LangFallback?._MainPage.RegionChangeWarnExper1;
                public string RegionLoadingTitle { get; set; } = LangFallback?._MainPage.RegionLoadingTitle;
                public string RegionLoadingAPITitle1 { get; set; } = LangFallback?._MainPage.RegionLoadingAPITitle1;
                public string RegionLoadingAPITitle2 { get; set; } = LangFallback?._MainPage.RegionLoadingAPITitle2;
                public string RegionLoadingSubtitleTimeOut { get; set; } = LangFallback?._MainPage.RegionLoadingSubtitleTimeOut;
                public string RegionLoadingSubtitleTooLong { get; set; } = LangFallback?._MainPage.RegionLoadingSubtitleTooLong;
                public string NotifNeverAsk { get; set; } = LangFallback?._MainPage.NotifNeverAsk;
                public string NotifNoNewNotifs { get; set; } = LangFallback?._MainPage.NotifNoNewNotifs;
                public string NotifClearAll { get; set; } = LangFallback?._MainPage.NotifClearAll;
                public string NavigationMenu { get; set; } = LangFallback?._MainPage.NavigationMenu;
                public string NavigationUtilities { get; set; } = LangFallback?._MainPage.NavigationUtilities;
                public string Initializing { get; set; } = LangFallback?._MainPage.Initializing;
                public string LoadingLauncherMetadata { get; set; } = LangFallback?._MainPage.LoadingLauncherMetadata;
                public string LoadingGameConfiguration { get; set; } = LangFallback?._MainPage.LoadingGameConfiguration;
            }
        }
        #endregion
    }
}
