using System.Collections.Generic;
using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region Misc
        public sealed partial class LocalizationParams
        {
            public LangOOBEStartUpMenu _OOBEStartUpMenu { get; set; } = LangFallback?._OOBEStartUpMenu;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangOOBEStartUpMenu
            {
                public Dictionary<string, string[]> WelcomeTitleString { get; set; } = LangFallback?._OOBEStartUpMenu.WelcomeTitleString;
                public string SetupNextButton { get; set; } = LangFallback?._OOBEStartUpMenu.SetupNextButton;
                public string SetupBackButton { get; set; } = LangFallback?._OOBEStartUpMenu.SetupBackButton;

                public string CustomizationTitle { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationTitle;
                public string CustomizationSettingsLanguageHeader { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsLanguageHeader;
                public string CustomizationSettingsLanguageDescription { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsLanguageDescription;
                public string CustomizationSettingsWindowSizeHeader { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsWindowSizeHeader;
                public string CustomizationSettingsWindowSizeDescription { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsWindowSizeDescription;
                public string CustomizationSettingsCDNHeader { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsCDNHeader;
                public string CustomizationSettingsCDNDescription { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsCDNDescription;
                public string CustomizationSettingsStyleHeader { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsStyleHeader;
                public string CustomizationSettingsStyleDescription { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsStyleDescription;
                public string CustomizationSettingsStyleThemeHeader { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsStyleThemeHeader;
                public string CustomizationSettingsStyleThemeDescription { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsStyleThemeDescription;
                public string CustomizationSettingsStyleCustomBackgroundHeader { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsStyleCustomBackgroundHeader;
                public string CustomizationSettingsStyleCustomBackgroundDescription { get; set; } = LangFallback?._OOBEStartUpMenu.CustomizationSettingsStyleCustomBackgroundDescription;

                public string VideoBackgroundPreviewUnavailableHeader { get; set; } = LangFallback?._OOBEStartUpMenu.VideoBackgroundPreviewUnavailableHeader;
                public string VideoBackgroundPreviewUnavailableDescription { get; set; } = LangFallback?._OOBEStartUpMenu.VideoBackgroundPreviewUnavailableDescription;

                public string LoadingInitializationTitle { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingInitializationTitle;
                public string LoadingCDNCheckboxCheckLatency { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingCDNCheckboxCheckLatency;
                public string LoadingCDNCheckboxPlaceholder { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingCDNCheckboxPlaceholder;
                public string LoadingCDNCheckingSubitle { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingCDNCheckingSubitle;
                public string LoadingCDNCheckingSkipButton { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingCDNCheckingSkipButton;
                public string LoadingBackgroundImageTitle { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingBackgroundImageTitle;
                public string LoadingBackgroundImageSubtitle { get; set; } = LangFallback?._OOBEStartUpMenu.LoadingBackgroundImageSubtitle;

                public string CDNCheckboxItemLatencyFormat { get; set; } = LangFallback?._OOBEStartUpMenu.CDNCheckboxItemLatencyFormat;
                public string CDNCheckboxItemLatencyUnknownFormat { get; set; } = LangFallback?._OOBEStartUpMenu.CDNCheckboxItemLatencyUnknownFormat;
                public string CDNCheckboxItemLatencyRecommendedFormat { get; set; } = LangFallback?._OOBEStartUpMenu.CDNCheckboxItemLatencyRecommendedFormat;
            }
        }
        #endregion
    }
}
