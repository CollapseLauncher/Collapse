namespace Hi3Helper
{
    public partial class Locale
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
                public string RepairFilesRequiredShowFilesBtn { get; set; } = LangFallback?._InstallMgmt.RepairFilesRequiredShowFilesBtn;
                public string RepairFilesRequiredTitle { get; set; } = LangFallback?._InstallMgmt.RepairFilesRequiredTitle;
                public string RepairFilesRequiredSubtitle { get; set; } = LangFallback?._InstallMgmt.RepairFilesRequiredSubtitle;
            }
        }
        #endregion
    }
}
