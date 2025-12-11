using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

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
                public string Mark_Experimental { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Mark_Experimental;

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

                public string Graphics_HighPrecisionCharacterAnimation { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Graphics_HighPrecisionCharacterAnimation;

                public string Audio_PlaybackDev { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev;
                
                public string Audio_PlaybackDev_Headphones { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev_Headphones;
                
                public string Audio_PlaybackDev_Speakers { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev_Speakers;
                
                public string Audio_PlaybackDev_TV { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_PlaybackDev_TV;

                public string Audio_Ambient { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.Audio_Ambient;

                public string AdvancedGraphics_Title { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_Title;

                public string AdvancedGraphics_Tooltip1 { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_Tooltip1;

                public string AdvancedGraphics_Tooltip2 { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_Tooltip2;

                public string AdvancedGraphics_UseDirectX12API { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_UseDirectX12API;

                public string AdvancedGraphics_RayTracing { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_RayTracing;

                public string AdvancedGraphics_RayTracingQ { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_RayTracingQ;

                public string AdvancedGraphics_SuperScaling { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_SuperScaling;

                public string AdvancedGraphics_SuperScalingQ { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_SuperScalingQ;

                public string AdvancedGraphics_SuperScalingQ_Performance { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_SuperScalingQ_Performance;

                public string AdvancedGraphics_SuperScalingQ_Balanced { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_SuperScalingQ_Balanced;

                public string AdvancedGraphics_SuperScalingQ_Quality { get; set; } =
                    LangFallback?._ZenlessGameSettingsPage.AdvancedGraphics_SuperScalingQ_Quality;
            }
        }
    }
}
