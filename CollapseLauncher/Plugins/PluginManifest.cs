using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Plugin.Core.Utility.Json.Converters;
using System;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Plugins;

#nullable enable
[JsonSerializable(typeof(PluginManifest))]
public partial class PluginManifestJsonContext : JsonSerializerContext;

public class PluginManifest
{
    public required string  MainLibraryName       { get; init; }
    public required string  MainPluginName        { get; init; }
    public required string  MainPluginAuthor      { get; init; }
    public          string? MainPluginDescription { get; init; }

    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion PluginStandardVersion { get; init; }

    [JsonConverter(typeof(Utf8SpanParsableJsonConverter<GameVersion>))]
    public GameVersion PluginVersion         { get; init; }

    [JsonConverter(typeof(Utf16SpanParsableJsonConverter<DateTimeOffset>))]
    public DateTimeOffset PluginCreationDate    { get; init; }
    public DateTimeOffset ManifestDate          { get; init; }
}
