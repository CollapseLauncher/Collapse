using Hi3Helper.EncTool;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail Asset Bundle Metadata (SRAM) data parser for Start_AsbV and AsbV. This parser read-only and cannot be written back.<br/>
/// </summary>
public sealed class StarRailAssetBundleMetadata : StarRailAssetBinaryMetadata<StarRailAssetBlockMetadata.Metadata>
{
    public StarRailAssetBundleMetadata()
        : base(1280,
               256,
               40,
               8,
               16) { }

    private static     ReadOnlySpan<byte> MagicSignatureStatic => "SRAM"u8;
    protected override ReadOnlySpan<byte> MagicSignature       => MagicSignatureStatic;

    protected override async ValueTask<(StarRailBinaryDataHeaderStruct Header, int Offset)>
        ReadHeaderCoreAsync(
            Stream            dataStream,
            CancellationToken token = default)
    {
        return await dataStream
           .ReadDataAssertWithPosAndSeekAsync<StarRailBinaryDataHeaderStruct> // Use ReadDataAssertWithPosAndSeekAsync for SRAM
                (0,
                 x => x.HeaderOrDataLength,
                 token);
    }

    protected override async ValueTask<long> ReadDataCoreAsync(
        long              currentOffset,
        Stream            dataStream,
        CancellationToken token = default)
    {
        // -- Read the first data section header.
        (DataSectionHeaderStruct headerStructs, int read) =
            await dataStream.ReadDataAssertAndSeekAsync<DataSectionHeaderStruct>
                (_ => Header.SubStructSize,
                 token);
        currentOffset += read;

        // -- Skip other data section header.
        int remainedBufferSize = Header.SubStructSize * (Header.SubStructCount - 1);
        currentOffset += await dataStream.SeekForwardAsync(remainedBufferSize, token);

        // ASSERT: Make sure we are at data start offset before reading.
        Debug.Assert(currentOffset == headerStructs.DataStartOffset);

        // -- Read data buffer.
        int    dataBufferLen = headerStructs.DataCount * headerStructs.DataSize;
        byte[] dataBuffer    = ArrayPool<byte>.Shared.Rent(dataBufferLen);
        try
        {
            // -- Read data buffer
            currentOffset += await dataStream.ReadBufferAssertAsync(currentOffset,
                                                                    dataBuffer.AsMemory(0, dataBufferLen),
                                                                    token);

            // -- Create span
            ReadOnlySpan<byte> dataBufferSpan = dataBuffer.AsSpan(0, dataBufferLen);

            // -- Allocate list
            DataList = new List<StarRailAssetBlockMetadata.Metadata>(headerStructs.DataCount);
            while (!dataBufferSpan.IsEmpty)
            {
                dataBufferSpan = StarRailAssetBlockMetadata
                                .Metadata
                                .Parse(dataBufferSpan,
                                       headerStructs.DataSize,
                                       out StarRailAssetBlockMetadata.Metadata result);
                DataList.Add(result);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(dataBuffer);
        }

        return currentOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct DataSectionHeaderStruct
    {
        public int Reserved;

        /// <summary>
        /// The absolute offset of data array in the data stream.
        /// </summary>
        public int DataStartOffset;

        /// <summary>
        /// The count of data array in the data stream.
        /// </summary>
        public int DataCount;

        /// <summary>
        /// The size of the one data inside the array.
        /// </summary>
        public int DataSize;
    }
}
