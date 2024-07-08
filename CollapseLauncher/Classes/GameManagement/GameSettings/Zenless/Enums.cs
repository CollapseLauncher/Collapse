// ReSharper disable InconsistentNaming
namespace CollapseLauncher.GameSettings.Zenless.Enums;

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

public enum LanguageVoice
{
    Unset = -1,
    en_us = 1,
    zh_cn = 2,
    ja_jp = 3,
    ko_kr = 4
}

public enum FpsOption
{
    Lo30,
    Hi60,
    Unlimited
}

public enum RenderResOption
{
    f08,
    f10,
    f12
}

public enum AntiAliasingOption
{
    Off,
    TAA,
    SMAA
}

public enum QualityOption2
{
    Low,
    High
}

public enum QualityOption3
{
    Low,
    Medium,
    High
}

public enum QualityOption4
{
    Off,
    Low,
    Medium,
    High
}

public enum AudioPlaybackDevice
{
    Headphones,
    Speakers,
    TV
}

public static class ServerName
{
    public const string Europe   = "prod_gf_eu";
    public const string America  = "prod_gf_us";
    public const string Asia     = "prod_gf_jp";
    public const string TW_HK_MO = "prod_gf_sg";
    // TODO : find sn for CN regions
}
