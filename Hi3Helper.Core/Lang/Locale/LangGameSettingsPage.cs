using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region GameSettingsPage
        public sealed partial class LocalizationParams
        {
            public LangGameSettingsPage _GameSettingsPage { get; set; } = LangFallback?._GameSettingsPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangGameSettingsPage
            {
                public string PageTitle                       { get; set; } = LangFallback?._GameSettingsPage.PageTitle;
                public string Graphics_Title                  { get; set; } = LangFallback?._GameSettingsPage.Graphics_Title;
                public string Graphics_ResolutionPanel        { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResolutionPanel;
                public string Graphics_Fullscreen             { get; set; } = LangFallback?._GameSettingsPage.Graphics_Fullscreen;
                public string Graphics_Borderless             { get; set; } = LangFallback?._GameSettingsPage.Graphics_Borderless;
                public string Graphics_ExclusiveFullscreen    { get; set; } = LangFallback?._GameSettingsPage.Graphics_ExclusiveFullscreen;
                public string Graphics_ResizableWindow        { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResizableWindow;
                public string Graphics_ResizableWindowTooltip { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResizableWindowTooltip;
                public string Graphics_ResSelectPlaceholder   { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResSelectPlaceholder;
                public string Graphics_ResCustom              { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResCustom;
                public string Graphics_ResCustomW             { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResCustomW;
                public string Graphics_ResCustomH             { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResCustomH;
                public string Graphics_ResPrefixFullscreen    { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResPrefixFullscreen;
                public string Graphics_ResPrefixWindowed      { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResPrefixWindowed;
                public string Graphics_VSync                  { get; set; } = LangFallback?._GameSettingsPage.Graphics_VSync;
                public string Graphics_FPS                    { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPS;
                public string Graphics_FPSPanel               { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSPanel;
                public string Graphics_FPSInCombat            { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSInCombat;
                public string Graphics_FPSInMenu              { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSInMenu;
                public string Graphics_FPSUnlimited           { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSUnlimited;
                public string Graphics_APIPanel               { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIPanel;
                public string Graphics_APIHelp1               { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIHelp1;
                public string Graphics_APIHelp2               { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIHelp2;
                public string Graphics_APIHelp3               { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIHelp3;
                public string Graphics_SpecPanel              { get; set; } = LangFallback?._GameSettingsPage.Graphics_SpecPanel;
                public string Graphics_Preset                 { get; set; } = LangFallback?._GameSettingsPage.Graphics_Preset;
                public string Graphics_Render                 { get; set; } = LangFallback?._GameSettingsPage.Graphics_Render;
                public string Graphics_Shadow                 { get; set; } = LangFallback?._GameSettingsPage.Graphics_Shadow;
                public string Graphics_Reflection             { get; set; } = LangFallback?._GameSettingsPage.Graphics_Reflection;
                public string Graphics_FX                     { get; set; } = LangFallback?._GameSettingsPage.Graphics_FX;
                public string Graphics_FXPost                 { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXPost;
                public string Graphics_FXPhysics              { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXPhysics;
                public string Graphics_FXHDR                  { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXHDR;
                public string Graphics_FXHQ                   { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXHQ;
                public string Graphics_FXAA                   { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXAA;
                public string Graphics_FXDistort              { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXDistort;
                public string Graphics_APHO2Panel             { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2Panel;
                public string Graphics_APHO2GI                { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2GI;
                public string Graphics_APHO2VL                { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2VL;
                public string Graphics_APHO2AO                { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2AO;
                public string Graphics_APHO2LOD               { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2LOD;
                public string Graphics_ParticleQuality        { get; set; } = LangFallback?._GameSettingsPage.Graphics_ParticleQuality;
                public string Graphics_LightingQuality        { get; set; } = LangFallback?._GameSettingsPage.Graphics_LightingQuality;
                public string Graphics_PostFXQuality          { get; set; } = LangFallback?._GameSettingsPage.Graphics_PostFXQuality;
                public string Graphics_AAMode                 { get; set; } = LangFallback?._GameSettingsPage.Graphics_AAMode;
                public string Graphics_CharacterQuality       { get; set; } = LangFallback?._GameSettingsPage.Graphics_CharacterQuality;
                public string Graphics_WeatherQuality         { get; set; } = LangFallback?._GameSettingsPage.Graphics_WeatherQuality;
                public string Graphics_Legacy_Title           { get; set; } = LangFallback?._GameSettingsPage.Graphics_Legacy_Title;
                public string Graphics_Legacy_Subtitle        { get; set; } = LangFallback?._GameSettingsPage.Graphics_Legacy_Subtitle;
                public string SpecDisabled                    { get; set; } = LangFallback?._GameSettingsPage.SpecDisabled;
                public string SpecCustom                      { get; set; } = LangFallback?._GameSettingsPage.SpecCustom;
                public string SpecLow                         { get; set; } = LangFallback?._GameSettingsPage.SpecLow;
                public string SpecMedium                      { get; set; } = LangFallback?._GameSettingsPage.SpecMedium;
                public string SpecHigh                        { get; set; } = LangFallback?._GameSettingsPage.SpecHigh;
                public string SpecVeryHigh                    { get; set; } = LangFallback?._GameSettingsPage.SpecVeryHigh;
                public string SpecMaximum                     { get; set; } = LangFallback?._GameSettingsPage.SpecMaximum;
                public string SpecUltra                       { get; set; } = LangFallback?._GameSettingsPage.SpecUltra;
                public string Audio_Title                     { get; set; } = LangFallback?._GameSettingsPage.Audio_Title;
                public string Audio_Mute                      { get; set; } = LangFallback?._GameSettingsPage.Audio_Mute;
                public string Audio_Master                    { get; set; } = LangFallback?._GameSettingsPage.Audio_Master;
                public string Audio_BGM                       { get; set; } = LangFallback?._GameSettingsPage.Audio_BGM;
                public string Audio_SFX                       { get; set; } = LangFallback?._GameSettingsPage.Audio_SFX;
                public string Audio_VOLang                    { get; set; } = LangFallback?._GameSettingsPage.Audio_VOLang;
                public string Audio_VOLang1                   { get; set; } = LangFallback?._GameSettingsPage.Audio_VOLang1;
                public string Audio_VOLang2                   { get; set; } = LangFallback?._GameSettingsPage.Audio_VOLang2;
                public string Audio_VODefault                 { get; set; } = LangFallback?._GameSettingsPage.Audio_VODefault;
                public string Audio_VO                        { get; set; } = LangFallback?._GameSettingsPage.Audio_VO;
                public string Audio_Elf                       { get; set; } = LangFallback?._GameSettingsPage.Audio_Elf;
                public string Audio_Cutscenes                 { get; set; } = LangFallback?._GameSettingsPage.Audio_Cutscenes;
                public string ApplyBtn                        { get; set; } = LangFallback?._GameSettingsPage.ApplyBtn;
                public string SettingsApplied                 { get; set; } = LangFallback?._GameSettingsPage.SettingsApplied;
                public string SettingsRegExported             { get; set; } = LangFallback?._GameSettingsPage.SettingsRegExported;
                public string SettingsRegExportTitle          { get; set; } = LangFallback?._GameSettingsPage.SettingsRegExportTitle;
                public string SettingsRegImported             { get; set; } = LangFallback?._GameSettingsPage.SettingsRegImported;
                public string SettingsRegImportTitle          { get; set; } = LangFallback?._GameSettingsPage.SettingsRegImportTitle;
                public string SettingsRegErr1                 { get; set; } = LangFallback?._GameSettingsPage.SettingsRegErr1;
                public string SettingsRegErr2                 { get; set; } = LangFallback?._GameSettingsPage.SettingsRegErr2;
                public string RegImportExport                 { get; set; } = LangFallback?._GameSettingsPage.RegImportExport;
                public string RegExportTitle                  { get; set; } = LangFallback?._GameSettingsPage.RegExportTitle;
                public string RegExportTooltip                { get; set; } = LangFallback?._GameSettingsPage.RegExportTooltip;
                public string RegImportTitle                  { get; set; } = LangFallback?._GameSettingsPage.RegImportTitle;
                public string RegImportTooltip                { get; set; } = LangFallback?._GameSettingsPage.RegImportTooltip;
                public string OverlayNotInstalledTitle        { get; set; } = LangFallback?._GameSettingsPage.OverlayNotInstalledTitle;
                public string OverlayNotInstalledSubtitle     { get; set; } = LangFallback?._GameSettingsPage.OverlayNotInstalledSubtitle;
                public string OverlayGameRunningTitle         { get; set; } = LangFallback?._GameSettingsPage.OverlayGameRunningTitle;
                public string OverlayGameRunningSubtitle      { get; set; } = LangFallback?._GameSettingsPage.OverlayGameRunningSubtitle;
                public string OverlayFirstTimeTitle           { get; set; } = LangFallback?._GameSettingsPage.OverlayFirstTimeTitle;
                public string OverlayFirstTimeSubtitle        { get; set; } = LangFallback?._GameSettingsPage.OverlayFirstTimeSubtitle;
                public string CustomArgs_Title                { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Title;
                public string CustomArgs_Subtitle             { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Subtitle;
                public string CustomArgs_Footer1              { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Footer1;
                public string CustomArgs_Footer2              { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Footer2;
                public string CustomArgs_Footer3              { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Footer3;
                public string GameBoost                       { get; set; } = LangFallback?._GameSettingsPage.GameBoost;
                public string MobileLayout                    { get; set; } = LangFallback?._GameSettingsPage.MobileLayout;
                public string Advanced_Title                  { get; set; } = LangFallback?._GameSettingsPage.Advanced_Title;
                public string Advanced_Subtitle1              { get; set; } = LangFallback?._GameSettingsPage.Advanced_Subtitle1;
                public string Advanced_Subtitle2              { get; set; } = LangFallback?._GameSettingsPage.Advanced_Subtitle2;
                public string Advanced_Subtitle3              { get; set; } = LangFallback?._GameSettingsPage.Advanced_Subtitle3;
                public string Advanced_GLC_WarningAdmin       { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_WarningAdmin;
                public string Advanced_GLC_PreLaunch_Title    { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_PreLaunch_Title;
                public string Advanced_GLC_PreLaunch_Subtitle { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_PreLaunch_Subtitle;
                public string Advanced_GLC_PreLaunch_Exit     { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_PreLaunch_Exit;
                public string Advanced_GLC_PreLaunch_Delay    { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_PreLaunch_Delay;
                public string Advanced_GLC_PostExit_Title     { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_PostExit_Title;
                public string Advanced_GLC_PostExit_Subtitle  { get; set; } = LangFallback?._GameSettingsPage.Advanced_GLC_PostExit_Subtitle;
            }
        }
        #endregion
    }
}
