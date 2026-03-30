using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.Metadata
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(List<Stamp>))]
    internal sealed partial class StampJsonContext : JsonSerializerContext;

    public sealed class Stamp
    {
        public          long         LastUpdated     { get; init; }
        public required string       MetadataPath    { get; init; }
        public          MetadataType MetadataType    { get; init; }
        public          bool         MetadataInclude { get; init; }
        public          string?      GameName        { get; init; }
        public          string?      GameRegion      { get; init; }

        [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
        public GameVersion PresetConfigVersion { get; init; }

        [JsonIgnore]
        public DateTime LastModifiedTimeUtc { get; private set; }

        public override string ToString() => string.Join(" - ", GameName, GameRegion);

        public override int GetHashCode() =>
            HashCode.Combine(LastUpdated, MetadataPath, MetadataType, MetadataInclude, GameName, GameRegion);

        public void SetFileModifiedTime(string configDir)
            => LastModifiedTimeUtc = GetCurrentFileModifiedTime(configDir);

        public DateTime GetCurrentFileModifiedTime(string configDir)
        {
            string filePath = Path.Combine(configDir, MetadataPath);
            return File.Exists(filePath)
                ? File.GetLastWriteTimeUtc(filePath)
                : default;
        }
    }
}
