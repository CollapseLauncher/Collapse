using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Plugins;

#nullable enable
[JsonSerializable(typeof(PluginManifest))]
public partial class PluginManifestJsonContext : JsonSerializerContext;

public class PluginManifest
{
    public required string  MainLibraryName       { get; init; }
    public required string  MainPluginName        { get; init; }
    public          string? MainPluginAuthor      { get; init; }
    public          string? MainPluginDescription { get; init; }

    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion PluginStandardVersion { get; init; }

    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion PluginVersion { get; init; }

    [JsonConverter(typeof(Utf16SpanParsableJsonConverter<DateTimeOffset>))]
    public DateTimeOffset PluginCreationDate { get; init; }

    [JsonConverter(typeof(Utf16SpanParsableJsonConverter<DateTimeOffset>))]
    public DateTimeOffset ManifestDate { get; init; }

    public required List<PluginManifestAsset> Assets { get; init; }
}

public class PluginManifestAsset
{
    public          string? FilePath { get; set; }
    public          long    Size     { get; set; }
    public required byte[]  FileHash { get; set; }
}
