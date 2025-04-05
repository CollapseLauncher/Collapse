using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region StarRailGameSettingsPage
        public sealed partial class LocalizationParams
        {
            public LangStarRailGameSettingsPage _StarRailGameSettingsPage { get; set; } = LangFallback?._StarRailGameSettingsPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangStarRailGameSettingsPage
            {
                public string PageTitle                     { get; set; } = LangFallback?._StarRailGameSettingsPage.PageTitle;
                public string Graphics_Title                { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_Title;
                public string Graphics_ResolutionPanel      { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResolutionPanel;
                public string Graphics_Fullscreen           { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_Fullscreen;
                public string Graphics_ExclusiveFullscreen  { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ExclusiveFullscreen;
                public string Graphics_ResSelectPlaceholder { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResSelectPlaceholder;
                public string Graphics_ResCustom            { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResCustom;
                public string Graphics_ResCustomTooltip     { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResCustomTooltip;
                public string Graphics_ResCustomW           { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResCustomW;
                public string Graphics_ResCustomH           { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResCustomH;
                public string OverlayNotInstalledTitle      { get; set; } = LangFallback?._StarRailGameSettingsPage.OverlayNotInstalledTitle;
                public string OverlayNotInstalledSubtitle   { get; set; } = LangFallback?._StarRailGameSettingsPage.OverlayNotInstalledSubtitle;
                public string OverlayGameRunningTitle       { get; set; } = LangFallback?._StarRailGameSettingsPage.OverlayGameRunningTitle;
                public string OverlayGameRunningSubtitle    { get; set; } = LangFallback?._StarRailGameSettingsPage.OverlayGameRunningSubtitle;
                public string OverlayFirstTimeTitle         { get; set; } = LangFallback?._StarRailGameSettingsPage.OverlayFirstTimeTitle;
                public string OverlayFirstTimeSubtitle      { get; set; } = LangFallback?._StarRailGameSettingsPage.OverlayFirstTimeSubtitle;
                public string CustomArgs_Title              { get; set; } = LangFallback?._StarRailGameSettingsPage.CustomArgs_Title;
                public string CustomArgs_Subtitle           { get; set; } = LangFallback?._StarRailGameSettingsPage.CustomArgs_Subtitle;
                public string CustomArgs_Footer1            { get; set; } = LangFallback?._StarRailGameSettingsPage.CustomArgs_Footer1;
                public string CustomArgs_Footer2            { get; set; } = LangFallback?._StarRailGameSettingsPage.CustomArgs_Footer2;
                public string CustomArgs_Footer3            { get; set; } = LangFallback?._StarRailGameSettingsPage.CustomArgs_Footer3;
                public string ApplyBtn                      { get; set; } = LangFallback?._StarRailGameSettingsPage.ApplyBtn;
                public string SettingsApplied               { get; set; } = LangFallback?._StarRailGameSettingsPage.SettingsApplied;
                public string Audio_Title                   { get; set; } = LangFallback?._StarRailGameSettingsPage.Audio_Title;
                public string Audio_Master                  { get; set; } = LangFallback?._StarRailGameSettingsPage.Audio_Master;
                public string Audio_BGM                     { get; set; } = LangFallback?._StarRailGameSettingsPage.Audio_BGM;
                public string Audio_SFX                     { get; set; } = LangFallback?._StarRailGameSettingsPage.Audio_SFX;
                public string Audio_VO                      { get; set; } = LangFallback?._StarRailGameSettingsPage.Audio_VO;
                public string Audio_Mute                    { get; set; } = LangFallback?._StarRailGameSettingsPage.Audio_Mute;
                public string Language                      { get; set; } = LangFallback?._StarRailGameSettingsPage.Language;
                public string Language_Help1                { get; set; } = LangFallback?._StarRailGameSettingsPage.Language_Help1;
                public string Language_Help2                { get; set; } = LangFallback?._StarRailGameSettingsPage.Language_Help2;
                public string LanguageAudio                 { get; set; } = LangFallback?._StarRailGameSettingsPage.LanguageAudio;
                public string LanguageText                  { get; set; } = LangFallback?._StarRailGameSettingsPage.LanguageText;
                public string VO_en                         { get; set; } = LangFallback?._StarRailGameSettingsPage.VO_en;
                public string VO_cn                         { get; set; } = LangFallback?._StarRailGameSettingsPage.VO_cn;
                public string VO_jp                         { get; set; } = LangFallback?._StarRailGameSettingsPage.VO_jp;
                public string VO_kr                         { get; set; } = LangFallback?._StarRailGameSettingsPage.VO_kr;
                public string Graphics_FPS                  { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_FPS;
                public string Graphics_FPS_Help             { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_FPS_Help;
                public string Graphics_FPS_Help2            { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_FPS_Help2;
                public string Graphics_VSync                { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_VSync;
                public string Graphics_VSync_Help           { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_VSync_Help;
                public string Graphics_RenderScale          { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_RenderScale;
                public string Graphics_ResolutionQuality    { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ResolutionQuality;
                public string Graphics_ShadowQuality        { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ShadowQuality;
                public string Graphics_LightQuality         { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_LightQuality;
                public string Graphics_CharacterQuality     { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_CharacterQuality;
                public string Graphics_EnvDetailQuality     { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_EnvDetailQuality;
                public string Graphics_ReflectionQuality    { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_ReflectionQuality;
                public string Graphics_BloomQuality         { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_BloomQuality;
                public string Graphics_SFXQuality           { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_SFXQuality;
                public string Graphics_AAMode               { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_AAMode;
                public string Graphics_DlssQuality          { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_DlssQuality;
                public string Graphics_SpecPanel            { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_SpecPanel;
                public string Graphics_SelfShadow           { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_SelfShadow;
                public string Graphics_HalfResTransparent   { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_HalfResTransparent;
                public string SpecEnabled                   { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecEnabled;
                public string SpecDisabled                  { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecDisabled;
                public string SpecVeryLow                   { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecVeryLow;
                public string SpecLow                       { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecLow;
                public string SpecMedium                    { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecMedium;
                public string SpecHigh                      { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecHigh;
                public string SpecVeryHigh                  { get; set; } = LangFallback?._StarRailGameSettingsPage.SpecVeryHigh;
                public string Graphics_DLSS_UHP             { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_DLSS_UHP;
                public string Graphics_DLSS_Perf            { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_DLSS_Perf;
                public string Graphics_DLSS_Balanced        { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_DLSS_Balanced;
                public string Graphics_DLSS_Quality         { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_DLSS_Quality;
                public string Graphics_DLSS_DLAA            { get; set; } = LangFallback?._StarRailGameSettingsPage.Graphics_DLSS_DLAA;
            }
        }
        #endregion
    }
}
