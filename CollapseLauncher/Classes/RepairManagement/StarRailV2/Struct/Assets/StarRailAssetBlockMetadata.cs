using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

/// <summary>
/// Star Rail Binary Metadata (SRBM) data parser for Start_BlockV and BlockV. This parser is read-only and cannot be written back.<br/>
/// </summary>
public sealed class StarRailAssetBlockMetadata : StarRailAssetBinaryMetadata<StarRailAssetBlockMetadata.Metadata>
{
    public StarRailAssetBlockMetadata()
        : base(256,
               768,
               16,
               2,
               12)
    { }

    protected override async ValueTask<long> ReadDataCoreAsync(
        long              currentOffset,
        Stream            dataStream,
        CancellationToken token = default)
    {
        (DataSectionHeaderStruct dataHeader, int readHeader) =
            await dataStream
                 .ReadDataAssertAndSeekAsync<DataSectionHeaderStruct>(_ => Header.SubStructSize,
                                                                      token)
                 .ConfigureAwait(false);
        currentOffset += readHeader;

        // ASSERT: Make sure the current data stream offset is at the exact data buffer offset.
        Debug.Assert(dataHeader.DataStartOffset == currentOffset);

        int    dataBufferLen = dataHeader.DataSize * dataHeader.DataCount;
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
            DataList = new List<Metadata>(dataHeader.DataCount);
            while (!dataBufferSpan.IsEmpty)
            {
                dataBufferSpan = Metadata.Parse(dataBufferSpan,
                                                dataHeader.DataSize,
                                                out Metadata result);
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
    internal struct DataSectionHeaderStruct
    {
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

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    internal unsafe struct MetadataStruct
    {
        /// <summary>
        /// 4x4 bytes Reordered hash (each 4 bytes reordered as Big-endian).
        /// </summary>
        private fixed byte _shifted4BytesMD5Checksum[16];

        /// <summary>
        /// Defined flags of the asset bundle block file.
        /// </summary>
        public uint Flags;

        /// <summary>
        /// The size of the block file.
        /// </summary>
        public int FileSize;

        /// <summary>
        /// 4x4 bytes Reordered hash (each 4 bytes reordered as Big-endian).
        /// </summary>
        public ReadOnlySpan<byte> Shifted4BytesMD5Checksum
        {
            get
            {
                fixed (byte* magicP = _shifted4BytesMD5Checksum)
                {
                    return new ReadOnlySpan<byte>(magicP, 16);
                }
            }
        }
    }

    public class Metadata : StarRailAssetFlaggable
    {
        public static ReadOnlySpan<byte> Parse(ReadOnlySpan<byte> buffer,
                                               int sizeOfStruct,
                                               out Metadata       result)
        {
            ref readonly MetadataStruct structRef = ref MemoryMarshal.AsRef<MetadataStruct>(buffer);
            byte[]                      md5Buffer = new byte[16];

            structRef.Shifted4BytesMD5Checksum.CopyTo(md5Buffer);
            StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Buffer);

            result = new Metadata
            {
                Filename = $"{HexTool.BytesToHexUnsafe(md5Buffer)}.block",
                FileSize = structRef.FileSize,
                Flags = structRef.Flags,
                MD5Checksum = md5Buffer
            };

            return buffer[sizeOfStruct..];
        }
    }
}
