// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
namespace CollapseLauncher.GameSettings.Zenless.Enums;

/// <summary>
/// Represent int for corresponding text languages <br/>
/// Default : -1 (Unset)
/// </summary>
public enum LanguageText
{
    Unset = -1,
    en_us = 1,
    zh_cn = 2,
    zh_tw = 3,
    fr_fr = 4,
    de_de = 5,
    es_es = 6,
    pt_pt = 7,
    ru_ru = 8,
    ja_jp = 9,
    ko_kr = 10,
    th_th = 11,
    vi_vn = 12,
    id_id = 13
}

/// <summary>
/// Represent int for corresponding audio languages <br/>
/// Default : -1 (Unset)
/// </summary>
public enum LanguageVoice
{
    Unset = -1,
    en_us = 1,
    zh_cn = 2,
    ja_jp = 3,
    ko_kr = 4
}

/// <summary>
/// Represent int for graphics preset options <br/>
/// Needs to be set to 4 (Custom) for other Graphics options to apply <br/>
/// Default : 2 (Medium)
/// </summary>
public enum GraphicsPresetOption
{
    High,
    Medium,
    Low,
    Custom
}

/// <summary>
/// Available options for in-game FPS limiter <br/>
/// Default : 1 (60 FPS)
/// </summary>
public enum FpsOption
{
    Lo30,
    Hi60,
    Unlimited
}

/// <summary>
/// Available options for in-game High-Precision Character Resolution setting <br/>
/// Default : Off (on Low and Medium Preset), Dynamic (on High Preset)
/// </summary>
public enum HiPrecisionCharaAnimOption
{
    Off,
    Dynamic,
    Global
}

/// <summary>
/// Available options for render resolutions. 0.8, 1.0, 1.2 <br/>
/// Default : 1 (1.0)
/// </summary>
public enum RenderResOption
{
    f08,
    f10,
    f12
}

/// <summary>
/// Available options for AntiAliasing <br/>
/// Default : TAA
/// </summary>
public enum AntiAliasingOption
{
    Off,
    TAA,
    SMAA
}

/// <summary>
/// Available options for graphics settings that has 2 options <br/>
/// Low, High
/// </summary>
public enum QualityOption2
{
    Low,
    High
}

/// <summary>
/// Available options for graphics settings that has 3 options <br/>
/// Low, Medium, High
/// </summary>
public enum QualityOption3
{
    Low,
    Medium,
    High
}

/// <summary>
/// Available options for graphics settings that has 4 options <br/>
/// Off, Low, Medium, High
/// </summary>
public enum QualityOption4
{
    Off,
    Low,
    Medium,
    High
}

/// <summary>
/// Available options for graphics settings that has 4 options starting from Very Low <br/>
/// VeryLow, Low, Medium, High
/// </summary>
public enum QualityOption5
{
    VeryLow,
    Low,
    Medium,
    High
}

/// <summary>
/// Available options for Audio Playback Device. Alters sound profile <br/>
/// Default : Headphones, Options: Headphones, Speakers, TV
/// </summary>
public enum AudioPlaybackDevice
{
    Headphones,
    Speakers,
    TV = 3
}

public static class ServerName
{
    public const string Europe   = "prod_gf_eu";
    public const string America  = "prod_gf_us";
    public const string Asia     = "prod_gf_jp";
    public const string TW_HK_MO = "prod_gf_sg";
    // TODO : find sn for CN regions
}
