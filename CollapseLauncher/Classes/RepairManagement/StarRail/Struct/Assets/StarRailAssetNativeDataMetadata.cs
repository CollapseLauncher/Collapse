using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

internal sealed class StarRailAssetNativeDataMetadata : StarRailAssetBinaryMetadata<StarRailAssetNativeDataMetadata.Metadata>
{
    public StarRailAssetNativeDataMetadata()
        : base(256,
               768,
               16,
               2,
               16)
    { }

    protected override async ValueTask<long> ReadDataCoreAsync(
        long              currentOffset,
        Stream            dataStream,
        CancellationToken token = default)
    {
        (DataSectionHeaderStruct fileInfoHeaderStruct, int readHeader) =
            await dataStream
                 .ReadDataAssertAndSeekAsync<DataSectionHeaderStruct>(_ => Header.SubStructSize,
                                                                      token)
                 .ConfigureAwait(false);
        currentOffset += readHeader;

        (DataSectionHeaderStruct filenameHeaderStruct, readHeader) =
            await dataStream
                 .ReadDataAssertAndSeekAsync<DataSectionHeaderStruct>(_ => Header.SubStructSize,
                                                                      token)
                 .ConfigureAwait(false);
        currentOffset += readHeader;

        // ASSERT: Make sure the current data stream offset is at the exact filename buffer offset.
        Debug.Assert(filenameHeaderStruct.DataStartOffset == currentOffset);

        int    filenameBufferLen = filenameHeaderStruct.DataSize * filenameHeaderStruct.DataCount;
        int    fileInfoBufferLen = fileInfoHeaderStruct.DataSize * fileInfoHeaderStruct.DataCount;
        byte[] filenameBuffer    = ArrayPool<byte>.Shared.Rent(filenameBufferLen);
        byte[] fileInfoBuffer    = ArrayPool<byte>.Shared.Rent(fileInfoBufferLen);
        try
        {
            // -- Read filename buffer
            currentOffset += await dataStream.ReadBufferAssertAsync(currentOffset,
                                                                    filenameBuffer.AsMemory(0, filenameBufferLen),
                                                                    token);

            // -- Read file info buffer
            currentOffset += await dataStream.ReadBufferAssertAsync(currentOffset,
                                                                    fileInfoBuffer.AsMemory(0, fileInfoBufferLen),
                                                                    token);

            // -- Create spans
            Span<byte> filenameBufferSpan = filenameBuffer.AsSpan(0, filenameBufferLen);
            Span<byte> fileInfoBufferSpan = fileInfoBuffer.AsSpan(0, fileInfoBufferLen);

            // -- Allocate list
            DataList = new List<Metadata>(fileInfoHeaderStruct.DataCount);

            for (int i = 0; i < fileInfoHeaderStruct.DataCount && !filenameBufferSpan.IsEmpty; i++)
            {
                ref FileInfoStruct fileInfo = ref MemoryMarshal.AsRef<FileInfoStruct>(fileInfoBufferSpan);
                filenameBufferSpan = Metadata.Parse(filenameBufferSpan,
                                                    ref fileInfo,
                                                    out Metadata? asset);
                fileInfoBufferSpan = fileInfoBufferSpan[fileInfoHeaderStruct.DataSize..];

                if (asset == null)
                {
                    throw new IndexOutOfRangeException("Failed to parse NativeData assets as the buffer data might be insufficient or out-of-bounds!");
                }
                DataList.Add(asset);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(filenameBuffer);
            ArrayPool<byte>.Shared.Return(fileInfoBuffer);
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

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct FileInfoStruct
    {
        /// <summary>
        /// 4x4 bytes Reordered hash (each 4 bytes reordered as Big-endian).
        /// </summary>
        private fixed byte _shifted4BytesMD5Checksum[16];

        /// <summary>
        /// The size of the file.
        /// </summary>
        public long FileSize;

        /// <summary>
        /// The filename length on the filename buffer.
        /// </summary>
        public int FilenameLength;

        /// <summary>
        /// The start offset of the filename inside the filename buffer.
        /// </summary>
        public int FilenameStartAt;

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

    public class Metadata : StarRailAssetGenericFileInfo
    {
        public static Span<byte> Parse(Span<byte>               filenameBuffer,
                                       ref FileInfoStruct assetInfo,
                                       out Metadata?      result)
        {
            Unsafe.SkipInit(out result);
            int filenameLength = assetInfo.FilenameLength;
            if (filenameBuffer.Length < filenameLength)
            {
                return filenameBuffer;
            }

            string filename    = Encoding.UTF8.GetString(filenameBuffer[..filenameLength]);
            long   fileSize    = assetInfo.FileSize;
            byte[] md5Checksum = new byte[16];

            assetInfo.Shifted4BytesMD5Checksum.CopyTo(md5Checksum);
            StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Checksum); // Reorder 4x4 hash using SIMD

            result = new Metadata
            {
                Filename    = filename,
                FileSize    = fileSize,
                MD5Checksum = md5Checksum
            };

            return filenameBuffer[filenameLength..];
        }
    }
}
