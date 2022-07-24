namespace Hi3Helper
{
    public static partial class Locale
    {
        #region MainPage
        public partial class LocalizationParams
        {
            public LangMainPage _MainPage { get; set; } = LangFallback?._MainPage;
            public class LangMainPage
            {
                public string PageTitle { get; set; } = LangFallback?._MainPage.PageTitle;
                public string RegionChangeConfirm { get; set; } = LangFallback?._MainPage.RegionChangeConfirm;
                public string RegionChangeConfirmBtn { get; set; } = LangFallback?._MainPage.RegionChangeConfirmBtn;
                public string RegionLoadingTitle { get; set; } = LangFallback?._MainPage.RegionLoadingTitle;
                public string RegionLoadingAPITitle1 { get; set; } = LangFallback?._MainPage.RegionLoadingAPITitle1;
                public string RegionLoadingAPITitle2 { get; set; } = LangFallback?._MainPage.RegionLoadingAPITitle2;
                public string RegionLoadingSubtitleTimeOut { get; set; } = LangFallback?._MainPage.RegionLoadingSubtitleTimeOut;
                public string RegionLoadingSubtitleTooLong { get; set; } = LangFallback?._MainPage.RegionLoadingSubtitleTooLong;
                public string NotifNeverAsk { get; set; } = LangFallback?._MainPage.NotifNeverAsk;
                public string MetadataUpdateTitle { get; set; } = LangFallback?._MainPage.MetadataUpdateTitle;
                public string MetadataUpdateSubtitle { get; set; } = LangFallback?._MainPage.MetadataUpdateSubtitle;
                public string MetadataUpdateBtn { get; set; } = LangFallback?._MainPage.MetadataUpdateBtn;
                public string MetadataUpdateBtnUpdating { get; set; } = LangFallback?._MainPage.MetadataUpdateBtnUpdating;
            }
        }
        #endregion
    }
}
