using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region SettingsPage
        public sealed partial class LocalizationParams
        {
            public LangSettingsPage _SettingsPage { get; set; } = LangFallback?._SettingsPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangSettingsPage
            {
                public string PageTitle                                   { get; set; } = LangFallback?._SettingsPage.PageTitle;
                public string Debug                                       { get; set; } = LangFallback?._SettingsPage.Debug;
                public string Debug_Console                               { get; set; } = LangFallback?._SettingsPage.Debug_Console;
                public string Debug_IncludeGameLogs                       { get; set; } = LangFallback?._SettingsPage.Debug_IncludeGameLogs;
                public string Debug_SendRemoteCrashData                   { get; set; } = LangFallback?._SettingsPage.Debug_SendRemoteCrashData;
                public string Debug_SendRemoteCrashData_EnvVarDisablement { get; set; } = LangFallback?._SettingsPage.Debug_SendRemoteCrashData_EnvVarDisablement;
                public string Debug_MultipleInstance                      { get; set; } = LangFallback?._SettingsPage.Debug_MultipleInstance;
                public string Debug_CustomDialogBtn                            { get; set; } = LangFallback?._SettingsPage.Debug_CustomDialogBtn;
                
                public string ChangeRegionWarning_Toggle                { get; set; } = LangFallback?._SettingsPage.ChangeRegionWarning_Toggle;
                public string ChangeRegionInstant_Toggle                { get; set; } = LangFallback?._SettingsPage.ChangeRegionInstant_Toggle;
                public string ChangeRegionWarning_Warning               { get; set; } = LangFallback?._SettingsPage.ChangeRegionWarning_Warning;
                public string Language                                  { get; set; } = LangFallback?._SettingsPage.Language;
                public string LanguageEntry                             { get; set; } = LangFallback?._SettingsPage.LanguageEntry;
                public string AppThemes                                 { get; set; } = LangFallback?._SettingsPage.AppThemes;
                public string AppThemes_Default                         { get; set; } = LangFallback?._SettingsPage.AppThemes_Default;
                public string AppThemes_Light                           { get; set; } = LangFallback?._SettingsPage.AppThemes_Light;
                public string AppThemes_Dark                            { get; set; } = LangFallback?._SettingsPage.AppThemes_Dark;
                public string AppCDNRepository                          { get; set; } = LangFallback?._SettingsPage.AppCDNRepository;
                public string AppThemes_ApplyNeedRestart                { get; set; } = LangFallback?._SettingsPage.AppThemes_ApplyNeedRestart;
                public string IntroSequenceToggle                       { get; set; } = LangFallback?._SettingsPage.IntroSequenceToggle;
                public string AppWindowSize                             { get; set; } = LangFallback?._SettingsPage.AppWindowSize;
                public string AppWindowSize_Normal                      { get; set; } = LangFallback?._SettingsPage.AppWindowSize_Normal;
                public string AppWindowSize_Small                       { get; set; } = LangFallback?._SettingsPage.AppWindowSize_Small;
                public string AppBG                                     { get; set; } = LangFallback?._SettingsPage.AppBG;
                public string AppBG_Checkbox                            { get; set; } = LangFallback?._SettingsPage.AppBG_Checkbox;
                public string AppBG_Note                                { get; set; } = LangFallback?._SettingsPage.AppBG_Note;
                public string AppBG_Note_Regional                       { get; set; } = LangFallback?._SettingsPage.AppBG_Note_Regional;
                public string AppLang_ApplyNeedRestart                  { get; set; } = LangFallback?._SettingsPage.AppLang_ApplyNeedRestart;
                public string AppThreads                                { get; set; } = LangFallback?._SettingsPage.AppThreads;
                public string AppThreads_Download                       { get; set; } = LangFallback?._SettingsPage.AppThreads_Download;
                public string AppThreads_Extract                        { get; set; } = LangFallback?._SettingsPage.AppThreads_Extract;
                public string AppThreads_Help1                          { get; set; } = LangFallback?._SettingsPage.AppThreads_Help1;
                public string AppThreads_Help2                          { get; set; } = LangFallback?._SettingsPage.AppThreads_Help2;
                public string AppThreads_Help3                          { get; set; } = LangFallback?._SettingsPage.AppThreads_Help3;
                public string AppThreads_Help4                          { get; set; } = LangFallback?._SettingsPage.AppThreads_Help4;
                public string AppThreads_Help5                          { get; set; } = LangFallback?._SettingsPage.AppThreads_Help5;
                public string AppThreads_Help6                          { get; set; } = LangFallback?._SettingsPage.AppThreads_Help6;
                public string AppThreads_Attention                      { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention;
                public string AppThreads_Attention1                     { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention1;
                public string AppThreads_Attention2                     { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention2;
                public string AppThreads_Attention3                     { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention3;
                public string AppThreads_Attention4                     { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention4;
                public string AppThreads_Attention5                     { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention5;
                public string AppThreads_Attention6                     { get; set; } = LangFallback?._SettingsPage.AppThreads_Attention6;
                public string AppThreads_AttentionTop1                  { get; set; } = LangFallback?._SettingsPage.AppThreads_AttentionTop1;
                public string AppThreads_AttentionTop2                  { get; set; } = LangFallback?._SettingsPage.AppThreads_AttentionTop2;
                public string DiscordRPC                                { get; set; } = LangFallback?._SettingsPage.DiscordRPC;
                public string DiscordRPC_Toggle                         { get; set; } = LangFallback?._SettingsPage.DiscordRPC_Toggle;
                public string DiscordRPC_GameStatusToggle               { get; set; } = LangFallback?._SettingsPage.DiscordRPC_GameStatusToggle;
                public string DiscordRPC_IdleStatusToggle               { get; set; } = LangFallback?._SettingsPage.DiscordRPC_IdleStatusToggle;
                public string ImageBackground                           { get; set; } = LangFallback?._SettingsPage.ImageBackground;
                public string VideoBackground                           { get; set; } = LangFallback?._SettingsPage.VideoBackground;
                public string VideoBackground_IsEnableAudio             { get; set; } = LangFallback?._SettingsPage.VideoBackground_IsEnableAudio;
                public string VideoBackground_IsEnableAcrylicBackground { get; set; } = LangFallback?._SettingsPage.VideoBackground_IsEnableAcrylicBackground;
                public string VideoBackground_AudioVolume               { get; set; } = LangFallback?._SettingsPage.VideoBackground_AudioVolume;
                public string Update                                    { get; set; } = LangFallback?._SettingsPage.Update;
                public string Update_CurVer                             { get; set; } = LangFallback?._SettingsPage.Update_CurVer;
                public string Update_CheckBtn                           { get; set; } = LangFallback?._SettingsPage.Update_CheckBtn;
                public string Update_ForceBtn                           { get; set; } = LangFallback?._SettingsPage.Update_ForceBtn;
                public string Update_NewVer1                            { get; set; } = LangFallback?._SettingsPage.Update_NewVer1;
                public string Update_NewVer2                            { get; set; } = LangFallback?._SettingsPage.Update_NewVer2;
                public string Update_LatestVer                          { get; set; } = LangFallback?._SettingsPage.Update_LatestVer;
                public string Update_SeeChangelog                       { get; set; } = LangFallback?._SettingsPage.Update_SeeChangelog;
                public string Update_ChangelogTitle                     { get; set; } = LangFallback?._SettingsPage.Update_ChangelogTitle;
                public string AppFiles                                  { get; set; } = LangFallback?._SettingsPage.AppFiles;
                public string AppFiles_OpenDataFolderBtn                { get; set; } = LangFallback?._SettingsPage.AppFiles_OpenDataFolderBtn;
                public string AppFiles_RelocateDataFolderBtn            { get; set; } = LangFallback?._SettingsPage.AppFiles_RelocateDataFolderBtn;
                public string AppFiles_ClearLogBtn                      { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearLogBtn;
                public string AppFiles_ClearImgCachesBtn                { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearImgCachesBtn;
                public string AppFiles_ClearMetadataBtn                 { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearMetadataBtn;
                public string AppFiles_ClearMetadataDialog              { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearMetadataDialog;
                public string AppFiles_ClearMetadataDialogHelp          { get; set; } = LangFallback?._SettingsPage.AppFiles_ClearMetadataDialogHelp;
                public string ReportIssueBtn                            { get; set; } = LangFallback?._SettingsPage.ReportIssueBtn;
                public string HelpLocalizeBtn                           { get; set; } = LangFallback?._SettingsPage.HelpLocalizeBtn;
                public string ContributePRBtn                           { get; set; } = LangFallback?._SettingsPage.ContributePRBtn;
                public string ContributorListBtn                        { get; set; } = LangFallback?._SettingsPage.ContributorListBtn;
                public string ShareYourFeedbackBtn                      { get; set; } = LangFallback?._SettingsPage.ShareYourFeedbackBtn;
                public string About                                     { get; set; } = LangFallback?._SettingsPage.About;
                public string About_Copyright1                          { get; set; } = LangFallback?._SettingsPage.About_Copyright1;
                public string About_Copyright2                          { get; set; } = LangFallback?._SettingsPage.About_Copyright2;
                public string About_Copyright3                          { get; set; } = LangFallback?._SettingsPage.About_Copyright3;
                public string About_Copyright4                          { get; set; } = LangFallback?._SettingsPage.About_Copyright4;
                public string LicenseType                               { get; set; } = LangFallback?._SettingsPage.LicenseType;
                public string Disclaimer                                { get; set; } = LangFallback?._SettingsPage.Disclaimer;
                public string Disclaimer1                               { get; set; } = LangFallback?._SettingsPage.Disclaimer1;
                public string Disclaimer2                               { get; set; } = LangFallback?._SettingsPage.Disclaimer2;
                public string Disclaimer3                               { get; set; } = LangFallback?._SettingsPage.Disclaimer3;
                public string DiscordBtn1                               { get; set; } = LangFallback?._SettingsPage.DiscordBtn1;
                public string DiscordBtn2                               { get; set; } = LangFallback?._SettingsPage.DiscordBtn2;
                public string DiscordBtn3                               { get; set; } = LangFallback?._SettingsPage.DiscordBtn3;
                public string WebsiteBtn                                { get; set; } = LangFallback?._SettingsPage.WebsiteBtn;
                public string AppChangeReleaseChannel                   { get; set; } = LangFallback?._SettingsPage.AppChangeReleaseChannel;
                public string EnableAcrylicEffect                       { get; set; } = LangFallback?._SettingsPage.EnableAcrylicEffect;
                public string EnableDownloadChunksMerging               { get; set; } = LangFallback?._SettingsPage.EnableDownloadChunksMerging;
                public string Enforce7ZipExtract                        { get; set; } = LangFallback?._SettingsPage.Enforce7ZipExtract;
                public string LowerCollapsePrioOnGameLaunch             { get; set; } = LangFallback?._SettingsPage.LowerCollapsePrioOnGameLaunch;
                public string LowerCollapsePrioOnGameLaunch_Tooltip     { get; set; }  = LangFallback?._SettingsPage.LowerCollapsePrioOnGameLaunch_Tooltip;
                public string UseExternalBrowser                        { get; set; } = LangFallback?._SettingsPage.UseExternalBrowser;
                public string KbShortcuts_Title                         { get; set; } = LangFallback?._SettingsPage.KbShortcuts_Title;
                public string KbShortcuts_ShowBtn                       { get; set; } = LangFallback?._SettingsPage.KbShortcuts_ShowBtn;
                public string KbShortcuts_ResetBtn                      { get; set; } = LangFallback?._SettingsPage.KbShortcuts_ResetBtn;
                public string AppBehavior_Title                         { get; set; } = LangFallback?._SettingsPage.AppBehavior_Title;
                public string AppBehavior_PostGameLaunch                { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch;
                public string AppBehavior_PostGameLaunch_Minimize       { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch_Minimize;
                public string AppBehavior_PostGameLaunch_ToTray         { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch_ToTray;
                public string AppBehavior_PostGameLaunch_Nothing        { get; set; } = LangFallback?._SettingsPage.AppBehavior_PostGameLaunch_Nothing;
                public string AppBehavior_MinimizeToTray                { get; set; } = LangFallback?._SettingsPage.AppBehavior_MinimizeToTray;
                public string AppBehavior_LaunchOnStartup               { get; set; } = LangFallback?._SettingsPage.AppBehavior_LaunchOnStartup;
                public string AppBehavior_StartupToTray                 { get; set; } = LangFallback?._SettingsPage.AppBehavior_StartupToTray;
                public string Waifu2X_Toggle                            { get; set; } = LangFallback?._SettingsPage.Waifu2X_Toggle;
                public string Waifu2X_Help                              { get; set; } = LangFallback?._SettingsPage.Waifu2X_Help;
                public string Waifu2X_Help2                             { get; set; } = LangFallback?._SettingsPage.Waifu2X_Help2;
                public string Waifu2X_Warning_CpuMode                   { get; set; } = LangFallback?._SettingsPage.Waifu2X_Warning_CpuMode;
                public string Waifu2X_Warning_D3DMappingLayers          { get; set; } = LangFallback?._SettingsPage.Waifu2X_Warning_D3DMappingLayers;
                public string Waifu2X_Error_Loader                      { get; set; } = LangFallback?._SettingsPage.Waifu2X_Error_Loader;
                public string Waifu2X_Error_Output                      { get; set; } = LangFallback?._SettingsPage.Waifu2X_Error_Output;
                public string Waifu2X_Initializing                      { get; set; } = LangFallback?._SettingsPage.Waifu2X_Initializing;
                public string SophonSettingsTitle                       { get; set; } = LangFallback?._SettingsPage.SophonSettingsTitle;
                public string SophonHelp_Title                          { get; set; } = LangFallback?._SettingsPage.SophonHelp_Title;
                public string SophonHelp_1                              { get; set; } = LangFallback?._SettingsPage.SophonHelp_1;
                public string SophonHelp_2                              { get; set; } = LangFallback?._SettingsPage.SophonHelp_2;
                public string SophonHelp_IndicatorTitle                 { get; set; } = LangFallback?._SettingsPage.SophonHelp_IndicatorTitle;
                public string SophonHelp_Indicator1                     { get; set; } = LangFallback?._SettingsPage.SophonHelp_Indicator1;
                public string SophonHelp_Indicator2                     { get; set; } = LangFallback?._SettingsPage.SophonHelp_Indicator2;
                public string SophonHelp_Indicator3                     { get; set; } = LangFallback?._SettingsPage.SophonHelp_Indicator3;
                public string SophonHelp_Indicator4                     { get; set; } = LangFallback?._SettingsPage.SophonHelp_Indicator4;
                public string SophonHelp_Thread                         { get; set; } = LangFallback?._SettingsPage.SophonHelp_Thread;
                public string SophonHttpNumberBox                       { get; set; } = LangFallback?._SettingsPage.SophonHttpNumberBox;
                public string SophonHelp_Http                           { get; set; } = LangFallback?._SettingsPage.SophonHelp_Http;
                public string SophonToggle                              { get; set; } = LangFallback?._SettingsPage.SophonToggle;
                public string SophonPredownPerfMode_Toggle              { get; set; } = LangFallback?._SettingsPage.SophonPredownPerfMode_Toggle;
                public string SophonPredownPerfMode_Tooltip             { get; set; } = LangFallback?._SettingsPage.SophonPredownPerfMode_Tooltip;

                public string NetworkSettings_Title                                 { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Title;
                public string NetworkSettings_Proxy_Title                           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_Title;
                public string NetworkSettings_Proxy_Hostname                        { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_Hostname;
                public string NetworkSettings_Proxy_HostnameHelp1                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_HostnameHelp1;
                public string NetworkSettings_Proxy_HostnameHelp2                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_HostnameHelp2;
                public string NetworkSettings_Proxy_HostnameHelp3                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_HostnameHelp3;
                public string NetworkSettings_Proxy_HostnameHelp4                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_HostnameHelp4;
                public string NetworkSettings_ProxyWarn_UrlInvalid                  { get; set; } = LangFallback?._SettingsPage.NetworkSettings_ProxyWarn_UrlInvalid;
                public string NetworkSettings_ProxyWarn_NotSupported                { get; set; } = LangFallback?._SettingsPage.NetworkSettings_ProxyWarn_NotSupported;
                public string NetworkSettings_Proxy_Username                        { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_Username;
                public string NetworkSettings_Proxy_UsernamePlaceholder             { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_UsernamePlaceholder;
                public string NetworkSettings_Proxy_UsernameHelp1                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_UsernameHelp1;
                public string NetworkSettings_Proxy_UsernameHelp2                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_UsernameHelp2;
                public string NetworkSettings_Proxy_UsernameHelp3                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_UsernameHelp3;
                public string NetworkSettings_Proxy_Password                        { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_Password;
                public string NetworkSettings_Proxy_PasswordPlaceholder             { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_PasswordPlaceholder;
                public string NetworkSettings_Proxy_PasswordHelp1                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_PasswordHelp1;
                public string NetworkSettings_Proxy_PasswordHelp2                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_PasswordHelp2;
                public string NetworkSettings_Proxy_PasswordHelp3                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Proxy_PasswordHelp3;
                public string NetworkSettings_Http_Title                            { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Http_Title;
                public string NetworkSettings_Http_Redirect                         { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Http_Redirect;
                public string NetworkSettings_Http_SimulateCookies                  { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Http_SimulateCookies;
                public string NetworkSettings_Http_UntrustedHttps                   { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Http_UntrustedHttps;
                public string NetworkSettings_Http_Timeout                          { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Http_Timeout;
                public string NetworkSettings_ProxyTest_Button                      { get; set; } = LangFallback?._SettingsPage.NetworkSettings_ProxyTest_Button;
                public string NetworkSettings_ProxyTest_ButtonChecking              { get; set; } = LangFallback?._SettingsPage.NetworkSettings_ProxyTest_ButtonChecking;
                public string NetworkSettings_ProxyTest_ButtonSuccess               { get; set; } = LangFallback?._SettingsPage.NetworkSettings_ProxyTest_ButtonSuccess;
                public string NetworkSettings_ProxyTest_ButtonFailed                { get; set; } = LangFallback?._SettingsPage.NetworkSettings_ProxyTest_ButtonFailed;
                public string NetworkSettings_Dns_Title                             { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_Title;
                public string NetworkSettings_Dns_ConnectionType                    { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ConnectionType;
                public string NetworkSettings_Dns_ConnectionType_SelectionUdp       { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ConnectionType_SelectionUdp;
                public string NetworkSettings_Dns_ConnectionType_SelectionDoH       { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ConnectionType_SelectionDoH;
                public string NetworkSettings_Dns_ConnectionType_SelectionDoT       { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ConnectionType_SelectionDoT;
                public string NetworkSettings_Dns_ConnectionType_Tooltip1           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ConnectionType_Tooltip1;
                public string NetworkSettings_Dns_ProviderSelection                 { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ProviderSelection;
                public string NetworkSettings_Dns_ProviderSelection_SelectionCustom { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ProviderSelection_SelectionCustom;
                public string NetworkSettings_Dns_ProviderSelection_Tooltip1        { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ProviderSelection_Tooltip1;
                public string NetworkSettings_Dns_CustomProvider                    { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider;
                public string NetworkSettings_Dns_CustomProvider_Tooltip1           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider_Tooltip1;
                public string NetworkSettings_Dns_CustomProvider_Tooltip2           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider_Tooltip2;
                public string NetworkSettings_Dns_CustomProvider_Tooltip3           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider_Tooltip3;
                public string NetworkSettings_Dns_CustomProvider_Tooltip4           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider_Tooltip4;
                public string NetworkSettings_Dns_CustomProvider_Tooltip5           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider_Tooltip5;
                public string NetworkSettings_Dns_CustomProvider_Tooltip6           { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_CustomProvider_Tooltip6;
                public string NetworkSettings_Dns_ChangesWarning                    { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ChangesWarning;
                public string NetworkSettings_Dns_ValidateAndSaveSettingsButton     { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ValidateAndSaveSettingsButton;
                public string NetworkSettings_Dns_ApplyingSettingsButton            { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_ApplyingSettingsButton;
                public string NetworkSettings_Dns_SettingsSavedButton               { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_SettingsSavedButton;
                public string NetworkSettings_Dns_SettingsFailedButton              { get; set; } = LangFallback?._SettingsPage.NetworkSettings_Dns_SettingsFailedButton;

                public string FileDownloadSettings_Title                { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_Title;
                public string FileDownloadSettings_SpeedLimit_Title     { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimit_Title;
                public string FileDownloadSettings_SpeedLimit_NumBox    { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimit_NumBox;
                public string FileDownloadSettings_SpeedLimitHelp1      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimitHelp1;
                public string FileDownloadSettings_SpeedLimitHelp2      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimitHelp2;
                public string FileDownloadSettings_SpeedLimitHelp3      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimitHelp3;
                public string FileDownloadSettings_SpeedLimitHelp4      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimitHelp4;
                public string FileDownloadSettings_SpeedLimitHelp5      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_SpeedLimitHelp5;

                public string FileDownloadSettings_NewPreallocChunk_Title       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunk_Title;
                public string FileDownloadSettings_NewPreallocChunk_Subtitle    { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunk_Subtitle;
                public string FileDownloadSettings_NewPreallocChunk_NumBox      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunk_NumBox;
                public string FileDownloadSettings_NewPreallocChunkHelp1        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp1;
                public string FileDownloadSettings_NewPreallocChunkHelp2        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp2;
                public string FileDownloadSettings_NewPreallocChunkHelp3        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp3;
                public string FileDownloadSettings_NewPreallocChunkHelp4        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp4;
                public string FileDownloadSettings_NewPreallocChunkHelp5        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp5;
                public string FileDownloadSettings_NewPreallocChunkHelp6        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp6;
                public string FileDownloadSettings_NewPreallocChunkHelp7        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp7;
                public string FileDownloadSettings_NewPreallocChunkHelp8        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp8;
                public string FileDownloadSettings_NewPreallocChunkHelp9        { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp9;
                public string FileDownloadSettings_NewPreallocChunkHelp10       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_NewPreallocChunkHelp10;

                public string FileDownloadSettings_BurstDownload_Title      { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownload_Title;
                public string FileDownloadSettings_BurstDownload_Subtitle   { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownload_Subtitle;
                public string FileDownloadSettings_BurstDownloadHelp1       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp1;
                public string FileDownloadSettings_BurstDownloadHelp2       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp2;
                public string FileDownloadSettings_BurstDownloadHelp3       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp3;
                public string FileDownloadSettings_BurstDownloadHelp4       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp4;
                public string FileDownloadSettings_BurstDownloadHelp5       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp5;
                public string FileDownloadSettings_BurstDownloadHelp6       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp6;
                public string FileDownloadSettings_BurstDownloadHelp7       { get; set; } = LangFallback?._SettingsPage.FileDownloadSettings_BurstDownloadHelp7;

                public string Database_Title                            { get; set; } = LangFallback?._SettingsPage.Database_Title;
                public string Database_ConnectionOk                     { get; set; } = LangFallback?._SettingsPage.Database_ConnectionOk;
                public string Database_ConnectFail                      { get; set; } = LangFallback?._SettingsPage.Database_ConnectFail;
                public string Database_Toggle                           { get; set; } = LangFallback?._SettingsPage.Database_Toggle;
                public string Database_Url                              { get; set; } = LangFallback?._SettingsPage.Database_Url;
                public string Database_Url_Example                      { get; set; } = LangFallback?._SettingsPage.Database_Url_Example;
                public string Database_Token                            { get; set; } = LangFallback?._SettingsPage.Database_Token;
                public string Database_UserId                           { get; set; } = LangFallback?._SettingsPage.Database_UserId;
                public string Database_GenerateGuid                     { get; set; } = LangFallback?._SettingsPage.Database_GenerateGuid;
                public string Database_Validate                         { get; set; } = LangFallback?._SettingsPage.Database_Validate;
                public string Database_Error_EmptyUri                   { get; set; } = LangFallback?._SettingsPage.Database_Error_EmptyUri;
                public string Database_Error_EmptyToken                 { get; set; } = LangFallback?._SettingsPage.Database_Error_EmptyToken;
                public string Database_Error_InvalidGuid                { get; set; } = LangFallback?._SettingsPage.Database_Error_InvalidGuid;
                public string Database_Warning_PropertyChanged          { get; set; } = LangFallback?._SettingsPage.Database_Warning_PropertyChanged;
                public string Database_ValidationChecking               { get; set; } = LangFallback?._SettingsPage.Database_ValidationChecking;
                public string Database_Placeholder_DbUserIdTextBox      { get; set; } = LangFallback?._SettingsPage.Database_Placeholder_DbUserIdTextBox;
                public string Database_Placeholder_DbTokenPasswordBox   { get; set; } = LangFallback?._SettingsPage.Database_Placeholder_DbTokenPasswordBox;

                public string SearchPlaceholder { get; set; } = LangFallback?._SettingsPage.SearchPlaceholder;
                
                public string Plugin_LoadedInfoTitle { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoTitle;
                public string Plugin_OpenManagerBtn { get; set; } = LangFallback?._SettingsPage.Plugin_OpenManagerBtn;
                public string Plugin_AuthorBy { get; set; } = LangFallback?._SettingsPage.Plugin_AuthorBy;
                public string Plugin_LoadedInfoDesc { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoDesc;
                public string Plugin_LoadedInfoPluginVer { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoPluginVer;
                public string Plugin_LoadedInfoInterfaceVer { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoInterfaceVer;
                public string Plugin_LoadedInfoCreationDate { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoCreationDate;
                public string Plugin_LoadedInfoMainLibLocation { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoMainLibLocation;
                public string Plugin_LoadedInfoLoadedPresets { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoLoadedPresets;
                public string Plugin_LoadedInfoClipboardCopied { get; set; } = LangFallback?._SettingsPage.Plugin_LoadedInfoClipboardCopied;
                public string Plugin_PluginInfoNameUnknown { get; set; } = LangFallback?._SettingsPage.Plugin_PluginInfoNameUnknown;
                public string Plugin_PluginInfoDescUnknown { get; set; } = LangFallback?._SettingsPage.Plugin_PluginInfoDescUnknown;
                public string Plugin_PluginInfoAuthorUnknown { get; set; } = LangFallback?._SettingsPage.Plugin_PluginInfoAuthorUnknown;
            }
        }
        #endregion
    }
}
