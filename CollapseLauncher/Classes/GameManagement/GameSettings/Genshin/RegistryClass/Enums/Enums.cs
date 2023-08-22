using System.Collections.Generic;
using System.Collections.Immutable;

namespace CollapseLauncher.GameSettings.Genshin.Enums
{
    internal static class DictionaryCategory
    {
        // HoYoooooooo :wogreeee:
        internal static ImmutableDictionary<double, int> RenderResolutionOption = new Dictionary<double, int>
        {
            { 0.6d, 1 },
            { 0.8d, 2 },
            { 0.9d, 9 },
            { 1.0d, 3 },
            { 1.1d, 4 },
            { 1.2d, 5 },
            { 1.3d, 6 },
            { 1.4d, 7 },
            { 1.5d, 8 }
        }.ToImmutableDictionary();
    }

    enum FPSOption : int
    {
        f30 = 1,
        f60 = 2,
        f45 = 3
    }

    enum ShadowQualityOption
    {
        Lowest = 1,
        Low = 2,
        Medium = 3,
        High = 4
    }

    enum VisualEffectsOption
    {
        Lowest = 1,
        Low = 2,
        Medium = 3,
        High = 4
    }

    enum SFXQualityOption
    {
        Lowest = 1,
        Low = 2,
        Medium = 3,
        High = 4
    }

    enum EnvironmentDetailOption
    {
        Lowest = 1,
        Low = 2,
        Medium = 3,
        High = 4,
        Highest = 5
    }

    enum VerticalSyncOption
    {
        Off = 1,
        On = 2
    }

    enum AntialiasingOption
    {
        Off = 1,
        FSR2 = 2,
        SMAA = 3
    }

    enum VolumetricFogOption
    {
        Off = 1,
        On = 2
    }

    enum ReflectionsOption
    {
        Off = 1,
        On = 2
    }

    enum MotionBlurOption
    {
        Off = 1,
        Low = 2,
        High = 3,
        Extreme = 4
    }

    enum BloomOption
    {
        Off = 1,
        On = 2
    }

    enum CrowdDensityOption
    {
        Low = 1,
        High = 2
    }

    enum SubsurfaceScatteringOption
    {
        Off = 1,
        Medium = 2,
        High = 3
    }

    enum CoOpTeammateEffectsOption
    {
        Off = 1,
        PartiallyOff = 2,
        On = 3
    }

    enum AnisotropicFilteringOption
    {
        x1 = 1,
        x2 = 2,
        x4 = 3,
        x8 = 4,
        x16 = 5
    }

    enum GlobalIlluminationOption
    {
        Off = 1,
        Medium = 2,
        High = 3,
        Extreme = 4
    }
}

