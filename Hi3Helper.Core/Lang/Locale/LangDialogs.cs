using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region Dialogs
        public sealed partial class LocalizationParams
        {
            public LangDialogs _Dialogs { get; set; } = LangFallback?._Dialogs;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangDialogs
            {
                public string DeltaPatchDetectedTitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
                public string DeltaPatchDetectedSubtitle { get; set; } = LangFallback?._Dialogs.PreloadVerifiedTitle;
                public string DeltaPatchPrevFailedTitle { get; set; } = LangFallback?._Dialogs.DeltaPatchPrevFailedTitle;
                public string DeltaPatchPrevFailedSubtitle { get; set; } = LangFallback?._Dialogs.DeltaPatchPrevFailedSubtitle;
                public string DeltaPatchPreReqTitle { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqTitle;
                public string DeltaPatchPreReqSubtitle1 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle1;
                public string DeltaPatchPreReqSubtitle2 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle2;
                public string DeltaPatchPreReqSubtitle3 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle3;
                public string DeltaPatchPreReqSubtitle4 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle4;
                public string DeltaPatchPreReqSubtitle5 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle5;
                public string DeltaPatchPreReqSubtitle6 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle6;
                public string DeltaPatchPreReqSubtitle7 { get; set; } = LangFallback?._Dialogs.DeltaPatchPreReqSubtitle7;
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
                public string MigrateExistingMoveDirectoryTitle { get; set; } = LangFallback?._Dialogs.MigrateExistingMoveDirectoryTitle;
                public string MigrateExistingInstallChoiceTitle { get; set; } = LangFallback?._Dialogs.MigrateExistingInstallChoiceTitle;
                public string MigrateExistingInstallChoiceSubtitle1 { get; set; } = LangFallback?._Dialogs.MigrateExistingInstallChoiceSubtitle1;
                public string MigrateExistingInstallChoiceSubtitle2 { get; set; } = LangFallback?._Dialogs.MigrateExistingInstallChoiceSubtitle2;
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
                public string InstallCorruptDataAnywayTitle { get; set; } = LangFallback?._Dialogs.InstallCorruptDataAnywayTitle;
                public string InstallCorruptDataAnywaySubtitle1 { get; set; } = LangFallback?._Dialogs.InstallCorruptDataAnywaySubtitle1;
                public string InstallCorruptDataAnywaySubtitle2 { get; set; } = LangFallback?._Dialogs.InstallCorruptDataAnywaySubtitle2;
                public string InstallCorruptDataAnywaySubtitle3 { get; set; } = LangFallback?._Dialogs.InstallCorruptDataAnywaySubtitle3;
                public string InstallDataDownloadResumeTitle { get; set; } = LangFallback?._Dialogs.InstallDataDownloadResumeTitle;
                public string InstallDataDownloadResumeSubtitle { get; set; } = LangFallback?._Dialogs.InstallDataDownloadResumeSubtitle;
                public string InsufficientDiskTitle { get; set; } = LangFallback?._Dialogs.InsufficientDiskTitle;
                public string InsufficientDiskSubtitle { get; set; } = LangFallback?._Dialogs.InsufficientDiskSubtitle;
                public string RelocateFolderTitle { get; set; } = LangFallback?._Dialogs.RelocateFolderTitle;
                public string RelocateFolderSubtitle { get; set; } = LangFallback?._Dialogs.RelocateFolderSubtitle;
                public string UninstallGameTitle { get; set; } = LangFallback?._Dialogs.UninstallGameTitle;
                public string UninstallGameSubtitle { get; set; } = LangFallback?._Dialogs.UninstallGameSubtitle;
                public string ChangePlaytimeTitle { get; set; } = LangFallback?._Dialogs.ChangePlaytimeTitle;
                public string ChangePlaytimeSubtitle { get; set; } = LangFallback?._Dialogs.ChangePlaytimeSubtitle;
                public string ResetPlaytimeTitle { get; set; } = LangFallback?._Dialogs.ResetPlaytimeTitle;
                public string ResetPlaytimeSubtitle { get; set; } = LangFallback?._Dialogs.ResetPlaytimeSubtitle;
                public string ResetPlaytimeSubtitle2 { get; set; } = LangFallback?._Dialogs.ResetPlaytimeSubtitle2;
                public string ResetPlaytimeSubtitle3 { get; set; } = LangFallback?._Dialogs.ResetPlaytimeSubtitle3;
                public string InvalidPlaytimeTitle { get; set; } = LangFallback?._Dialogs.ResetPlaytimeSubtitle;
                public string InvalidPlaytimeSubtitle1 { get; set; } = LangFallback?._Dialogs.InvalidPlaytimeSubtitle1;
                public string InvalidPlaytimeSubtitle2 { get; set; } = LangFallback?._Dialogs.InvalidPlaytimeSubtitle2;
                public string InvalidPlaytimeSubtitle3 { get; set; } = LangFallback?._Dialogs.InvalidPlaytimeSubtitle3;
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
                public string ForceUpdateCurrentInstallTitle { get; set; } = LangFallback?._Dialogs.ForceUpdateCurrentInstallTitle;
                public string ForceUpdateCurrentInstallSubtitle1 { get; set; } = LangFallback?._Dialogs.ForceUpdateCurrentInstallSubtitle1;
                public string ForceUpdateCurrentInstallSubtitle2 { get; set; } = LangFallback?._Dialogs.ForceUpdateCurrentInstallSubtitle2;
                public string LocateExePathTitle { get; set; } = LangFallback?._Dialogs.LocateInstallTitle;
                public string CannotUseAppLocationForGameDirTitle { get; set; } = LangFallback?._Dialogs.CannotUseAppLocationForGameDirTitle;
                public string CannotUseAppLocationForGameDirSubtitle { get; set; } = LangFallback?._Dialogs.CannotUseAppLocationForGameDirSubtitle;
                public string InvalidGameDirNewTitleFormat { get; set; } = LangFallback?._Dialogs.InvalidGameDirNewTitleFormat;
                public string InvalidGameDirNewSubtitleSelectedPath { get; set; } = LangFallback?._Dialogs.InvalidGameDirNewSubtitleSelectedPath;
                public string InvalidGameDirNewSubtitleSelectOther { get; set; } = LangFallback?._Dialogs.InvalidGameDirNewSubtitleSelectOther;
                public string InvalidGameDirNew1Title { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew1Title;
                public string InvalidGameDirNew1Subtitle { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew1Subtitle;
                public string InvalidGameDirNew2Title { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew2Title;
                public string InvalidGameDirNew2Subtitle { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew2Subtitle;
                public string InvalidGameDirNew3Title { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew3Title;
                public string InvalidGameDirNew3Subtitle { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew3Subtitle;
                public string InvalidGameDirNew4Title { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew4Title;
                public string InvalidGameDirNew4Subtitle { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew4Subtitle;
                public string InvalidGameDirNew5Title { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew5Title;
                public string InvalidGameDirNew5Subtitle { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew5Subtitle;
                public string InvalidGameDirNew6Title { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew6Title;
                public string InvalidGameDirNew6Subtitle { get; set; } = LangFallback?._Dialogs.InvalidGameDirNew6Subtitle;
                public string FolderDialogTitle1 { get; set; } = LangFallback?._Dialogs.FolderDialogTitle1;
                public string LocateExePathSubtitle { get; set; } = LangFallback?._Dialogs.LocateExePathSubtitle;
                public string StopGameTitle { get; set; } = LangFallback?._Dialogs.StopGameTitle;
                public string StopGameSubtitle { get; set; } = LangFallback?._Dialogs.StopGameSubtitle;
                public string MeteredConnectionWarningTitle { get; set; } = LangFallback?._Dialogs.MeteredConnectionWarningTitle;
                public string MeteredConnectionWarningSubtitle { get; set; } = LangFallback?._Dialogs.MeteredConnectionWarningSubtitle;
                public string ResetKbShortcutsTitle { get; set; } = LangFallback?._Dialogs.ResetKbShortcutsTitle;
                public string ResetKbShortcutsSubtitle { get; set; } = LangFallback?._Dialogs.ResetKbShortcutsSubtitle;
                public string ShortcutCreationConfirmTitle { get; set; } = LangFallback?._Dialogs.ShortcutCreationConfirmTitle;
                public string ShortcutCreationConfirmSubtitle1 { get; set; } = LangFallback?._Dialogs.ShortcutCreationConfirmSubtitle1;
                public string ShortcutCreationConfirmSubtitle2 { get; set; } = LangFallback?._Dialogs.ShortcutCreationConfirmSubtitle2;
                public string ShortcutCreationConfirmCheckBox { get; set; } = LangFallback?._Dialogs.ShortcutCreationConfirmCheckBox;
                public string ShortcutCreationSuccessTitle { get; set; } = LangFallback?._Dialogs.ShortcutCreationSuccessTitle;
                public string ShortcutCreationSuccessSubtitle1 { get; set; } = LangFallback?._Dialogs.ShortcutCreationSuccessSubtitle1;
                public string ShortcutCreationSuccessSubtitle2 { get; set; } = LangFallback?._Dialogs.ShortcutCreationSuccessSubtitle2;
                public string ShortcutCreationSuccessSubtitle3 { get; set; } = LangFallback?._Dialogs.ShortcutCreationSuccessSubtitle3;
                public string ShortcutCreationSuccessSubtitle4 { get; set; } = LangFallback?._Dialogs.ShortcutCreationSuccessSubtitle4;
                public string ShortcutCreationSuccessSubtitle5 { get; set; } = LangFallback?._Dialogs.ShortcutCreationSuccessSubtitle5;
                public string SteamShortcutCreationConfirmTitle { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationConfirmTitle;
                public string SteamShortcutCreationConfirmSubtitle1 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationConfirmSubtitle1;
                public string SteamShortcutCreationConfirmSubtitle2 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationConfirmSubtitle2;
                public string SteamShortcutCreationConfirmCheckBox { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationConfirmCheckBox;
                public string SteamShortcutCreationSuccessTitle { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessTitle;
                public string SteamShortcutCreationSuccessSubtitle1 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle1;
                public string SteamShortcutCreationSuccessSubtitle2 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle2;
                public string SteamShortcutCreationSuccessSubtitle3 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle3;
                public string SteamShortcutCreationSuccessSubtitle4 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle4;
                public string SteamShortcutCreationSuccessSubtitle5 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle5;
                public string SteamShortcutCreationSuccessSubtitle6 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle6;
                public string SteamShortcutCreationSuccessSubtitle7 { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationSuccessSubtitle7;
                public string SteamShortcutCreationFailureTitle { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationFailureTitle;
                public string SteamShortcutCreationFailureSubtitle { get; set; } = LangFallback?._Dialogs.SteamShortcutCreationFailureSubtitle;
                public string SteamShortcutTitle { get; set; } = LangFallback?._Dialogs.SteamShortcutTitle;
                public string SteamShortcutDownloadingImages { get; set; } = LangFallback?._Dialogs.SteamShortcutDownloadingImages;
                public string OperationErrorDiskSpaceInsufficientTitle { get; set; } = LangFallback?._Dialogs.OperationErrorDiskSpaceInsufficientTitle;
                public string OperationErrorDiskSpaceInsufficientMsg { get; set; } = LangFallback?._Dialogs.OperationErrorDiskSpaceInsufficientMsg;
                public string OperationWarningNotCancellableTitle { get; set; } = LangFallback?._Dialogs.OperationWarningNotCancellableTitle;
                public string OperationWarningNotCancellableMsg1 { get; set; } = LangFallback?._Dialogs.OperationWarningNotCancellableMsg1;
                public string OperationWarningNotCancellableMsg2 { get; set; } = LangFallback?._Dialogs.OperationWarningNotCancellableMsg2;
                public string OperationWarningNotCancellableMsg3 { get; set; } = LangFallback?._Dialogs.OperationWarningNotCancellableMsg3;
                public string OperationWarningNotCancellableMsg4 { get; set; } = LangFallback?._Dialogs.OperationWarningNotCancellableMsg4;
                public string OperationWarningNotCancellableMsg5 { get; set; } = LangFallback?._Dialogs.OperationWarningNotCancellableMsg5;
                public string DownloadSettingsTitle { get; set; } = LangFallback?._Dialogs.DownloadSettingsTitle;
                public string DownloadSettingsOption1 { get; set; } = LangFallback?._Dialogs.DownloadSettingsOption1;
                public string OpenInExternalBrowser { get; set; } = LangFallback?._Dialogs.OpenInExternalBrowser;
                public string CloseOverlay { get; set; } = LangFallback?._Dialogs.CloseOverlay;

                public string DbGenerateUid_Title   { get; set; } = LangFallback?._Dialogs.DbGenerateUid_Title;
                public string DbGenerateUid_Content { get; set; } = LangFallback?._Dialogs.DbGenerateUid_Content;
                public string SophonIncrementUpdateUnavailTitle { get; set; } = LangFallback?._Dialogs.SophonIncrementUpdateUnavailTitle;
                public string SophonIncrementUpdateUnavailSubtitle1 { get; set; } = LangFallback?._Dialogs.SophonIncrementUpdateUnavailSubtitle1;
                public string SophonIncrementUpdateUnavailSubtitle2 { get; set; } = LangFallback?._Dialogs.SophonIncrementUpdateUnavailSubtitle2;
                public string SophonIncrementUpdateUnavailSubtitle3 { get; set; } = LangFallback?._Dialogs.SophonIncrementUpdateUnavailSubtitle3;
                public string SophonIncrementUpdateUnavailSubtitle4 { get; set; } = LangFallback?._Dialogs.SophonIncrementUpdateUnavailSubtitle4;
                public string UACWarningTitle { get; set; } = LangFallback?._Dialogs.UACWarningTitle;
                public string UACWarningContent { get; set; } = LangFallback?._Dialogs.UACWarningContent;
                public string UACWarningLearnMore { get; set; } = LangFallback?._Dialogs.UACWarningLearnMore;
                public string UACWarningDontShowAgain { get; set; } = LangFallback?._Dialogs.UACWarningDontShowAgain;
                public string EnsureExitTitle { get; set; } = LangFallback?._Dialogs.EnsureExitTitle;
                public string EnsureExitSubtitle { get; set; } = LangFallback?._Dialogs.EnsureExitSubtitle;
                
                public string UserFeedback_DialogTitle { get; set; } = LangFallback?._Dialogs.UserFeedback_DialogTitle;
                public string UserFeedback_TextFieldTitleHeader { get; set; } = LangFallback?._Dialogs.UserFeedback_TextFieldTitleHeader;
                public string UserFeedback_TextFieldTitlePlaceholder { get; set; } = LangFallback?._Dialogs.UserFeedback_TextFieldTitlePlaceholder;
                public string UserFeedback_TextFieldMessageHeader { get; set; } = LangFallback?._Dialogs.UserFeedback_TextFieldMessageHeader;
                public string UserFeedback_TextFieldMessagePlaceholder { get; set; } = LangFallback?._Dialogs.UserFeedback_TextFieldMessagePlaceholder;
                public string UserFeedback_TextFieldRequired { get; set; } = LangFallback?._Dialogs.UserFeedback_TextFieldRequired;
                public string UserFeedback_RatingText { get; set; } = LangFallback?._Dialogs.UserFeedback_RatingText;
                public string UserFeedback_CancelBtn { get; set; } = LangFallback?._Dialogs.UserFeedback_CancelBtn;
                public string UserFeedback_SubmitBtn { get; set; } = LangFallback?._Dialogs.UserFeedback_SubmitBtn;
                public string UserFeedback_SubmitBtn_Processing { get; set; } = LangFallback?._Dialogs.UserFeedback_SubmitBtn_Processing;
                public string UserFeedback_SubmitBtn_Completed { get; set; } = LangFallback?._Dialogs.UserFeedback_SubmitBtn_Completed;
                public string UserFeedback_SubmitBtn_Cancelled { get; set; } = LangFallback?._Dialogs.UserFeedback_SubmitBtn_Cancelled;
            }
        }
        #endregion
    }
}
