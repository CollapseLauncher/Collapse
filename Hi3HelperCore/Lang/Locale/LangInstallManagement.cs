namespace Hi3Helper
{
    public static partial class Locale
    {
        #region InstallManagement
        public partial class LocalizationParams
        {
            public LangInstallManagement _InstallMgmt { get; set; } = LangFallback?._InstallMgmt;
            public class LangInstallManagement
            {
                public string IntegrityCheckTitle { get; set; } = LangFallback?._InstallMgmt.IntegrityCheckTitle;
                public string PreparePatchTitle { get; set; } = LangFallback?._InstallMgmt.PreparePatchTitle;
                public string AddtDownloadTitle { get; set; } = LangFallback?._InstallMgmt.AddtDownloadTitle;
            }
        }
        #endregion
    }
}
