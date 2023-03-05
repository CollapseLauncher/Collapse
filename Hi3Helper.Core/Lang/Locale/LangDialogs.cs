namespace Hi3Helper
{
    public partial class Locale
    {
        #region Dialogs
        public partial class LocalizationParams
        {
            public LangDialogs _Dialogs { get; set; } = LangFallback?._Dialogs;
            public class LangDialogs
            {
                public string DeltaPatchDetectedTitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
                public string DeltaPatchDetectedSubtitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
                public string DeltaPatchPrevFailedTitle { get; set; } = LangFallback?._Dialogs.DeltaPatchPrevFailedTitle;
                public string DeltaPatchPrevFailedSubtitle { get; set; } = LangFallback?._Dialogs.DeltaPatchPrevFailedSubtitle;
                public string GameConversionPrevFailedTitle { get; set; } = LangFallback?._Dialogs.GameConversionPrevFailedTitle;
                public string GameConversionPrevFailedSubtitle { get; set; } = LangFallback?._Dialogs.GameConversionPrevFailedSubtitle;
                public string PreloadVerifiedTitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
                public string PreloadVerifiedSubtitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedSubtitle;
                public string LocateInstallTitle { get; set; } = LangFallback?._Dialogs.LocateInstallTitle;
                public string LocateInstallSubtitle { get; set; } = LangFallback?._Dialogs.LocateInstallSubtitle;
                public string UnauthorizedDirTitle { get; set; } = LangFallback?._Dialogs.UnauthorizedDirTitle;
                public string UnauthorizedDirSubtitle { get; set; } = LangFallback?._Dialogs.UnauthorizedDirSubtitle;
                public string ChooseAudioLangSelectPlaceholder { get; set; } = LangFallback?._Dialogs.ChooseAudioLangSelectPlaceholder;
                public string ChooseAudioLangTitle { get; set; } = LangFallback?._Dialogs.ChooseAudioLangTitle;
                public string ChooseAudioLangSubtitle { get; set; } = LangFallback?._Dialogs.ChooseAudioLangSubtitle;
                public string AddtDownloadNeededTitle { get; set; } = LangFallback?._Dialogs.AddtDownloadNeededTitle;
                public string AddtDownloadNeededSubtitle { get; set; } = LangFallback?._Dialogs.AddtDownloadNeededSubtitle;
                public string AddtDownloadCompletedTitle { get; set; } = LangFallback?._Dialogs.AddtDownloadCompletedTitle;
                public string AddtDownloadCompletedSubtitle { get; set; } = LangFallback?._Dialogs.AddtDownloadCompletedSubtitle;
                public string RepairCompletedTitle { get; set; } = LangFallback?._Dialogs.RepairCompletedTitle;
                public string RepairCompletedSubtitle { get; set; } = LangFallback?._Dialogs.RepairCompletedSubtitle;
                public string RepairCompletedSubtitleNoBroken { get; set; } = LangFallback?._Dialogs.RepairCompletedSubtitleNoBroken;
                public string ExtremeGraphicsSettingsWarnTitle { get; set; } = LangFallback?._Dialogs.ExtremeGraphicsSettingsWarnTitle;
                public string ExtremeGraphicsSettingsWarnSubtitle { get; set; } = LangFallback?._Dialogs.ExtremeGraphicsSettingsWarnSubtitle;
                public string ExistingInstallTitle { get; set; } = LangFallback?._Dialogs.ExistingInstallTitle;
                public string ExistingInstallSubtitle { get; set; } = LangFallback?._Dialogs.ExistingInstallSubtitle;
                public string ExistingInstallBHI3LTitle { get; set; } = LangFallback?._Dialogs.ExistingInstallBHI3LTitle;
                public string ExistingInstallBHI3LSubtitle { get; set; } = LangFallback?._Dialogs.ExistingInstallBHI3LSubtitle;
                public string ExistingInstallSteamTitle { get; set; } = LangFallback?._Dialogs.ExistingInstallSteamTitle;
                public string ExistingInstallSteamSubtitle { get; set; } = LangFallback?._Dialogs.ExistingInstallSteamSubtitle;
                public string SteamConvertNeedMigrateTitle { get; set; } = LangFallback?._Dialogs.SteamConvertNeedMigrateTitle;
                public string SteamConvertNeedMigrateSubtitle { get; set; } = LangFallback?._Dialogs.SteamConvertNeedMigrateSubtitle;
                public string SteamConvertIntegrityDoneTitle { get; set; } = LangFallback?._Dialogs.SteamConvertIntegrityDoneTitle;
                public string SteamConvertIntegrityDoneSubtitle { get; set; } = LangFallback?._Dialogs.SteamConvertIntegrityDoneSubtitle;
                public string SteamConvertFailedTitle { get; set; } = LangFallback?._Dialogs.SteamConvertFailedTitle;
                public string SteamConvertFailedSubtitle { get; set; } = LangFallback?._Dialogs.SteamConvertFailedSubtitle;
                public string InstallDataCorruptTitle { get; set; } = LangFallback?._Dialogs.InstallDataCorruptTitle;
                public string InstallDataCorruptSubtitle { get; set; } = LangFallback?._Dialogs.InstallDataCorruptSubtitle;
                public string InstallDataDownloadResumeTitle { get; set; } = LangFallback?._Dialogs.InstallDataDownloadResumeTitle;
                public string InstallDataDownloadResumeSubtitle { get; set; } = LangFallback?._Dialogs.InstallDataDownloadResumeSubtitle;
                public string InsufficientDiskTitle { get; set; } = LangFallback?._Dialogs.InsufficientDiskTitle;
                public string InsufficientDiskSubtitle { get; set; } = LangFallback?._Dialogs.InsufficientDiskSubtitle;
                public string RelocateFolderTitle { get; set; } = LangFallback?._Dialogs.RelocateFolderTitle;
                public string RelocateFolderSubtitle { get; set; } = LangFallback?._Dialogs.RelocateFolderSubtitle;
                public string UninstallGameTitle { get; set; } = LangFallback?._Dialogs.UninstallGameTitle;
                public string UninstallGameSubtitle { get; set; } = LangFallback?._Dialogs.UninstallGameSubtitle;
                public string MigrationTitle { get; set; } = LangFallback?._Dialogs.MigrationTitle;
                public string MigrationSubtitle { get; set; } = LangFallback?._Dialogs.MigrationSubtitle;
                public string NeedInstallMediaPackTitle { get; set; } = LangFallback?._Dialogs.NeedInstallMediaPackTitle;
                public string NeedInstallMediaPackSubtitle1 { get; set; } = LangFallback?._Dialogs.NeedInstallMediaPackSubtitle1;
                public string NeedInstallMediaPackSubtitle2 { get; set; } = LangFallback?._Dialogs.NeedInstallMediaPackSubtitle2;
                public string InstallMediaPackCompleteTitle { get; set; } = LangFallback?._Dialogs.InstallMediaPackCompleteTitle;
                public string InstallMediaPackCompleteSubtitle { get; set; } = LangFallback?._Dialogs.InstallMediaPackCompleteSubtitle;
                public string InstallingMediaPackTitle { get; set; } = LangFallback?._Dialogs.InstallingMediaPackTitle;
                public string InstallingMediaPackSubtitle { get; set; } = LangFallback?._Dialogs.InstallingMediaPackSubtitle;
                public string InstallingMediaPackSubtitleFinished { get; set; } = LangFallback?._Dialogs.InstallingMediaPackSubtitleFinished;
                public string GameConfigBrokenTitle1 { get; set; } = LangFallback?._Dialogs.GameConfigBrokenTitle1;
                public string GameConfigBrokenSubtitle1 { get; set; } = LangFallback?._Dialogs.GameConfigBrokenSubtitle1;
                public string GameConfigBrokenSubtitle2 { get; set; } = LangFallback?._Dialogs.GameConfigBrokenSubtitle2;
                public string GameConfigBrokenSubtitle3 { get; set; } = LangFallback?._Dialogs.GameConfigBrokenSubtitle3;
                public string CookbookLocateTitle { get; set; } = LangFallback?._Dialogs.CookbookLocateTitle;
                public string CookbookLocateSubtitle1 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle1;
                public string CookbookLocateSubtitle2 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle2;
                public string CookbookLocateSubtitle3 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle3;
                public string CookbookLocateSubtitle4 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle4;
                public string CookbookLocateSubtitle5 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle5;
                public string CookbookLocateSubtitle6 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle6;
                public string CookbookLocateSubtitle7 { get; set; } = LangFallback?._Dialogs.CookbookLocateSubtitle7;
                public string PrivilegeMustRunTitle { get; set; } = LangFallback?._Dialogs.PrivilegeMustRunTitle;
                public string PrivilegeMustRunSubtitle { get; set; } = LangFallback?._Dialogs.PrivilegeMustRunSubtitle;
                public string ReleaseChannelChangeTitle { get; set; } = LangFallback?._Dialogs.ReleaseChannelChangeTitle;
                public string ReleaseChannelChangeSubtitle1 { get; set; } = LangFallback?._Dialogs.ReleaseChannelChangeSubtitle1;
                public string ReleaseChannelChangeSubtitle2 { get; set; } = LangFallback?._Dialogs.ReleaseChannelChangeSubtitle2;
                public string ReleaseChannelChangeSubtitle3 { get; set; } = LangFallback?._Dialogs.ReleaseChannelChangeSubtitle3;
            }
        }
        #endregion
    }
}
