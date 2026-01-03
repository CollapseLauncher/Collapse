using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CommentTypo

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher.RepairManagement.StarRail.Struct;

/// <summary>
/// Star Rail Metadata Index (SRMI) data parser. This parser also implements <see cref="StarRailBinaryDataWritable{TAsset}"/> to write back the result to a <see cref="Stream"/>.<br/>
/// This implementation inherit these subtypes:<br/>
/// </summary>
internal class StarRailAssetMetadataIndex : StarRailBinaryDataWritable<StarRailAssetMetadataIndex.MetadataIndex>
{
    public StarRailAssetMetadataIndex() : this(false, false)
    {
    }

    public StarRailAssetMetadataIndex(bool use6BytesPadding = false, bool useHeaderSizeOfForAssert = false)
        : base(MagicSignatureStatic,
               768,
               256,
               0, // On SRMI, the header length is actually the entire size of the data inside the stream (including header).
               // The size will be recalculated if something changed.

               0, // On SRMI, the value is always be 0.
               12) // On SRMI, the subStruct header length is 12 bytes (compared to SRBM's 16 bytes)
    {
        Use6BytesPaddingMode     = use6BytesPadding;
        UseHeaderSizeOfForAssert = useHeaderSizeOfForAssert;
    }

    private static     ReadOnlySpan<byte> MagicSignatureStatic => "SRMI"u8;
    protected override ReadOnlySpan<byte> MagicSignature       => MagicSignatureStatic;
    
    private bool Use6BytesPaddingMode     { get; }
    private bool UseHeaderSizeOfForAssert { get; }

    /// <summary>
    /// Reads the header and perform assertion on the header.
    /// </summary>
    /// <param name="dataStream">The <see cref="Stream"/> which provides the source of the data to be parsed.</param>
    /// <param name="token">Cancellation token for cancelling asynchronous operations.</param>
    /// <returns>
    /// This returns the <see cref="StarRailBinaryDataHeaderStruct"/> and the current offset/position of the data stream after reading the header.
    /// </returns>
    protected override async ValueTask<(StarRailBinaryDataHeaderStruct Header, int Offset)> ReadHeaderCoreAsync(
        Stream            dataStream,
        CancellationToken token = default)
    {
        return await dataStream
           .ReadDataAssertAndSeekAsync<StarRailBinaryDataHeaderStruct>(x => UseHeaderSizeOfForAssert
                                                                           ? Marshal.SizeOf<StarRailBinaryDataHeaderStruct>()
                                                                           : x.HeaderOrDataLength,
                                                                       token);
    }

    protected override async ValueTask<long> ReadDataCoreAsync(
        long              currentOffset,
        Stream            dataStream,
        CancellationToken token = default)
    {
        if (Use6BytesPaddingMode)
        {
            (MetadataIndex6BytesPadStruct indexData6BytesPad, int readHeader6BytesPad) =
                await dataStream
                     .ReadDataAssertAndSeekAsync<MetadataIndex6BytesPadStruct>(_ => Unsafe.SizeOf<MetadataIndex6BytesPadStruct>(), token)
                     .ConfigureAwait(false);
            currentOffset += readHeader6BytesPad;

            MetadataIndex.Parse(in indexData6BytesPad, out MetadataIndex metadataIndex6Bytes);
            DataList = [metadataIndex6Bytes];

            return currentOffset;
        }

        (MetadataIndexStruct indexData, int readHeader) =
            await dataStream
                 .ReadDataAssertAndSeekAsync<MetadataIndexStruct>(_ => Unsafe.SizeOf<MetadataIndexStruct>(), token)
                 .ConfigureAwait(false);
        currentOffset += readHeader;

        MetadataIndex.Parse(in indexData, out MetadataIndex metadataIndex);
        DataList = [metadataIndex];

        return currentOffset;
    }

    protected override async ValueTask WriteHeaderCoreAsync(Stream dataStream, CancellationToken token = default)
    {
        int sizeOfHeader = Marshal.SizeOf<StarRailBinaryDataHeaderStruct>();
        int sizeOfData   = DataList.Count * Marshal.SizeOf<MetadataIndexStruct>();

        StarRailBinaryDataHeaderStruct header = Header;        // Copy header
        header.HeaderOrDataLength = sizeOfHeader + sizeOfData; // Set data length

        await dataStream.WriteAsync(header, token).ConfigureAwait(false);
    }

    protected override async ValueTask WriteDataCoreAsync(Stream dataStream, CancellationToken token = default)
    {
        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (DataList.Count == 0)
        {
            throw new InvalidOperationException("Data is not initialized!");
        }

        if (DataList.Count > 1)
        {
            throw new InvalidOperationException("This struct doesn't accept multiple data!");
        }

        ref readonly MetadataIndex dataRef = ref CollectionsMarshal.AsSpan(DataList)[0];

        if (Use6BytesPaddingMode)
        {
            dataRef.ToStruct6BytesPad(out MetadataIndex6BytesPadStruct indexStruct6BytesPad);
            await dataStream.WriteAsync(indexStruct6BytesPad, token).ConfigureAwait(false);
            return;
        }

        dataRef.ToStruct(out MetadataIndexStruct indexStruct);
        await dataStream.WriteAsync(indexStruct, token).ConfigureAwait(false);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public unsafe struct MetadataIndexStruct
    {
        public        int  MajorVersion;
        public        int  MinorVersion;
        public        int  PatchVersion;
        private fixed byte _shifted4BytesMD5Checksum[16];
        public        int  MetadataIndexFileSize;
        public        int  PrevPatch;
        public        int  UnixTimestamp;
        private fixed byte _reserved[10];

        public Span<byte> Shifted4BytesMD5Checksum
        {
            get
            {
                fixed (byte* magicP = _shifted4BytesMD5Checksum)
                {
                    return new Span<byte>(magicP, 16);
                }
            }
        }

        public Span<byte> Reserved
        {
            get
            {
                fixed (byte* reservedP = _reserved)
                {
                    return new Span<byte>(reservedP, 10);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 2)]
    public unsafe struct MetadataIndex6BytesPadStruct
    {
        public        int  MajorVersion;
        public        int  MinorVersion;
        public        int  PatchVersion;
        private fixed byte _shifted4BytesMD5Checksum[16];
        public        int  MetadataIndexFileSize;
        public        int  PrevPatch;
        public        int  UnixTimestamp;
        private fixed byte _reserved[6];

        public Span<byte> Shifted4BytesMD5Checksum
        {
            get
            {
                fixed (byte* magicP = _shifted4BytesMD5Checksum)
                {
                    return new Span<byte>(magicP, 16);
                }
            }
        }

        public Span<byte> Reserved
        {
            get
            {
                fixed (byte* reservedP = _reserved)
                {
                    return new Span<byte>(reservedP, 6);
                }
            }
        }
    }

    public class MetadataIndex
    {
        public required int            MajorVersion          { get; init; }
        public required int            MinorVersion          { get; init; }
        public required int            PatchVersion          { get; init; }
        public required byte[]         MD5Checksum           { get; init; }
        public required int            MetadataIndexFileSize { get; init; }
        public required int            PrevPatch             { get; init; }
        public required DateTimeOffset Timestamp             { get; init; }
        public required byte[]         Reserved              { get; init; }

        public static void Parse(in  MetadataIndexStruct indexStruct,
                                 out MetadataIndex       result)
        {
            byte[] md5Buffer      = new byte[16];
            byte[] reservedBuffer = new byte[10];

            indexStruct.Shifted4BytesMD5Checksum.CopyTo(md5Buffer);
            indexStruct.Reserved.CopyTo(reservedBuffer);

            StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Buffer);
            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(indexStruct.UnixTimestamp);

            result = new MetadataIndex
            {
                MajorVersion          = indexStruct.MajorVersion,
                MinorVersion          = indexStruct.MinorVersion,
                PatchVersion          = indexStruct.PatchVersion,
                MD5Checksum           = md5Buffer,
                MetadataIndexFileSize = indexStruct.MetadataIndexFileSize,
                PrevPatch             = indexStruct.PrevPatch,
                Timestamp             = timestamp,
                Reserved              = reservedBuffer
            };
        }

        public static void Parse(in  MetadataIndex6BytesPadStruct indexStruct,
                                 out MetadataIndex                result)
        {
            byte[] md5Buffer      = new byte[16];
            byte[] reservedBuffer = new byte[6];

            indexStruct.Shifted4BytesMD5Checksum.CopyTo(md5Buffer);
            indexStruct.Reserved.CopyTo(reservedBuffer);

            StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Buffer);
            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(indexStruct.UnixTimestamp);

            result = new MetadataIndex
            {
                MajorVersion          = indexStruct.MajorVersion,
                MinorVersion          = indexStruct.MinorVersion,
                PatchVersion          = indexStruct.PatchVersion,
                MD5Checksum           = md5Buffer,
                MetadataIndexFileSize = indexStruct.MetadataIndexFileSize,
                PrevPatch             = indexStruct.PrevPatch,
                Timestamp             = timestamp,
                Reserved              = reservedBuffer
            };
        }

        public void ToStruct(out MetadataIndexStruct indexStruct)
        {
            indexStruct = new MetadataIndexStruct
            {
                MetadataIndexFileSize = MetadataIndexFileSize,
                MajorVersion          = MajorVersion,
                MinorVersion          = MinorVersion,
                PatchVersion          = PatchVersion,
                PrevPatch             = PrevPatch,
                UnixTimestamp         = (int)Timestamp.ToUnixTimeSeconds()
            };

            Span<byte> reservedSpan = indexStruct.Reserved;
            Span<byte> md5Span      = indexStruct.Shifted4BytesMD5Checksum;

            Reserved.CopyTo(reservedSpan);
            MD5Checksum.CopyTo(md5Span);

            StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Span);
        }

        public void ToStruct6BytesPad(out MetadataIndex6BytesPadStruct indexStruct)
        {
            indexStruct = new MetadataIndex6BytesPadStruct
            {
                MetadataIndexFileSize = MetadataIndexFileSize,
                MajorVersion          = MajorVersion,
                MinorVersion          = MinorVersion,
                PatchVersion          = PatchVersion,
                PrevPatch             = PrevPatch,
                UnixTimestamp         = (int)Timestamp.ToUnixTimeSeconds()
            };

            Span<byte> reservedSpan = indexStruct.Reserved;
            Span<byte> md5Span      = indexStruct.Shifted4BytesMD5Checksum;

            Reserved.CopyTo(reservedSpan);
            MD5Checksum.CopyTo(md5Span);

            StarRailBinaryDataExtension.ReverseReorderBy4X4HashData(md5Span);
        }
    }
}