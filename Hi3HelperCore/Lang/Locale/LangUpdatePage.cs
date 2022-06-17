namespace Hi3Helper
{
    public static partial class Locale
    {
        #region UpdatePage
        public partial class LocalizationParams
        {
            public LangUpdatePage _UpdatePage { get; set; } = LangFallback?._UpdatePage;
            public class LangUpdatePage
            {
                public string PageTitle1 { get; set; } = LangFallback?._UpdatePage.PageTitle1;
                public string PageTitle2 { get; set; } = LangFallback?._UpdatePage.PageTitle2;
                public string VerCurLabel { get; set; } = LangFallback?._UpdatePage.VerCurLabel;
                public string VerNewLabel { get; set; } = LangFallback?._UpdatePage.VerNewLabel;
                public string VerChannelLabel { get; set; } = LangFallback?._UpdatePage.VerChannelLabel;
                public string VerDateLabel { get; set; } = LangFallback?._UpdatePage.VerDateLabel;
                public string ReleaseNote { get; set; } = LangFallback?._UpdatePage.ReleaseNote;
                public string NeverShowNotification { get; set; } = LangFallback?._UpdatePage.NeverShowNotification;
                public string RemindLaterBtn { get; set; } = LangFallback?._UpdatePage.RemindLaterBtn;
                public string UpdateNowBtn { get; set; } = LangFallback?._UpdatePage.UpdateNowBtn;
                public string LoadingRelease { get; set; } = LangFallback?._UpdatePage.LoadingRelease;
                public string LoadingReleaseFailed { get; set; } = LangFallback?._UpdatePage.LoadingReleaseFailed;
            }
        }
        #endregion
    }
}
