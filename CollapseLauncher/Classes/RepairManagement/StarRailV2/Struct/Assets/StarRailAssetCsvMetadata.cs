using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Streams;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail Comma-Separated Value Metadata (CSV) data parser for IFixV. This parser is read-only and cannot be written back.<br/>
/// </summary>
public sealed class StarRailAssetCsvMetadata : StarRailAssetBinaryMetadata<StarRailAssetCsvMetadata.Metadata>
{
    public StarRailAssetCsvMetadata()
        : base(0, // Leave the rest of it to 0 as this metadata is a Comma-Separated Value (CSV) format
               0,
               0,
               0,
               0) { }

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
            if (!Metadata.Parse(line, out Metadata result))
            {
                continue;
            }
            DataList.Add(result);
        }

        return trackingNullStream.Position;
    }

    public class Metadata : StarRailAssetFlaggable
    {
        public static bool Parse(ReadOnlySpan<char> line, out Metadata result)
        {
            Unsafe.SkipInit(out result);
            if (line.IsEmpty ||
                line.IsWhiteSpace())
            {
                return false;
            }

            const string             separators = ",;"; // Include ; as well, just in case.
            const StringSplitOptions options = StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries;

            Span<Range> ranges    = stackalloc Range[8];
            int         rangesLen = line.SplitAny(ranges, separators, options);
            if (rangesLen < 3)
            {
                throw new InvalidOperationException("Format has been changed! Please report this issue to our devs!");
            }

            ReadOnlySpan<char> filePath    = line[ranges[0]];
            ReadOnlySpan<char> hashStr     = line[ranges[1]];
            ReadOnlySpan<char> fileSizeStr = line[ranges[2]];

            byte[] hash = new byte[16];
            if (!HexTool.TryHexToBytesUnsafe(hashStr, hash) ||
                !uint.TryParse(fileSizeStr, out uint fileSize))
            {
                throw new InvalidOperationException($"Cannot parse values for this current line: {line} Please report this issue to our devs!");
            }

            result = new Metadata
            {
                Filename    = filePath.ToString(),
                FileSize    = fileSize,
                MD5Checksum = hash
            };
            return true;
        }
    }
}
