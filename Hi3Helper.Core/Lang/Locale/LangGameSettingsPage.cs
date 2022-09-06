namespace Hi3Helper
{
    public static partial class Locale
    {
        #region GameSettingsPage
        public partial class LocalizationParams
        {
            public LangGameSettingsPage _GameSettingsPage { get; set; } = LangFallback?._GameSettingsPage;
            public class LangGameSettingsPage
            {
                public string PageTitle { get; set; } = LangFallback?._GameSettingsPage.PageTitle;
                public string Graphics_Title { get; set; } = LangFallback?._GameSettingsPage.Graphics_Title;
                public string Graphics_ResolutionPanel { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResolutionPanel;
                public string Graphics_Fullscreen { get; set; } = LangFallback?._GameSettingsPage.Graphics_Fullscreen;
                public string Graphics_ExclusiveFullscreen { get; set; } = LangFallback?._GameSettingsPage.Graphics_ExclusiveFullscreen;
                public string Graphics_ResSelectPlaceholder { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResSelectPlaceholder;
                public string Graphics_ResCustom { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResCustom;
                public string Graphics_ResCustomW { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResCustomW;
                public string Graphics_ResCustomH { get; set; } = LangFallback?._GameSettingsPage.Graphics_ResCustomH;
                public string Graphics_FPSPanel { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSPanel;
                public string Graphics_FPSInCombat { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSInCombat;
                public string Graphics_FPSInMenu { get; set; } = LangFallback?._GameSettingsPage.Graphics_FPSInMenu;
                public string Graphics_APIPanel { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIPanel;
                public string Graphics_APIHelp1 { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIHelp1;
                public string Graphics_APIHelp2 { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIHelp2;
                public string Graphics_APIHelp3 { get; set; } = LangFallback?._GameSettingsPage.Graphics_APIHelp3;
                public string Graphics_SpecPanel { get; set; } = LangFallback?._GameSettingsPage.Graphics_SpecPanel;
                public string Graphics_Render { get; set; } = LangFallback?._GameSettingsPage.Graphics_Render;
                public string Graphics_Shadow { get; set; } = LangFallback?._GameSettingsPage.Graphics_Shadow;
                public string Graphics_Reflection { get; set; } = LangFallback?._GameSettingsPage.Graphics_Reflection;
                public string Graphics_FX { get; set; } = LangFallback?._GameSettingsPage.Graphics_FX;
                public string Graphics_FXPost { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXPost;
                public string Graphics_FXPhysics { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXPhysics;
                public string Graphics_FXHDR { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXHDR;
                public string Graphics_FXHQ { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXHQ;
                public string Graphics_FXAA { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXAA;
                public string Graphics_FXDistort { get; set; } = LangFallback?._GameSettingsPage.Graphics_FXDistort;
                public string Graphics_APHO2Panel { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2Panel;
                public string Graphics_APHO2GI { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2GI;
                public string Graphics_APHO2VL { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2VL;
                public string Graphics_APHO2AO { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2AO;
                public string Graphics_APHO2LOD { get; set; } = LangFallback?._GameSettingsPage.Graphics_APHO2LOD;
                public string SpecDisabled { get; set; } = LangFallback?._GameSettingsPage.SpecDisabled;
                public string SpecLow { get; set; } = LangFallback?._GameSettingsPage.SpecLow;
                public string SpecMedium { get; set; } = LangFallback?._GameSettingsPage.SpecMedium;
                public string SpecHigh { get; set; } = LangFallback?._GameSettingsPage.SpecHigh;
                public string SpecVeryHigh { get; set; } = LangFallback?._GameSettingsPage.SpecVeryHigh;
                public string SpecMaximum { get; set; } = LangFallback?._GameSettingsPage.SpecMaximum;
                public string Audio_Title { get; set; } = LangFallback?._GameSettingsPage.Audio_Title;
                public string Audio_BGM { get; set; } = LangFallback?._GameSettingsPage.Audio_BGM;
                public string Audio_SFX { get; set; } = LangFallback?._GameSettingsPage.Audio_SFX;
                public string Audio_VOLang { get; set; } = LangFallback?._GameSettingsPage.Audio_VOLang;
                public string Audio_VOLang1 { get; set; } = LangFallback?._GameSettingsPage.Audio_VOLang1;
                public string Audio_VOLang2 { get; set; } = LangFallback?._GameSettingsPage.Audio_VOLang2;
                public string Audio_VODefault { get; set; } = LangFallback?._GameSettingsPage.Audio_VODefault;
                public string Audio_VO { get; set; } = LangFallback?._GameSettingsPage.Audio_VO;
                public string Audio_Elf { get; set; } = LangFallback?._GameSettingsPage.Audio_Elf;
                public string Audio_Cutscenes { get; set; } = LangFallback?._GameSettingsPage.Audio_Cutscenes;
                public string ApplyBtn { get; set; } = LangFallback?._GameSettingsPage.ApplyBtn;
                public string SettingsApplied { get; set; } = LangFallback?._GameSettingsPage.SettingsApplied;
                public string OverlayNotInstalledTitle { get; set; } = LangFallback?._GameSettingsPage.OverlayNotInstalledTitle;
                public string OverlayNotInstalledSubtitle { get; set; } = LangFallback?._GameSettingsPage.OverlayNotInstalledSubtitle;
                public string OverlayGameRunningTitle { get; set; } = LangFallback?._GameSettingsPage.OverlayGameRunningTitle;
                public string OverlayGameRunningSubtitle { get; set; } = LangFallback?._GameSettingsPage.OverlayGameRunningSubtitle;
                public string OverlayFirstTimeTitle { get; set; } = LangFallback?._GameSettingsPage.OverlayFirstTimeTitle;
                public string OverlayFirstTimeSubtitle { get; set; } = LangFallback?._GameSettingsPage.OverlayFirstTimeSubtitle;
                public string CustomArgs_Title { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Title;
                public string CustomArgs_Subtitle { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Subtitle;
                public string CustomArgs_Footer1 { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Footer1;
                public string CustomArgs_Footer2 { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Footer2;
                public string CustomArgs_Footer3 { get; set; } = LangFallback?._GameSettingsPage.CustomArgs_Footer3;
            }
        }
        #endregion
    }
}
