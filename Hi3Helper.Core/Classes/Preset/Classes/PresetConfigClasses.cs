namespace Hi3Helper.Preset
{
    public class BHI3LInfo
    {
        public BHI3LInfo_GameInfo game_info { get; set; }
    }

    public class BHI3LInfo_GameInfo
    {
        public string version { get; set; }
        public string install_path { get; set; }
        public bool installed { get; set; }
    }
}
