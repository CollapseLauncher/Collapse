using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region InstallMigrateSteam
        public sealed partial class LocalizationParams
        {
            public LangInstallMigrateSteam _InstallMigrateSteam { get; set; } = LangFallback?._InstallMigrateSteam;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangInstallMigrateSteam
            {
                public string PageTitle { get; set; } = LangFallback?._InstallMigrateSteam.PageTitle;
                public string Step1Title { get; set; } = LangFallback?._InstallMigrateSteam.Step1Title;
                public string Step2Title { get; set; } = LangFallback?._InstallMigrateSteam.Step2Title;
                public string Step2Subtitle1 { get; set; } = LangFallback?._InstallMigrateSteam.Step2Subtitle1;
                public string Step2Subtitle2 { get; set; } = LangFallback?._InstallMigrateSteam.Step2Subtitle2;
                public string Step3Title { get; set; } = LangFallback?._InstallMigrateSteam.Step3Title;
                public string Step3Subtitle { get; set; } = LangFallback?._InstallMigrateSteam.Step3Subtitle;
                public string Step4Title { get; set; } = LangFallback?._InstallMigrateSteam.Step4Title;
                public string Step4Subtitle { get; set; } = LangFallback?._InstallMigrateSteam.Step4Subtitle;
                public string Step5Title { get; set; } = LangFallback?._InstallMigrateSteam.Step5Title;
                public string Step5Subtitle { get; set; } = LangFallback?._InstallMigrateSteam.Step5Subtitle;
                public string StepNotRunning { get; set; } = LangFallback?._InstallMigrateSteam.StepNotRunning;
                public string PageFooter1 { get; set; } = LangFallback?._InstallMigrateSteam.PageFooter1;
                public string PageFooter2 { get; set; } = LangFallback?._InstallMigrateSteam.PageFooter2;
                public string PageFooter3 { get; set; } = LangFallback?._InstallMigrateSteam.PageFooter3;
                public string SelectDialogTitle { get; set; } = LangFallback?._InstallMigrateSteam.SelectDialogTitle;
                public string SelectDialogSource { get; set; } = LangFallback?._InstallMigrateSteam.SelectDialogSource;
                public string SelectDialogTarget { get; set; } = LangFallback?._InstallMigrateSteam.SelectDialogTarget;
                public string SelectDialogSubtitle { get; set; } = LangFallback?._InstallMigrateSteam.SelectDialogSubtitle;
                public string SelectDialogSubtitleNotInstalled { get; set; } = LangFallback?._InstallMigrateSteam.SelectDialogSubtitleNotInstalled;
                public string ConvertSuccessTitle { get; set; } = LangFallback?._InstallMigrateSteam.ConvertSuccessTitle;
                public string ConvertSuccessSubtitle { get; set; } = LangFallback?._InstallMigrateSteam.ConvertSuccessSubtitle;
                public string InnerCheckFile { get; set; } = LangFallback?._InstallMigrateSteam.InnerCheckFile;
                public string InnerCheckBlock1 { get; set; } = LangFallback?._InstallMigrateSteam.InnerCheckBlock1;
                public string InnerCheckBlock2 { get; set; } = LangFallback?._InstallMigrateSteam.InnerCheckBlock2;
                public string InnerConvertFile1 { get; set; } = LangFallback?._InstallMigrateSteam.InnerConvertFile1;
                public string InnerConvertFile2 { get; set; } = LangFallback?._InstallMigrateSteam.InnerConvertFile2;
                public string InnerConvertFile3 { get; set; } = LangFallback?._InstallMigrateSteam.InnerConvertFile3;
            }
        }
        #endregion
    }
}
