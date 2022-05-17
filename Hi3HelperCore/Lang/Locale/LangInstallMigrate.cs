namespace Hi3Helper
{
    public static partial class Locale
    {
        #region InstallMigrate
        public partial class LocalizationParams
        {
            public LangInstallMigrate _InstallMigrate { get; set; } = LangFallback?._InstallMigrate;
            public class LangInstallMigrate
            {
                public string PageTitle { get; set; } = LangFallback?._InstallMigrate.PageTitle;
                public string Step1Title { get; set; } = LangFallback?._InstallMigrate.Step1Title;
                public string Step2Title { get; set; } = LangFallback?._InstallMigrate.Step2Title;
                public string Step3Title { get; set; } = LangFallback?._InstallMigrate.Step3Title;
                public string StepCancelledTitle { get; set; } = LangFallback?._InstallMigrate.StepCancelledTitle;
                public string PageFooter1 { get; set; } = LangFallback?._InstallMigrate.PageFooter1;
                public string PageFooter2 { get; set; } = LangFallback?._InstallMigrate.PageFooter2;
                public string PageFooter3 { get; set; } = LangFallback?._InstallMigrate.PageFooter3;
            }
        }
        #endregion
    }
}
