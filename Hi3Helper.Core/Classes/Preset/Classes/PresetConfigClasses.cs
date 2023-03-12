using Hi3Helper.Shared.ClassStruct;
using System;

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

    public class PkgVersionProperties
    {
        public string localName { get; set; }
        public string remoteURL { get; set; }
        public string remoteName { get; set; }
        public string md5 { get; set; }
        public long fileSize { get; set; }
        public bool isPatch { get; set; } = false;
        public string type { get; set; }

        public FilePropertiesRemote AsFilePropertiesRemote() => new FilePropertiesRemote
        {
            N = remoteName,
            CRC = md5,
            S = fileSize,
            FT = FileType.Generic
        };
    }
}
