using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region StartupPage
        public sealed partial class LocalizationParams
        {
            public LangStartupPage _StartupPage { get; set; } = LangFallback?._StartupPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangStartupPage
            {
                public string SelectLang { get; set; } = LangFallback?._StartupPage.SelectLang;
                public string SelectLangDesc { get; set; } = LangFallback?._StartupPage.SelectLangDesc;
                public string SelectWindowSize { get; set; } = LangFallback?._StartupPage.SelectWindowSize;
                public string SelectCDN { get; set; } = LangFallback?._StartupPage.SelectCDN;
                public string CDNHelpTitle_1 { get; set; } = LangFallback?._StartupPage.CDNHelpTitle_1;
                public string CDNHelpTitle_2 { get; set; } = LangFallback?._StartupPage.CDNHelpTitle_2;
                public string CDNHelpTitle_3 { get; set; } = LangFallback?._StartupPage.CDNHelpTitle_3;
                public string CDNHelpTitle_4 { get; set; } = LangFallback?._StartupPage.CDNHelpTitle_4;
                public string CDNHelpDetail_1 { get; set; } = LangFallback?._StartupPage.CDNHelpDetail_1;
                public string CDNHelpDetail_2 { get; set; } = LangFallback?._StartupPage.CDNHelpDetail_2;
                public string CDNHelpDetail_3 { get; set; } = LangFallback?._StartupPage.CDNHelpDetail_3;
                public string CDNHelpDetail_4 { get; set; } = LangFallback?._StartupPage.CDNHelpDetail_4;
                public string CDNsAvailable { get; set; } = LangFallback?._StartupPage.CDNsAvailable;
                public string SplashArt_1 { get; set; } = LangFallback?._StartupPage.SplashArt_1;
                public string SplashArt_2 { get; set; } = LangFallback?._StartupPage.SplashArt_2;
                public string PageTitle { get; set; } = LangFallback?._StartupPage.PageTitle;
                public string Title1 { get; set; } = LangFallback?._StartupPage.Title1;
                public string Title2 { get; set; } = LangFallback?._StartupPage.Title2;
                public string Subtitle1 { get; set; } = LangFallback?._StartupPage.Subtitle1;
                public string Subtitle2 { get; set; } = LangFallback?._StartupPage.Subtitle2;
                public string Subtitle3 { get; set; } = LangFallback?._StartupPage.Subtitle3;
                public string Subtitle4_1 { get; set; } = LangFallback?._StartupPage.Subtitle4_1;
                public string Subtitle4_2 { get; set; } = LangFallback?._StartupPage.Subtitle4_2;
                public string Subtitle4_3 { get; set; } = LangFallback?._StartupPage.Subtitle4_3;
                public string FolderInsufficientPermission { get; set; } = LangFallback?._StartupPage.FolderInsufficientPermission;
                public string FolderNotSelected { get; set; } = LangFallback?._StartupPage.FolderNotSelected;
                public string ChooseFolderBtn { get; set; } = LangFallback?._StartupPage.ChooseFolderBtn;
                public string ChooseFolderDialogTitle { get; set; } = LangFallback?._StartupPage.ChooseFolderDialogTitle;
                public string ChooseFolderDialogSubtitle { get; set; } = LangFallback?._StartupPage.ChooseFolderDialogSubtitle;
                public string ChooseFolderDialogCancel { get; set; } = LangFallback?._StartupPage.ChooseFolderDialogCancel;
                public string ChooseFolderDialogPrimary { get; set; } = LangFallback?._StartupPage.ChooseFolderDialogPrimary;
                public string ChooseFolderDialogSecondary { get; set; } = LangFallback?._StartupPage.ChooseFolderDialogSecondary;
                public string OverlayPrepareFolderTitle { get; set; } = LangFallback?._StartupPage.OverlayPrepareFolderTitle;
                public string OverlayPrepareFolderSubtitle { get; set; } = LangFallback?._StartupPage.OverlayPrepareFolderSubtitle;
                public string Pg1NextBtn { get; set; } = LangFallback?._StartupPage.Pg1NextBtn;
                public string Pg1LoadingTitle1 { get; set; } = LangFallback?._StartupPage.Pg1LoadingTitle1;
                public string Pg1LoadingSubitle1 { get; set; } = LangFallback?._StartupPage.Pg1LoadingSubitle1;
                public string Pg1LoadingSubitle2 { get; set; } = LangFallback?._StartupPage.Pg1LoadingSubitle2;
                public string Pg2NextBtn { get; set; } = LangFallback?._StartupPage.Pg2NextBtn;
                public string Pg2PrevBtn { get; set; } = LangFallback?._StartupPage.Pg2PrevBtn;
                public string Pg2PrevBtnNew { get; set; } = LangFallback?._StartupPage.Pg2PrevBtnNew;
                public string Pg2Title { get; set; } = LangFallback?._StartupPage.Pg2Title;
                public string Pg2Subtitle1_1 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle1_1;
                public string Pg2Subtitle1_2 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle1_2;
                public string Pg2Subtitle1_3 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle1_3;
                public string Pg2Subtitle2_1 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle2_1;
                public string Pg2Subtitle2_2 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle2_2;
                public string Pg2Subtitle2_3 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle2_3;
                public string Pg2Subtitle2_4 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle2_4;
                public string Pg2Subtitle2_5 { get; set; } = LangFallback?._StartupPage.Pg2Subtitle2_5;
                public string Pg2ComboBox { get; set; } = LangFallback?._StartupPage.Pg2ComboBox;
                public string Pg2ComboBoxRegion { get; set; } = LangFallback?._StartupPage.Pg2ComboBoxRegion;
            }
        }
        #endregion
    }
}
