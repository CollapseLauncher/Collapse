using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip.Metadata
{
    internal static class ArchiveMetadataFormat
    {
        internal static readonly ImmutableArray<byte> kFileSignature = ImmutableArray.Create<byte>((byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C);
        internal const int kHeaderLength = 0x20;
    }

    internal enum ArchiveMetadataToken
    {
        #region Constants

        Unknown = -1,

        End = 0,
        Header = 1,
        ArchiveProperties = 2,
        AdditionalStreams = 3,
        MainStreams = 4,
        Files = 5,
        PackInfo = 6,
        UnpackInfo = 7,
        SubStreamsInfo = 8,
        Size = 9,
        CRC = 10,
        Folder = 11,
        CodersUnpackSize = 12,
        NumUnpackStream = 13,
        EmptyStream = 14,
        EmptyFile = 15,
        Anti = 16,
        Name = 17,
        CTime = 18,
        ATime = 19,
        MTime = 20,
        WinAttributes = 21,
        Comment = 22,
        PackedHeader = 23,
        StartPos = 24,
        Padding = 25, // called 'Dummy' in the original implementation

        #endregion
    }

    internal sealed class ArchiveSectionMetadataBuilder
    {
        public DecoderMetadataBuilder[] Decoders;
        public int RequiredRawInputStreamCount;
        public DecoderInputMetadata OutputStream;
        public long OutputLength;
        public Checksum? OutputChecksum;
        public int? SubStreamCount;
        public DecodedStreamMetadata[] Subsections;
    }

    internal sealed class DecoderMetadataBuilder
    {
        public CompressionMethod Method;
        public int InputCount;
        public int OutputCount;
        public ImmutableArray<byte> Settings;
        public ImmutableArray<DecoderInputMetadata>.Builder InputInfo;
        public ImmutableArray<DecoderOutputMetadata>.Builder OutputInfo;
    }
}
