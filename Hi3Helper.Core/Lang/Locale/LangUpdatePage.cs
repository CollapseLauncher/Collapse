using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region UpdatePage
        public sealed partial class LocalizationParams
        {
            public LangUpdatePage _UpdatePage { get; set; } = LangFallback?._UpdatePage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangUpdatePage
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

                public string UpdateForcedHeader { get; set; } = LangFallback?._UpdatePage.UpdateForcedHeader;

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

                public string ApplyUpdateTitle1 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTitle1;
                public string ApplyUpdateTitle2 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTitle2;
                public string ApplyUpdateCDNSelectorTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateCDNSelectorTitle;
                public string ApplyUpdateCDNSelectorSubtitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateCDNSelectorSubtitle;
                public string ApplyUpdateCDNSelectorSubtitleCount { get; set; } = LangFallback?._UpdatePage.ApplyUpdateCDNSelectorSubtitleCount;
                public string ApplyUpdateUpdateNowBtn { get; set; } = LangFallback?._UpdatePage.ApplyUpdateUpdateNowBtn;

                public string ApplyUpdateUpdateVersionTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateUpdateVersionTitle;
                public string ApplyUpdateUpdateChannelTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateUpdateChannelTitle;
                public string ApplyUpdateUpdateChannelSubtitlePlaceholder { get; set; } = LangFallback?._UpdatePage.ApplyUpdateUpdateChannelSubtitlePlaceholder;
                public string ApplyUpdateUpdateStatusTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateUpdateStatusTitle;
                public string ApplyUpdateMiscIdle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateMiscIdle;
                public string ApplyUpdateMiscNone { get; set; } = LangFallback?._UpdatePage.ApplyUpdateMiscNone;
                public string ApplyUpdateVersionSeparator { get; set; } = LangFallback?._UpdatePage.ApplyUpdateVersionSeparator;

                public string ApplyUpdateErrCollapseRunTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrCollapseRunTitle;
                public string ApplyUpdateErrCollapseRunSubtitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrCollapseRunSubtitle;
                public string ApplyUpdateErrCollapseRunTitleWarnBox { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrCollapseRunTitleWarnBox;
                public string ApplyUpdateErrCollapseRunSubtitleWarnBox { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrCollapseRunSubtitleWarnBox;
                public string ApplyUpdateErrVelopackStateBrokenTitleWarnBox { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrVelopackStateBrokenTitleWarnBox;
                public string ApplyUpdateErrVelopackStateBrokenSubtitleWarnBox { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrVelopackStateBrokenSubtitleWarnBox;
                public string ApplyUpdateErrReleaseFileNotFoundTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrReleaseFileNotFoundTitle;
                public string ApplyUpdateErrReleaseFileNotFoundSubtitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateErrReleaseFileNotFoundSubtitle;

                public string ApplyUpdateDownloadSizePlaceholder { get; set; } = LangFallback?._UpdatePage.ApplyUpdateMiscIdle;
                public string ApplyUpdateDownloadSpeed { get; set; } = LangFallback?._UpdatePage.ApplyUpdateDownloadSpeed;
                public string ApplyUpdateDownloadSpeedPlaceholder { get; set; } = LangFallback?._UpdatePage.ApplyUpdateDownloadSpeedPlaceholder;
                public string ApplyUpdateDownloadTimeEst { get; set; } = LangFallback?._UpdatePage.ApplyUpdateDownloadTimeEst;
                public string ApplyUpdateDownloadTimeEstPlaceholder { get; set; } = LangFallback?._UpdatePage.ApplyUpdateDownloadTimeEstPlaceholder;

                public string ApplyUpdateTaskLegacyVerFoundTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyVerFoundTitle;
                public string ApplyUpdateTaskLegacyVerFoundSubtitle1 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyVerFoundSubtitle1;
                public string ApplyUpdateTaskLegacyVerFoundSubtitle2 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyVerFoundSubtitle2;
                public string ApplyUpdateTaskLegacyVerFoundSubtitle3 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyVerFoundSubtitle3;
                public string ApplyUpdateTaskLegacyVerFoundSubtitle4 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyVerFoundSubtitle4;
                public string ApplyUpdateTaskLegacyVerFoundSubtitle5 { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyVerFoundSubtitle5;
                public string ApplyUpdateTaskLegacyCleanupCount { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyCleanupCount;
                public string ApplyUpdateTaskLegacyDeleting { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLegacyDeleting;

                public string ApplyUpdateTaskDownloadingPkgTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskDownloadingPkgTitle;
                public string ApplyUpdateTaskExtractingPkgTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskExtractingPkgTitle;
                public string ApplyUpdateTaskRemoveOldPkgTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskRemoveOldPkgTitle;
                public string ApplyUpdateTaskMovingExtractFileTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskMovingExtractFileTitle;

                public string ApplyUpdateTaskLauncherUpdatedTitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLauncherUpdatedTitle;
                public string ApplyUpdateTaskLauncherUpdatedSubtitle { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskLauncherUpdatedSubtitle;

                public string ApplyUpdateTaskError { get; set; } = LangFallback?._UpdatePage.ApplyUpdateTaskError;
            }
        }
        #endregion
    }
}
