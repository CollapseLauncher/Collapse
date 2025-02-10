using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region InstallConvert
        public sealed partial class LocalizationParams
        {
            public LangInstallConvert _InstallConvert { get; set; } = LangFallback?._InstallConvert;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangInstallConvert
            {
                public string PageTitle { get; set; } = LangFallback?._InstallConvert.PageTitle;
                public string Step1Title { get; set; } = LangFallback?._InstallConvert.Step1Title;
                public string Step2Title { get; set; } = LangFallback?._InstallConvert.Step2Title;
                public string Step2Subtitle { get; set; } = LangFallback?._InstallConvert.Step2Subtitle;
                public string Step3Title { get; set; } = LangFallback?._InstallConvert.Step3Title;
                public string Step3Title1 { get; set; } = LangFallback?._InstallConvert.Step3Title1;
                public string Step3Title2 { get; set; } = LangFallback?._InstallConvert.Step3Title2;
                public string Step3Subtitle { get; set; } = LangFallback?._InstallConvert.Step3Subtitle;
                public string Step4Title { get; set; } = LangFallback?._InstallConvert.Step4Title;
                public string Step4Subtitle { get; set; } = LangFallback?._InstallConvert.Step4Subtitle;
                public string Step5Title { get; set; } = LangFallback?._InstallConvert.Step5Title;
                public string Step5Subtitle { get; set; } = LangFallback?._InstallConvert.Step5Subtitle;
                public string StepNotRunning { get; set; } = LangFallback?._InstallConvert.StepNotRunning;
                public string PageFooter1 { get; set; } = LangFallback?._InstallConvert.PageFooter1;
                public string PageFooter2 { get; set; } = LangFallback?._InstallConvert.PageFooter2;
                public string PageFooter3 { get; set; } = LangFallback?._InstallConvert.PageFooter3;
                public string SelectDialogTitle { get; set; } = LangFallback?._InstallConvert.SelectDialogTitle;
                public string SelectDialogSource { get; set; } = LangFallback?._InstallConvert.SelectDialogSource;
                public string SelectDialogTarget { get; set; } = LangFallback?._InstallConvert.SelectDialogTarget;
                public string SelectDialogSubtitle { get; set; } = LangFallback?._InstallConvert.SelectDialogSubtitle;
                public string SelectDialogSubtitleNotInstalled { get; set; } = LangFallback?._InstallConvert.SelectDialogSubtitleNotInstalled;
                public string ConvertSuccessTitle { get; set; } = LangFallback?._InstallConvert.ConvertSuccessTitle;
                public string ConvertSuccessSubtitle { get; set; } = LangFallback?._InstallConvert.ConvertSuccessSubtitle;
                public string CookbookDownloadTitle { get; set; } = LangFallback?._InstallConvert.CookbookDownloadTitle;
                public string CookbookDownloadSubtitle { get; set; } = LangFallback?._InstallConvert.CookbookDownloadSubtitle;
                public string CookbookFileBrowserFileTypeCategory { get; set; } = LangFallback?._InstallConvert.CookbookFileBrowserFileTypeCategory;
                public string CancelBtn { get; set; } = LangFallback?._InstallConvert.CancelBtn;
                public string CancelMsgTitle { get; set; } = LangFallback?._InstallConvert.CancelMsgTitle;
                public string CancelMsgSubtitle1 { get; set; } = LangFallback?._InstallConvert.CancelMsgSubtitle1;
                public string CancelMsgSubtitle2 { get; set; } = LangFallback?._InstallConvert.CancelMsgSubtitle2;
            }
        }
        #endregion
    }
}
