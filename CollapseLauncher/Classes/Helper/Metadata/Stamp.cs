#nullable enable
using System;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.Metadata
{
    public class Stamp
    {
        public long LastUpdated { get; set; }
        public string? MetadataPath { get; set; } = null;
        public MetadataType? MetadataType { get; set; } = null;
        public bool? MetadataInclude { get; set; } = null;
        public string? GameName { get; set; } = null;
        public string? GameRegion { get; set; } = null;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime LastModifiedTimeUtc { get; set; } = default;
        public string? PresetConfigVersion { get; set; }
    }
}
