﻿namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region SettingsPage
        public sealed partial class LocalizationParams
        {
            public LangSettingsPage _SettingsPage { get; set; } = LangFallback?._SettingsPage;
            public sealed class LangSettingsPage
            {
                public string PageTitle                             { get; set; } = LangFallback?._SettingsPage.PageTitle;
                public string Debug                                 { get; set; } = LangFallback?._SettingsPage.Debug;
                public string Debug_Console                         { get; set; } = LangFallback?._SettingsPage.Debug_Console;
                public string Debug_IncludeGameLogs                 { get; set; } = LangFallback?._SettingsPage.Debug_IncludeGameLogs;
                public string Debug_MultipleInstance                { get; set; } = LangFallback?._SettingsPage.Debug_MultipleInstance;
                public string ChangeRegionWarning_Toggle            { get; set; } = LangFallback?._SettingsPage.ChangeRegionWarning_Toggle;
                public string ChangeRegionWarning_Warning           { get; set; } = LangFallback?._SettingsPage.ChangeRegionWarning_Warning;
                public string Language                              { get; set; } = LangFallback?._SettingsPage.Language;
                public string LanguageEntry                         { get; set; } = LangFallback?._SettingsPage.LanguageEntry;
                public string AppThemes                             { get; set; } = LangFallback?._SettingsPage.AppThemes;
                public string AppThemes_Default                     { get; set; } = LangFallback?._SettingsPage.AppThemes_Default;
                public string AppThemes_Light                       { get; set; } = LangFallback?._SettingsPage.AppThemes_Light;
                public string AppThemes_Dark                        { get; set; } = LangFallback?._SettingsPage.AppThemes_Dark;
                public string AppCDNRepository                      { get; set; } = LangFallback?._SettingsPage.AppCDNRepository;
                public string AppThemes_ApplyNeedRestart            { get; set; } = LangFallback?._SettingsPage.AppThemes_ApplyNeedRestart;
                public string AppWindowSize                         { get; set; } = LangFallback?._SettingsPage.AppWindowSize;
                public string AppWindowSize_Normal                  { get; set; } = LangFallback?._SettingsPage.AppWindowSize_Normal;
                public string AppWindowSize_Small                   { get; set; } = LangFallback?._SettingsPage.AppWindowSize_Small;
                public string AppBG                                 { get; set; } = LangFallback?._SettingsPage.AppBG;
                public string AppBG_Checkbox                        { get; set; } = LangFallback?._SettingsPage.AppBG_Checkbox;
                public string AppBG_Note                            { get; set; } = LangFallback?._SettingsPage.AppBG_Note;
                public string AppLang_ApplyNeedRestart              { get; set; } = LangFallback?._SettingsPage.AppLang_ApplyNeedRestart;
                public string AppThreads                            { get; set; } = LangFallback?._SettingsPage.AppThreads;
                public string AppThreads_Download                   { get; set; } = LangFallback?._SettingsPage.AppThreads_Download;
                public string AppThreads_Extract                    { get; set; } = LangFallback?._SettingsPage.AppThreads_Extract;
                public string AppThreads_Help1                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Help1;
                public string AppThreads_Help2                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Help2;
                public string AppThreads_Help3                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Help3;
                public string AppThreads_Help4                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Help4;
                public string AppThreads_Help5                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Help5;
                public string AppThreads_Help6                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Help6;
                public string DiscordRPC                            { get; set; } = LangFallback?._SettingsPage.DiscordRPC;
                public string DiscordRPC_Toggle                     { get; set; } = LangFallback?._SettingsPage.DiscordRPC_Toggle;
                public string DiscordRPC_GameStatusToggle           { get; set; } = LangFallback?._SettingsPage.DiscordRPC_GameStatusToggle;
                public string DiscordRPC_IdleStatusToggle           { get; set; } = LangFallback?._SettingsPage.DiscordRPC_IdleStatusToggle;
                public string Update                                { get; set; } = LangFallback?._SettingsPage.Update;
                public string Update_CurVer                         { get; set; } = LangFallback?._SettingsPage.Update_CurVer;
                public string Update_CheckBtn                       { get; set; } = LangFallback?._SettingsPage.Update_CheckBtn;
                public string Update_ForceBtn                       { get; set; } = LangFallback?._SettingsPage.Update_ForceBtn;
                public string Update_NewVer1                        { get; set; } = LangFallback?._SettingsPage.Update_NewVer1;
                public string Update_NewVer2                        { get; set; } = LangFallback?._SettingsPage.Update_NewVer2;
                public string Update_LatestVer                      { get; set; } = LangFallback?._SettingsPage.Update_LatestVer;
                public string AppFiles                              { get; set; } = LangFallback?._SettingsPage.AppFiles;
                public string AppFiles_OpenDataFolderBtn            { get; set; } = LangFallback?._SettingsPage.AppFiles_OpenDataFolderBtn;
                public string AppFiles_RelocateDataFolderBtn        { get; set; } = LangFallback?._SettingsPage.AppFiles_RelocateDataFolderBtn;
                public string AppFiles_ClearLogBtn                  { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearLogBtn;
                public string AppFiles_ClearImgCachesBtn            { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearImgCachesBtn;
                public string AppFiles_ClearMetadataBtn             { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearMetadataBtn;
                public string AppFiles_ClearMetadataDialog          { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearMetadataDialog;
                public string AppFiles_ClearMetadataDialogHelp      { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearMetadataDialogHelp;
                public string ReportIssueBtn                        { get; set; } = LangFallback?._SettingsPage.ReportIssueBtn;
                public string HelpLocalizeBtn                       { get; set; } = LangFallback?._SettingsPage.HelpLocalizeBtn;
                public string ContributePRBtn                       { get; set; } = LangFallback?._SettingsPage.ContributePRBtn;
                public string ContributorListBtn                    { get; set; } = LangFallback?._SettingsPage.ContributorListBtn;
                public string About                                 { get; set; } = LangFallback?._SettingsPage.About;
                public string About_Copyright1                      { get; set; } = LangFallback?._SettingsPage.About_Copyright1;
                public string About_Copyright2                      { get; set; } = LangFallback?._SettingsPage.About_Copyright2;
                public string About_Copyright3                      { get; set; } = LangFallback?._SettingsPage.About_Copyright3;
                public string About_Copyright4                      { get; set; } = LangFallback?._SettingsPage.About_Copyright4;
                public string Disclaimer                            { get; set; } = LangFallback?._SettingsPage.Disclaimer;
                public string Disclaimer1                           { get; set; } = LangFallback?._SettingsPage.Disclaimer1;
                public string Disclaimer2                           { get; set; } = LangFallback?._SettingsPage.Disclaimer2;
                public string Disclaimer3                           { get; set; } = LangFallback?._SettingsPage.Disclaimer3;
                public string DiscordBtn1                           { get; set; } = LangFallback?._SettingsPage.DiscordBtn1;
                public string DiscordBtn2                           { get; set; } = LangFallback?._SettingsPage.DiscordBtn2;
                public string DiscordBtn3                           { get; set; } = LangFallback?._SettingsPage.DiscordBtn3;
                public string AppChangeReleaseChannel               { get; set; } = LangFallback?._SettingsPage.AppChangeReleaseChannel;
                public string EnableAcrylicEffect                   { get; set; } = LangFallback?._SettingsPage.EnableAcrylicEffect;
                public string EnableDownloadChunksMerging           { get; set; } = LangFallback?._SettingsPage.EnableDownloadChunksMerging;
                public string LowerCollapsePrioOnGameLaunch         { get; set; } = LangFallback?._SettingsPage.LowerCollapsePrioOnGameLaunch;
                public string LowerCollapsePrioOnGameLaunch_Tooltip {get;  set;}  = LangFallback?._SettingsPage.LowerCollapsePrioOnGameLaunch_Tooltip;
                public string UseExternalBrowser                    { get; set; } = LangFallback?._SettingsPage.UseExternalBrowser;
				public string KbShortcuts_Title                     { get; set; } = LangFallback?._SettingsPage.KbShortcuts_Title;
                public string KbShortcuts_ShowBtn                   { get; set; } = LangFallback?._SettingsPage.KbShortcuts_ShowBtn;
                public string KbShortcuts_ResetBtn                  { get; set; } = LangFallback?._SettingsPage.KbShortcuts_ResetBtn;
                public string AppBehavior_Title                     { get; set; } = LangFallback?._SettingsPage.AppBehavior_Title;
                public string AppBehavior_PostGameLaunch            { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch;
                public string AppBehavior_PostGameLaunch_Minimize   { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch_Minimize;
                public string AppBehavior_PostGameLaunch_ToTray     { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch_ToTray;
                public string AppBehavior_PostGameLaunch_Nothing    { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch_Nothing;
                public string AppBehavior_MinimizeToTray            { get; set; } = LangFallback?._SettingsPage.AppBehavior_MinimizeToTray;
                public string AppBehavior_LaunchOnStartup           { get; set; } = LangFallback?._SettingsPage.AppBehavior_LaunchOnStartup;
                public string AppBehavior_StartupToTray             { get; set; } = LangFallback?._SettingsPage.AppBehavior_StartupToTray;
                public string Waifu2X_Toggle                        { get; set; } = LangFallback?._SettingsPage.Waifu2X_Toggle;
                public string Waifu2X_Help                          { get; set; } = LangFallback?._SettingsPage.Waifu2X_Help;
                public string Waifu2X_Warning_CpuMode               { get; set; } = LangFallback?._SettingsPage.Waifu2X_Warning_CpuMode;
                public string Waifu2X_Error_Loader                  { get; set; } = LangFallback?._SettingsPage.Waifu2X_Error_Loader;
                public string Waifu2X_Error_Output                  { get; set; } = LangFallback?._SettingsPage.Waifu2X_Error_Output;
            }
        }
        #endregion
    }
}
