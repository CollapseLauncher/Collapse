using Hi3Helper.Data;
using Hi3Helper.EncTool;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail Signatureless Metadata parser for LuaV, DesignV. This parser is read-only and cannot be written back.<br/>
/// </summary>
public sealed class StarRailAssetSignaturelessMetadata : StarRailAssetBinaryMetadata<StarRailAssetSignaturelessMetadata.Metadata>
{
    public StarRailAssetSignaturelessMetadata() : this(null)
    {
    }

    public StarRailAssetSignaturelessMetadata(string? customAssetExtension = null)
        : base(0,
               256,
               0, // Leave the rest of it to 0 as this metadata has non-consistent header struct
               0,
               0)
    {
        AssetExtension = customAssetExtension ?? ".block";
    }

    private string AssetExtension { get; }

    protected override ReadOnlySpan<byte> MagicSignature => [0x00, 0x00, 0x00, 0xFF];

    protected override async ValueTask<(StarRailBinaryDataHeaderStruct Header, int Offset)>
        ReadHeaderCoreAsync(
            Stream            dataStream,
            CancellationToken token = default)
    {
        // We manipulate the Header to only read its first 8 bytes so
        // the signature assertion can be bypassed as the other data
        // is needed to be read by ReadDataCoreAsync, we don't want to
        // read the whole 16 bytes of it.
        byte[]                         first8BytesBuffer = new byte[8];
        StarRailBinaryDataHeaderStruct header            = default;

        _ = await dataStream.ReadAtLeastAsync(first8BytesBuffer,
                                              first8BytesBuffer.Length,
                                              cancellationToken: token);

        CopyHeaderUnsafe(ref header, first8BytesBuffer);

        return (header, first8BytesBuffer.Length);

        static unsafe void CopyHeaderUnsafe(scoped ref StarRailBinaryDataHeaderStruct target,
                                            scoped     Span<byte>                     dataBuffer)
            => dataBuffer.CopyTo(new Span<byte>(Unsafe.AsPointer(in target), dataBuffer.Length));
    }

    protected override async ValueTask<long> ReadDataCoreAsync(
        long              currentOffset,
        Stream            dataStream,
        CancellationToken token = default)
    {
        // In this read data section, we will read the data manually since
        // the entire data is Big-endian ordered.

        // -- Read the count from the header
        byte[] countBuffer = new byte[8];
        currentOffset += await dataStream.ReadAtLeastAsync(countBuffer,
                                                           countBuffer.Length,
                                                           cancellationToken: token)
                                         .ConfigureAwait(false);

        Span<byte> countBufferSpan        = countBuffer;
        int        parentDataCount        = BinaryPrimitives.ReadInt32BigEndian(countBufferSpan);

        // -- Declare constant length for each parent and children data
        const int parentDataBufferLen   = 32;
        const int childrenDataBufferLen = 12;

        // -- Read the rest of the data buffer and parse it
        byte[]       parentDataBuffer       = ArrayPool<byte>.Shared.Rent(parentDataBufferLen);
        Memory<byte> parentDataBufferMemory = parentDataBuffer.AsMemory(0, parentDataBufferLen);

        // -- Allocate list
        DataList = new List<Metadata>(parentDataCount);

        try
        {
            for (int i = 0; i < parentDataCount; i++)
            {
                long lastPos = currentOffset;
                // -- Parse data and add to list
                currentOffset += await dataStream.ReadAtLeastAsync(parentDataBufferMemory,
                                                                   parentDataBufferMemory.Length,
                                                                   cancellationToken: token)
                                                 .ConfigureAwait(false);
                Metadata.Parse(parentDataBuffer,
                               AssetExtension,
                               childrenDataBufferLen,
                               lastPos,
                               out int bytesToSkip,
                               out Metadata result);
                DataList.Add(result);

                // -- Skip children data
                currentOffset += await dataStream.SeekForwardAsync(bytesToSkip, token)
                                                 .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(parentDataBuffer);
        }

        return currentOffset;
    }

    public class Metadata : StarRailAssetFlaggable
    {
        public static void Parse(ReadOnlySpan<byte> buffer,
                                 string             assetExtension,
                                 int                subDataSize,
                                 long               lastDataStreamPos,
                                 out int            bytesToSkip,
                                 out Metadata       result)
        {
            // uint firstAssetId = BinaryPrimitives.ReadUInt32BigEndian(buffer);
            uint assetType    = BinaryPrimitives.ReadUInt32BigEndian(buffer[20..]);
            long fileSize     = BinaryPrimitives.ReadUInt32BigEndian(buffer[24..]);
            int  subDataCount = BinaryPrimitives.ReadInt32BigEndian(buffer[28..]);

            byte[] md5Hash = new byte[16];
            buffer[4..20].CopyTo(md5Hash);

            // subDataCount = Number of sub-data struct count
            // subDataSize  = Number of sub-data struct size
            // 1            = Unknown offset, seek +1
            bytesToSkip = subDataCount * subDataSize + 1;
            result = new Metadata
            {
                MD5Checksum = md5Hash,
                Filename    = $"{HexTool.BytesToHexUnsafe(md5Hash)}{assetExtension}",
                FileSize    = fileSize,
                Flags       = assetType
            };
        }
    }
}
