namespace Hi3Helper
{ 
    public sealed partial class Locale
    {
        #region GenshinGameSettingsPage
        public sealed partial class LocalizationParams
        {
            public LangGenshinGameSettingsPage _GenshinGameSettingsPage { get; set; } = LangFallback?._GenshinGameSettingsPage;
            public sealed class LangGenshinGameSettingsPage
            {
                public string PageTitle { get; set; } = LangFallback?._GenshinGameSettingsPage.PageTitle;
                #region Overlay
                public string OverlayNotInstalledTitle { get; set; } = LangFallback?._GenshinGameSettingsPage.OverlayNotInstalledTitle;
                public string OverlayNotInstalledSubtitle { get; set; } = LangFallback?._GenshinGameSettingsPage.OverlayNotInstalledSubtitle;
                public string OverlayGameRunningTitle { get; set; } = LangFallback?._GenshinGameSettingsPage.OverlayGameRunningTitle;
                public string OverlayGameRunningSubtitle { get; set; } = LangFallback?._GenshinGameSettingsPage.OverlayGameRunningSubtitle;
                public string OverlayFirstTimeTitle { get; set; } = LangFallback?._GenshinGameSettingsPage.OverlayFirstTimeTitle;
                public string OverlayFirstTimeSubtitle { get; set; } = LangFallback?._GenshinGameSettingsPage.OverlayFirstTimeSubtitle;
                #endregion

                #region CustomArgs
                public string CustomArgs_Title { get; set; } = LangFallback?._GenshinGameSettingsPage.CustomArgs_Title;
                public string CustomArgs_Subtitle { get; set; } = LangFallback?._GenshinGameSettingsPage.CustomArgs_Subtitle;
                public string CustomArgs_Footer1 { get; set; } = LangFallback?._GenshinGameSettingsPage.CustomArgs_Footer1;
                public string CustomArgs_Footer2 { get; set; } = LangFallback?._GenshinGameSettingsPage.CustomArgs_Footer2;
                public string CustomArgs_Footer3 { get; set; } = LangFallback?._GenshinGameSettingsPage.CustomArgs_Footer3;
                #endregion

                #region Misc
                public string ApplyBtn { get; set; } = LangFallback?._GenshinGameSettingsPage.ApplyBtn;
                public string SettingsApplied { get; set; } = LangFallback?._GenshinGameSettingsPage.SettingsApplied;
                #endregion

                #region Audio
                public string Audio_Title { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_Title;

                public string Audio_Master { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_Master;
                public string Audio_BGM { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_BGM;
                public string Audio_SFX { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_SFX;
                public string Audio_VO { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_VO;

                public string Audio_Output_Surround { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_Output_Surround;
                public string Audio_DynamicRange { get; set; } = LangFallback?._GenshinGameSettingsPage.Audio_DynamicRange;
                #endregion

                #region Graphics
                public string Graphics_Title { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_Title;

                public string Graphics_ResolutionPanel { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ResolutionPanel;
                public string Graphics_SpecPanel { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_SpecPanel;
                public string Graphics_Fullscreen { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_Fullscreen;
                public string Graphics_ExclusiveFullscreen { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ExclusiveFullscreen;
                public string Graphics_ExclusiveFullscreen_Help { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ExclusiveFullscreen_Help;
                public string Graphics_ResSelectPlaceholder { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ResSelectPlaceholder;
                public string Graphics_ResCustom { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ResCustom;
                public string Graphics_ResCustomW { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ResCustomW;
                public string Graphics_ResCustomH { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ResCustomH;

                public string Graphics_Gamma { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_Gamma;
                public string Graphics_FPS { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_FPS;
                public string Graphics_RenderScale { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_RenderScale;
                public string Graphics_ShadowQuality { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ShadowQuality;
                public string Graphics_VisualFX { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_VisualFX;
                public string Graphics_SFXQuality { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_SFXQuality;
                public string Graphics_EnvDetailQuality { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_EnvDetailQuality;
                public string Graphics_VSync { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_VSync;
                public string Graphics_AAMode { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_AAMode;
                public string Graphics_VolFogs { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_VolFogs;
                public string Graphics_VolFogs_ToolTip { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_VolFogs_ToolTip;
                public string Graphics_ReflectionQuality { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_ReflectionQuality;
                public string Graphics_MotionBlur { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_MotionBlur;
                public string Graphics_BloomQuality { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_BloomQuality;
                public string Graphics_CrowdDensity { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_CrowdDensity;
                public string Graphics_SubsurfaceScattering { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_SubsurfaceScattering;
                public string Graphics_TeammateFX { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_TeammateFX;
                public string Graphics_AnisotropicFiltering { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_AnisotropicFiltering;
                public string Graphics_TeamPageBackground { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_TeamPageBackground;
                public string Graphics_GlobalIllumination { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_GlobalIllumination;
                public string Graphics_GlobalIllumination_Help1 { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_GlobalIllumination_Help1;
                public string Graphics_GlobalIllumination_Help2 { get; set; } = LangFallback?._GenshinGameSettingsPage.Graphics_GlobalIllumination_Help2;
                #endregion

                #region Specs
                public string SpecEnabled { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecEnabled;
                public string SpecDisabled { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecDisabled;
                public string SpecVeryLow { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecVeryLow;
                public string SpecLow { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecLow;
                public string SpecMedium { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecMedium;
                public string SpecHigh { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecHigh;
                public string SpecVeryHigh { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecVeryHigh;
                public string SpecExtreme { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecExtreme;
                public string SpecPartiallyOff { get; set; } = LangFallback?._GenshinGameSettingsPage.SpecPartiallyOff;
                #endregion

                #region Language
                public string Language { get; set; } = LangFallback?._GenshinGameSettingsPage.Language;
                public string Language_Help1 { get; set; } = LangFallback?._GenshinGameSettingsPage.Language_Help1;
                public string Language_Help2 { get; set; } = LangFallback?._GenshinGameSettingsPage.Language_Help2;
                public string LanguageAudio { get; set; } = LangFallback?._GenshinGameSettingsPage.LanguageAudio;
                public string LanguageText { get; set; } = LangFallback?._GenshinGameSettingsPage.LanguageText;
                public string VO_en { get; set; } = LangFallback?._GenshinGameSettingsPage.VO_en;
                public string VO_cn { get; set; } = LangFallback?._GenshinGameSettingsPage.VO_cn;
                public string VO_jp { get; set; } = LangFallback?._GenshinGameSettingsPage.VO_jp;
                public string VO_kr { get; set; } = LangFallback?._GenshinGameSettingsPage.VO_kr;
                #endregion
            }
        }
        #endregion
    }
}