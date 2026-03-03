using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region HomePage
        public sealed partial class LocalizationParams
        {
            public LangHomePage _HomePage { get; set; } = LangFallback?._HomePage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangHomePage
            {
                public string PageTitle { get; set; } = LangFallback?._HomePage.PageTitle;
                public string PreloadTitle { get; set; } = LangFallback?._HomePage.PreloadTitle;
                public string PreloadNotifTitle { get; set; } = LangFallback?._HomePage.PreloadNotifTitle;
                public string PreloadNotifDeltaDetectTitle { get; set; } = LangFallback?._HomePage.PreloadNotifDeltaDetectTitle;
                public string PreloadNotifSubtitle { get; set; } = LangFallback?._HomePage.PreloadNotifSubtitle;
                public string PreloadNotifDeltaDetectSubtitle { get; set; } = LangFallback?._HomePage.PreloadNotifDeltaDetectSubtitle;
                public string PreloadNotifCompleteTitle { get; set; } = LangFallback?._HomePage.PreloadNotifCompleteTitle;
                public string PreloadNotifCompleteSubtitle { get; set; } = LangFallback?._HomePage.PreloadNotifCompleteSubtitle;
                public string PreloadNotifIntegrityCheckBtn { get; set; } = LangFallback?._HomePage.PreloadNotifIntegrityCheckBtn;
                public string StartBtn { get; set; } = LangFallback?._HomePage.StartBtn;
                public string StartBtnRunning { get; set; } = LangFallback?._HomePage.StartBtnRunning;
                public string VerifyingPkgTitle { get; set; } = LangFallback?._HomePage.VerifyingPkgTitle;
                public string VerifyingPkgSubtitle { get; set; } = LangFallback?._HomePage.VerifyingPkgSubtitle;
                public string PreloadDownloadNotifbarTitle { get; set; } = LangFallback?._HomePage.PreloadDownloadNotifbarTitle;
                public string PreloadDownloadNotifbarVerifyTitle { get; set; } = LangFallback?._HomePage.PreloadDownloadNotifbarVerifyTitle;
                public string PreloadDownloadNotifbarSubtitle { get; set; } = LangFallback?._HomePage.PreloadDownloadNotifbarSubtitle;
                public string UpdatingVoicePack { get; set; } = LangFallback?._HomePage.UpdatingVoicePack;
                public string InstallBtn { get; set; } = LangFallback?._HomePage.InstallBtn;
                public string UpdateBtn { get; set; } = LangFallback?._HomePage.UpdateBtn;
                public string PauseDownloadBtn { get; set; } = LangFallback?._HomePage.PauseDownloadBtn;
                public string ResumeDownloadBtn { get; set; } = LangFallback?._HomePage.ResumeDownloadBtn;
                public string PauseCancelDownloadBtn { get; set; } = LangFallback?._HomePage.PauseCancelDownloadBtn;
                public string PauseCancelBtn { get; set; } = LangFallback?._HomePage.PauseCancelBtn;
                public string DownloadBtn { get; set; } = LangFallback?._HomePage.DownloadBtn;
                public string StartGameTooltip { get; set; } = LangFallback?._HomePage.StartGameTooltip;
                public string InstallGameTooltip { get; set; } = LangFallback?._HomePage.InstallGameTooltip;
                public string UpdateGameTooltip { get; set; } = LangFallback?._HomePage.UpdateGameTooltip;
                public string InstallSdkTooltip { get; set; } = LangFallback?._HomePage.InstallSdkTooltip;
                public string UpdateSdkTooltip { get; set; } = LangFallback?._HomePage.UpdateSdkTooltip;
                public string InstallPluginTooltip { get; set; } = LangFallback?._HomePage.InstallPluginTooltip;
                public string UpdatePluginTooltip { get; set; } = LangFallback?._HomePage.UpdatePluginTooltip;
                public string GameStatusPlaceholderComingSoonBtn { get; set; } = LangFallback?._HomePage.GameStatusPlaceholderComingSoonBtn;
                public string GameStatusPlaceholderPreRegisterBtn { get; set; } = LangFallback?._HomePage.GameStatusPlaceholderPreRegisterBtn;
                public string GameSettingsBtn { get; set; } = LangFallback?._HomePage.GameSettingsBtn;
                public string GameSettings_Panel1 { get; set; } = LangFallback?._HomePage.GameSettings_Panel1;
                public string GameSettings_Panel1OpenGameFolder { get; set; } = LangFallback?._HomePage.GameSettings_Panel1OpenGameFolder;
                public string GameSettings_Panel1OpenCacheFolder { get; set; } = LangFallback?._HomePage.GameSettings_Panel1OpenCacheFolder;
                public string GameSettings_Panel1OpenScreenshotFolder { get; set; } = LangFallback?._HomePage.GameSettings_Panel1OpenScreenshotFolder;
                public string GameSettings_Panel2 { get; set; } = LangFallback?._HomePage.GameSettings_Panel2;
                public string GameSettings_Panel2RepairGame { get; set; } = LangFallback?._HomePage.GameSettings_Panel2RepairGame;
                public string GameSettings_Panel2UninstallGame { get; set; } = LangFallback?._HomePage.GameSettings_Panel2UninstallGame;
                public string GameSettings_Panel2ConvertVersion { get; set; } = LangFallback?._HomePage.GameSettings_Panel2ConvertVersion;
                public string GameSettings_Panel2MoveGameLocationGame { get; set; } = LangFallback?._HomePage.GameSettings_Panel2MoveGameLocationGame;
                public string GameSettings_Panel2MoveGameLocationGame_SamePath { get; set; } = LangFallback?._HomePage.GameSettings_Panel2MoveGameLocationGame_SamePath;
                public string GameSettings_Panel2ChangeGameLocation { get; set; } = LangFallback?._HomePage.GameSettings_Panel2ChangeGameLocation;
                public string GameSettings_Panel2StopGame { get; set; } = LangFallback?._HomePage.GameSettings_Panel2StopGame;
                public string GameSettings_Panel3RegionalSettings { get; set; } = LangFallback?._HomePage.GameSettings_Panel3RegionalSettings;
                public string GameSettings_Panel3 { get; set; } = LangFallback?._HomePage.GameSettings_Panel3;
                public string GameSettings_Panel3RegionRpc { get; set; } = LangFallback?._HomePage.GameSettings_Panel3RegionRpc;
                public string GameSettings_Panel3CustomBGRegion { get; set; } = LangFallback?._HomePage.GameSettings_Panel3CustomBGRegion;
                public string GameSettings_Panel3CustomBGRegionSectionTitle { get; set; } = LangFallback?._HomePage.GameSettings_Panel3CustomBGRegionSectionTitle;
                public string GameSettings_Panel4 { get; set; } = LangFallback?._HomePage.GameSettings_Panel4;
                public string GameSettings_Panel4ShowEventsPanel { get; set; } = LangFallback?._HomePage.GameSettings_Panel4ShowEventsPanel;
                public string GameSettings_Panel4ScaleUpEventsPanel { get; set; } = LangFallback?._HomePage.GameSettings_Panel4ScaleUpEventsPanel;
                public string GameSettings_Panel4ShowSocialMediaPanel { get; set; } = LangFallback?._HomePage.GameSettings_Panel4ShowSocialMediaPanel;
                public string GameSettings_Panel4ShowPlaytimeButton { get; set; } = LangFallback?._HomePage.GameSettings_Panel4ShowPlaytimeButton;
                public string GameSettings_Panel4SyncPlaytimeDatabase { get; set; } = LangFallback?._HomePage.GameSettings_Panel4SyncPlaytimeDatabase;
                public string GameSettings_Panel4CreateShortcutBtn { get; set; } = LangFallback?._HomePage.GameSettings_Panel4CreateShortcutBtn;
                public string GameSettings_Panel4AddToSteamBtn { get; set; } = LangFallback?._HomePage.GameSettings_Panel4AddToSteamBtn;
                public string CreateShortcut_FolderPicker { get; set; } = LangFallback?._HomePage.CreateShortcut_FolderPicker;
                public string GamePlaytime_Panel1 { get; set; } = LangFallback?._HomePage.GamePlaytime_Panel1;
                public string GamePlaytime_Idle_Panel1Hours { get; set; } = LangFallback?._HomePage.GamePlaytime_Idle_Panel1Hours;
                public string GamePlaytime_Idle_Panel1Minutes { get; set; } = LangFallback?._HomePage.GamePlaytime_Idle_Panel1Minutes;
                public string GamePlaytime_Idle_ResetBtn { get; set; } = LangFallback?._HomePage.GamePlaytime_Idle_ResetBtn;
                public string GamePlaytime_Idle_ChangeBtn { get; set; } = LangFallback?._HomePage.GamePlaytime_Idle_ChangeBtn;
                public string GamePlaytime_Idle_SyncDb { get; set; } = LangFallback?._HomePage.GamePlaytime_Idle_SyncDb;
                public string GamePlaytime_Idle_SyncDbSyncing { get; set; } = LangFallback?._HomePage.GamePlaytime_Idle_SyncDbSyncing;
                public string GamePlaytime_Running_Info1 { get; set; } = LangFallback?._HomePage.GamePlaytime_Running_Info1;
                public string GamePlaytime_Running_Info2 { get; set; } = LangFallback?._HomePage.GamePlaytime_Running_Info2;
                public string GamePlaytime_Display { get; set; } = LangFallback?._HomePage.GamePlaytime_Display;
                public string GamePlaytime_DateDisplay { get; set; } = LangFallback?._HomePage.GamePlaytime_DateDisplay;
                public string GamePlaytime_Stats_Title { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_Title;
                public string GamePlaytime_Stats_NeverPlayed { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_NeverPlayed;
                public string GamePlaytime_Stats_LastSession { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_LastSession;
                public string GamePlaytime_Stats_LastSession_StartTime { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_LastSession_StartTime;
                public string GamePlaytime_Stats_LastSession_Duration { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_LastSession_Duration;
                public string GamePlaytime_Stats_Daily { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_Daily;
                public string GamePlaytime_Stats_Weekly { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_Weekly;
                public string GamePlaytime_Stats_Monthly { get; set; } = LangFallback?._HomePage.GamePlaytime_Stats_Monthly;
                public string PostPanel_Events { get; set; } = LangFallback?._HomePage.PostPanel_Events;
                public string PostPanel_Notices { get; set; } = LangFallback?._HomePage.PostPanel_Notices;
                public string PostPanel_Info { get; set; } = LangFallback?._HomePage.PostPanel_Info;
                public string PostPanel_NoNews { get; set; } = LangFallback?._HomePage.PostPanel_NoNews;
                public string CommunityToolsBtn { get; set; } = LangFallback?._HomePage.CommunityToolsBtn;
                public string CommunityToolsBtn_OfficialText { get; set; } = LangFallback?._HomePage.CommunityToolsBtn_OfficialText;
                public string CommunityToolsBtn_CommunityText { get; set; } = LangFallback?._HomePage.CommunityToolsBtn_CommunityText;
                public string CommunityToolsBtn_OpenExecutableAppDialogTitle { get; set; } = LangFallback?._HomePage.CommunityToolsBtn_OpenExecutableAppDialogTitle;
                public string Exception_DownloadTimeout1 { get; set; } = LangFallback?._HomePage.Exception_DownloadTimeout1;
                public string Exception_DownloadTimeout2 { get; set; } = LangFallback?._HomePage.Exception_DownloadTimeout2;
                public string Exception_DownloadTimeout3 { get; set; } = LangFallback?._HomePage.Exception_DownloadTimeout3;
                public string GameStateInvalid_Title { get; set; } = LangFallback?._HomePage.GameStateInvalid_Title;
                public string GameStateInvalid_Subtitle1 { get; set; } = LangFallback?._HomePage.GameStateInvalid_Subtitle1;
                public string GameStateInvalid_Subtitle2 { get; set; } = LangFallback?._HomePage.GameStateInvalid_Subtitle2;
                public string GameStateInvalid_Subtitle3 { get; set; } = LangFallback?._HomePage.GameStateInvalid_Subtitle3;
                public string GameStateInvalid_Subtitle4 { get; set; } = LangFallback?._HomePage.GameStateInvalid_Subtitle4;
                public string GameStateInvalid_Subtitle5 { get; set; } = LangFallback?._HomePage.GameStateInvalid_Subtitle5;
                public string GameStateInvalidFixed_Title { get; set; } = LangFallback?._HomePage.GameStateInvalidFixed_Title;
                public string GameStateInvalidFixed_Subtitle1 { get; set; } = LangFallback?._HomePage.GameStateInvalidFixed_Subtitle1;
                public string GameStateInvalidFixed_Subtitle2 { get; set; } = LangFallback?._HomePage.GameStateInvalidFixed_Subtitle2;
                public string GameStateInvalidFixed_Subtitle3 { get; set; } = LangFallback?._HomePage.GameStateInvalidFixed_Subtitle3;
                public string GameStateInvalidFixed_Subtitle4 { get; set; } = LangFallback?._HomePage.GameStateInvalidFixed_Subtitle4;
                public string InstallFolderRootTitle { get; set; } = LangFallback?._HomePage.InstallFolderRootTitle;
                public string InstallFolderRootSubtitle { get; set; } = LangFallback?._HomePage.InstallFolderRootSubtitle;

                public string BgContextMenu_SaveCurrentBgText { get; set; } = LangFallback?._HomePage.BgContextMenu_SaveCurrentBgText;
                public string BgContextMenu_SaveCurrentBgTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_SaveCurrentBgTooltip;
                public string BgContextMenu_SaveAllBgText { get; set; } = LangFallback?._HomePage.BgContextMenu_SaveAllBgText;
                public string BgContextMenu_SaveAllBgTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_SaveAllBgTooltip;
                public string BgContextMenu_CopyFrameToClipboardParentText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardParentText;
                public string BgContextMenu_CopyFrameToClipboardOverlayText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardOverlayText;
                public string BgContextMenu_CopyFrameToClipboardOverlayTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardOverlayTooltip;
                public string BgContextMenu_CopyFrameToClipboardBackgroundText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardBackgroundText;
                public string BgContextMenu_CopyFrameToClipboardBackgroundTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardBackgroundTooltip;
                public string BgContextMenu_CopyFrameToClipboardMergedText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardMergedText;
                public string BgContextMenu_CopyFrameToClipboardMergedTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyFrameToClipboardMergedTooltip;
                public string BgContextMenu_CopyUrlToClipboardParentText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyUrlToClipboardParentText;
                public string BgContextMenu_CopyUrlToClipboardOverlayText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyUrlToClipboardOverlayText;
                public string BgContextMenu_CopyUrlToClipboardOverlayTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyUrlToClipboardOverlayTooltip;
                public string BgContextMenu_CopyUrlToClipboardBackgroundText { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyUrlToClipboardBackgroundText;
                public string BgContextMenu_CopyUrlToClipboardBackgroundTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_CopyUrlToClipboardBackgroundTooltip;
                public string BgContextMenu_EnableAudioText { get; set; } = LangFallback?._HomePage.BgContextMenu_EnableAudioText;
                public string BgContextMenu_EnableAudioTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_EnableAudioTooltip;
                public string BgContextMenu_KeepPlayVideoOnUnfocusText { get; set; } = LangFallback?._HomePage.BgContextMenu_KeepPlayVideoOnUnfocusText;
                public string BgContextMenu_KeepPlayVideoOnUnfocusTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_KeepPlayVideoOnUnfocusTooltip;
                public string BgContextMenu_EnableParallaxText { get; set; } = LangFallback?._HomePage.BgContextMenu_EnableParallaxText;
                public string BgContextMenu_EnableParallaxTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_EnableParallaxTooltip;
                public string BgContextMenu_ParallaxPixelShiftParentText { get; set; } = LangFallback?._HomePage.BgContextMenu_ParallaxPixelShiftParentText;
                public string BgContextMenu_ParallaxPixelShiftCustomText { get; set; } = LangFallback?._HomePage.BgContextMenu_ParallaxPixelShiftCustomText;
                public string BgContextMenu_ParallaxPixelShiftCustomTooltip { get; set; } = LangFallback?._HomePage.BgContextMenu_ParallaxPixelShiftCustomTooltip;
                public string BgContextMenu_FolderSelectSaveCurrentBg { get; set; } = LangFallback?._HomePage.BgContextMenu_FolderSelectSaveCurrentBg;
                public string BgContextMenu_FolderSelectSaveAllBg { get; set; } = LangFallback?._HomePage.BgContextMenu_FolderSelectSaveAllBg;
            }
        }
        #endregion
    }
}
