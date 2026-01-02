using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Streams;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail JSON-based Metadata parser for AudioV and VideoV. This parser read-only and cannot be written back.<br/>
/// </summary>
public sealed partial class StarRailAssetJsonMetadata : StarRailAssetBinaryMetadata<StarRailAssetJsonMetadata.Metadata>
{
    public StarRailAssetJsonMetadata()
        : base(0, // Leave the rest of it to 0 as this metadata has JSON struct
               0,
               0,
               0,
               0)
    { }

    protected override ReadOnlySpan<byte> MagicSignature => "\0\0\0\0"u8;

    protected override ValueTask<(StarRailBinaryDataHeaderStruct Header, int Offset)>
        ReadHeaderCoreAsync(Stream            dataStream,
                            CancellationToken token = default)
    {
        return ValueTask.FromResult((default(StarRailBinaryDataHeaderStruct), 0));
    }

    protected override async ValueTask<long> ReadDataCoreAsync(
        long              currentOffset,
        Stream            dataStream,
        CancellationToken token = default)
    {
        // -- Allocate list
        DataList = [];

        // -- Read list
        await using NullPositionTrackableStream trackingNullStream = new();
        await using CopyToStream                bridgeStream       = new(dataStream, trackingNullStream, null, false);
        using StreamReader                      reader             = new(bridgeStream, leaveOpen: true);
        while (await reader.ReadLineAsync(token) is { } line)
        {
            Metadata? metadata = JsonSerializer.Deserialize(line, MetadataJsonContext.Default.Metadata);
            if (metadata == null)
            {
                continue;
            }

            DataList.Add(metadata);
        }

        return trackingNullStream.Position;
    }

    [JsonSerializable(typeof(Metadata))]
    public partial class MetadataJsonContext : JsonSerializerContext;

    public class Metadata : StarRailAssetFlaggable
    {
        [JsonPropertyName("Patch")]
        public bool IsPatch { get; init; }

        [JsonPropertyName("SubPackId")]
        public int SubPackId { get; init; }

        [JsonPropertyName("TaskIds")]
        public int[]? TaskIdList { get; init; }

        public override bool IsPersistent => IsPatch;

        public override string ToString() => $"{base.ToString()} | Patch: {IsPatch} | SubPackId: {SubPackId}" +
                                             (TaskIdList?.Length == 0 ? "" : $" | TaskIds: [{string.Join(", ", TaskIdList ?? [])}]");
    }
}
