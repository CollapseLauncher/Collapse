using Hi3Helper.EncTool;
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
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct.Assets;

internal class StarRailBinaryDataNative : StarRailBinaryData<StarRailAssetNative>
{
    protected override ReadOnlySpan<byte> MagicSignature => "SRBM"u8;

    protected override async ValueTask<long> ReadDataCoreAsync(long currentOffset, Stream dataStream, CancellationToken token)
    {
        (HeaderStruct header, int readHeader) = await dataStream
                                                     .ReadDataAssertAndSeekAsync<HeaderStruct>(x => x.AssetInfoStructSize, token)
                                                     .ConfigureAwait(false);
        currentOffset += readHeader;

        Debug.Assert(header.FilenameBufferStartOffset == currentOffset); // ASSERT: Make sure the current data stream offset is at the exact filename buffer offset.
        Debug.Assert(header.AssetCount is < ushort.MaxValue and > 0);    // ASSERT: Make sure the data isn't more than ushort.MaxValue and not empty.

        // Read file info
        int fileInfoBufferLen = header.AssetInfoStructSize * header.AssetCount;
        byte[] filenameBuffer = ArrayPool<byte>.Shared.Rent(header.FilenameBufferSize);
        byte[] fileInfoBuffer = ArrayPool<byte>.Shared.Rent(fileInfoBufferLen);
        try
        {

            // Read filename buffer
            int read = await dataStream.ReadAtLeastAsync(filenameBuffer.AsMemory(0, header.FilenameBufferSize),
                                                         header.FilenameBufferSize,
                                                         cancellationToken: token)
                                       .ConfigureAwait(false);
            Debug.Assert(header.FilenameBufferSize == read); // ASSERT: Make sure the filename buffer size is equal as what we read.

            currentOffset += read;
            int paddingOffsetToInfoBuffer = header.AssetInfoAbsoluteStartOffset - (int)currentOffset;
            await dataStream.SeekForwardAsync(paddingOffsetToInfoBuffer, token); // ASSERT: Seek forward to the asset info buffer.

            // Read file info buffer
            read = await dataStream.ReadAtLeastAsync(fileInfoBuffer.AsMemory(0, fileInfoBufferLen),
                                                     fileInfoBufferLen,
                                                     cancellationToken: token);
            Debug.Assert(fileInfoBufferLen == read); // ASSERT: Make sure the asset info struct size is equal as what we read.
            currentOffset += read;

            // Create spans
            Span<byte> filenameBufferSpan = filenameBuffer.AsSpan(0, header.FilenameBufferSize);
            Span<byte> fileInfoBufferSpan = fileInfoBuffer.AsSpan(0, fileInfoBufferLen);

            // Allocate list
            DataList = new List<StarRailAssetNative>(header.AssetCount);

            for (int i = 0; i < header.AssetCount && !filenameBufferSpan.IsEmpty; i++)
            {
                ref StarRailAssetNative.StarRailAssetNativeInfo fileInfo =
                    ref MemoryMarshal.AsRef<StarRailAssetNative.StarRailAssetNativeInfo>(fileInfoBufferSpan);
                filenameBufferSpan = StarRailAssetNative.Parse(filenameBufferSpan,
                                                               ref fileInfo,
                                                               out StarRailAssetNative? asset);
                fileInfoBufferSpan = fileInfoBufferSpan[header.AssetInfoStructSize..];

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
    private struct HeaderStruct
    {
        public int Reserved;
        /// <summary>
        /// The absolute offset of <see cref="StarRailAssetNative.StarRailAssetNativeInfo"/> struct array in the data stream.
        /// </summary>
        public int AssetInfoAbsoluteStartOffset;
        /// <summary>
        /// The count of the <see cref="StarRailAssetNative.StarRailAssetNativeInfo"/> struct array.
        /// </summary>
        public int AssetCount;
        /// <summary>
        /// The size of the <see cref="StarRailAssetNative.StarRailAssetNativeInfo"/> struct.
        /// </summary>
        public int AssetInfoStructSize;
        public int Unknown1;
        /// <summary>
        /// The absolute offset of filename buffer in the data stream.
        /// </summary>
        public int FilenameBufferStartOffset;
        public int Unknown2;
        /// <summary>
        /// The size of the filename buffer in bytes.
        /// </summary>
        public int FilenameBufferSize;
    }
}

internal class StarRailAssetNative
{
    public required string Filename    { get; init; }
    public required long   Size        { get; init; }
    public required byte[] MD5Checksum { get; init; }

    public static Span<byte> Parse(Span<byte>                  filenameBuffer,
                                   ref StarRailAssetNativeInfo assetInfo,
                                   out StarRailAssetNative?    result)
    {
        Unsafe.SkipInit(out result);
        int filenameLength = assetInfo.FilenameLength;
        if (filenameBuffer.Length < filenameLength)
        {
            return filenameBuffer;
        }

        string filename    = Encoding.UTF8.GetString(filenameBuffer[..filenameLength]);
        long   fileSize    = assetInfo.Size;
        byte[] md5Checksum = new byte[16];

        assetInfo.Shifted4BytesMD5Checksum.CopyTo(md5Checksum);
        StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Checksum); // Reorder 4x4 hash using SIMD

        result = new StarRailAssetNative
        {
            Filename    = filename,
            Size        = fileSize,
            MD5Checksum = md5Checksum
        };

        return filenameBuffer[filenameLength..];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public unsafe struct StarRailAssetNativeInfo
    {
        private fixed byte _shifted4BytesMD5Checksum[16];
        public        long Size;
        public        int  FilenameLength;
        public        int  FilenameStartAt;

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
}