using Hi3Helper.EncTool.Parser.AssetIndex;
using System.Collections.Generic;
// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace

#nullable enable
namespace Hi3Helper.Shared.ClassStruct
{
    public class YSDispatchInfo
    {
        public string? content { get; set; }
        public string? sign { get; set; }
    }

    public class QueryProperty
    {
        public string? GameServerName { get; set; }
        public string? ClientGameResURL { get; set; }
        public string? ClientDesignDataURL { get; set; }
        public string? ClientDesignDataSilURL { get; set; }
        public string? ClientAudioAssetsURL { get; set; }
        public uint AudioRevisionNum { get; set; }
        public uint DataRevisionNum { get; set; }
        public uint ResRevisionNum { get; set; }
        public uint SilenceRevisionNum { get; set; }
        public string? GameVersion { get; set; }
        public string? ChannelName { get; set; }
        public IEnumerable<PkgVersionProperties?>? ClientGameRes { get; set; }
        public PkgVersionProperties? ClientDesignData { get; set; }
        public PkgVersionProperties? ClientDesignDataSil { get; set; }
    }
}
