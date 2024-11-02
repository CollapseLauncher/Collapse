using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region InstallManagement
        public sealed partial class LocalizationParams
        {
            public LangInstallManagement _InstallMgmt { get; set; } = LangFallback?._InstallMgmt;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangInstallManagement
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
