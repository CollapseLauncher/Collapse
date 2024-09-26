// ReSharper disable InconsistentNaming
using WinRT;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        public sealed partial class LocalizationParams
        {
            public LangZenlessGameSettingsPage _ZenlessGameSettingsPage { get; set; } =
                LangFallback?._ZenlessGameSettingsPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangZenlessGameSettingsPage
            {
                public string Graphics_ColorFilter { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Graphics_ColorFilter;

                public string Graphics_RenderRes { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Graphics_RenderRes;

                public string Graphics_EffectsQ { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Graphics_EffectsQ;

                public string Graphics_ShadingQ { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Graphics_ShadingQ;

                public string Graphics_Distortion { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Graphics_Distortion;

                public string Audio_PlaybackDev { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev;
                
                public string Audio_PlaybackDev_Headphones { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev_Headphones;
                
                public string Audio_PlaybackDev_Speakers { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev_Speakers;
                
                public string Audio_PlaybackDev_TV { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev_TV;
            }
        }
    }
}