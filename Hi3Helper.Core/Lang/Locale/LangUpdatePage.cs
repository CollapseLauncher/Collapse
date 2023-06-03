namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region UpdatePage
        public sealed partial class LocalizationParams
        {
            public LangUpdatePage _UpdatePage { get; set; } = LangFallback?._UpdatePage;
            public sealed class LangUpdatePage
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
                public string UpdateHeader1 { get; set; } = LangFallback?._UpdatePage.UpdateHeader1;
                public string UpdateHeader2 { get; set; } = LangFallback?._UpdatePage.UpdateHeader2;
                public string UpdateHeader3 { get; set; } = LangFallback?._UpdatePage.UpdateHeader3;
                public string UpdateHeader4 { get; set; } = LangFallback?._UpdatePage.UpdateHeader4;
                public string UpdateHeader5PlaceHolder { get; set; } = LangFallback?._UpdatePage.UpdateHeader5PlaceHolder;

                public string UpdateStatus1 { get; set; } = LangFallback?._UpdatePage.UpdateStatus1;
                public string UpdateMessage1 { get; set; } = LangFallback?._UpdatePage.UpdateMessage1;
                public string UpdateStatus2 { get; set; } = LangFallback?._UpdatePage.UpdateStatus2;
                public string UpdateStatus3 { get; set; } = LangFallback?._UpdatePage.UpdateStatus3;
                public string UpdateStatus4 { get; set; } = LangFallback?._UpdatePage.UpdateStatus4;
                public string UpdateMessage4 { get; set; } = LangFallback?._UpdatePage.UpdateMessage4;
                public string UpdateStatus5 { get; set; } = LangFallback?._UpdatePage.UpdateStatus5;
                public string UpdateMessage5 { get; set; } = LangFallback?._UpdatePage.UpdateMessage5;
                public string UpdateCountdownMessage1 { get; set; } = LangFallback?._UpdatePage.UpdateCountdownMessage1;
                public string UpdateCountdownMessage2 { get; set; } = LangFallback?._UpdatePage.UpdateCountdownMessage2;
                public string UpdateCountdownMessage3 { get; set; } = LangFallback?._UpdatePage.UpdateCountdownMessage3;
            }
        }
        #endregion
    }
}
