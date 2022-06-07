namespace Hi3Helper
{
    public static partial class Locale
    {
        #region Misc
        public partial class LocalizationParams
        {
            public LangDialogs _Dialogs { get; set; } = LangFallback?._Dialogs;
            public class LangDialogs
            {
                public string DeltaPatchDetectedTitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
                public string DeltaPatchDetectedSubtitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
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
            }
        }
        #endregion
    }
}
