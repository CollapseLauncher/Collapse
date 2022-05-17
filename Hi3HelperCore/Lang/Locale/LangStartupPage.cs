namespace Hi3Helper
{
    public static partial class Locale
    {
        #region StartupPage
        public partial class LocalizationParams
        {
            public LangStartupPage _StartupPage { get; set; } = LangFallback?._StartupPage;
            public class LangStartupPage
            {
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
            }
        }
        #endregion
    }
}
