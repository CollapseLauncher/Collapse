//using System;
using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

namespace Hi3HelperGUI.Preset
{
    public class PresetConfigClasses
    {
        public string ProfileName { get; set; }
        public string ZoneName { get; set; }
        public string InstallRegistryLocation { get; set; }
        public string ConfigRegistryLocation { get; set; }
        public string ActualGameLocation { get; set; }
        public string DefaultGameLocation { get; set; }
        public string miHoYoAssetBundleAddress { get; set; }
        public string miHoYoAssetBigFileAddress { get; set; }
        public string Hi3MirrorAssetBundleAddress { get; set; }
        public string Hi3MirrorAssetBigFileAddress { get; set; }
        public List<string> LanguageAvailable { get; set; }
        public string UsedLanguage { get; set; }
        public string FallbackLanguage { get; set; }
    }

    public class AppSettings
    {
        public bool ShowConsole { get; set; }
        public ushort SupportedGameVersion { get; set; }
        public ushort PreviousGameVersion { get; set; }
    }

    public class UpdateDataProperties
    {
        public string N { get; set; }
        public long CS { get; set; }
        public long ECS { get; set; }
        public string CRC { get; set; }
        public string ActualPath { get; set; }
        public string HumanizeSize { get; set; }
        public string RemotePath { get; set; }
        public string ZoneName { get; set; }
        public string DataType { get; set; }
        public string DownloadStatus { get; set; } = "Not yet downloaded";
    }
}
