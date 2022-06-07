namespace Hi3Helper.Shared.ClassStruct
{
    public class RegionBackgroundProp
    {
        public RegionBackgroundProp_data data { get; set; }
        public string imgLocalPath { get; set; } = string.Empty;
    }

    public class RegionBackgroundProp_data
    {
        public RegionBackgroundProp_data_adv adv { get; set; }
    }

    public class RegionBackgroundProp_data_adv
    {
        public string background { get; set; }
        public string bg_checksum { get; set; }
    }
}
