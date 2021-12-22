using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;

namespace ManagedLzma.SevenZip.Writer
{
    // TODO: it would be even better if we could separate ArchiveWriter from a "modifyable" archive further
    //       the archive writer should just have enough state (from modifyable archives) that he can write new metadata/header

    public sealed class ArchiveWriter : IDisposable
    {
#if DEBUG
        static ArchiveWriter()
        {
            ArchivedAttributesExtensions.CheckArchivedAttributesConsistency();
        }
#endif

        private static void PutInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void PutInt64(byte[] buffer, int offset, long value)
        {
            PutInt32(buffer, offset, (int)value);
            PutInt32(buffer, offset + 4, (int)(value >> 32));
        }

        public static ArchiveWriter Create(Stream stream, bool dispose)
        {
            try
            {
                var writer = new ArchiveWriter(stream, dispose);

                stream.Position = 0;
                stream.SetLength(ArchiveMetadataFormat.kHeaderLength);
                stream.Write(writer.PrepareHeader(), 0, ArchiveMetadataFormat.kHeaderLength);

                return writer;
            }
            catch
            {
                if (dispose && stream != null)
                    stream.Dispose();

                throw;
            }
        }

        public static async Task<ArchiveWriter> CreateAsync(Stream stream, bool dispose)
        {
            try
            {
                var writer = new ArchiveWriter(stream, dispose);

                stream.Position = 0;
                stream.SetLength(ArchiveMetadataFormat.kHeaderLength);
                await stream.WriteAsync(writer.PrepareHeader(), 0, ArchiveMetadataFormat.kHeaderLength).ConfigureAwait(false);

                return writer;
            }
            catch
            {
                if (dispose && stream != null)
                    stream.Dispose();

                throw;
            }
        }

        private const byte kMajorVersion = 0;
        private const byte kMinorVersion = 3;

        private Stream mArchiveStream;
        private ImmutableArray<ArchiveFileSection>.Builder mFileSections;
        private ImmutableArray<ArchiveDecoderSection>.Builder mDecoderSections;
        private ArchiveWriterStreamProvider mStreamProvider;
        private List<EncoderSession> mEncoderSessions = new List<EncoderSession>();
        private long mAppendPosition;
        private long mMetadataPosition;
        private long mMetadataLength;
        private Checksum mMetadataChecksum;
        private bool mDisposeStream;

        private ArchiveWriter(Stream stream, bool dispose)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writeable.", nameof(stream));

            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.", nameof(stream));

            mArchiveStream = stream;
            mDisposeStream = dispose;
            mMetadataPosition = ArchiveMetadataFormat.kHeaderLength;
            mMetadataLength = 0;
            mAppendPosition = ArchiveMetadataFormat.kHeaderLength;
            mMetadataChecksum = Checksum.GetEmptyStreamChecksum();
            mFileSections = ImmutableArray.CreateBuilder<ArchiveFileSection>();
            mDecoderSections = ImmutableArray.CreateBuilder<ArchiveDecoderSection>();
        }

        public void Dispose()
        {
            if (mDisposeStream)
                mArchiveStream.Dispose();

            if (mEncoderSessions.Count > 0)
                throw new NotImplementedException();
        }

        /// <summary>
        /// Appends a new metadata section to the end of the file.
        /// </summary>
        public Task WriteMetadata(ArchiveMetadataProvider metadata)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            int metadataCount = metadata.GetCount();
            if (metadataCount < 0)
                throw new InvalidOperationException(nameof(ArchiveMetadataProvider) + " returned negative count.");

            // TODO: wait for completion of pending writes

            mMetadataPosition = mAppendPosition;
            mArchiveStream.Position = mAppendPosition;

            // TODO: we'll want to write the metadata into a stream, compress it, calculate the checksum on the fly
            // TODO: we'll probably also want to have a scratch buffer to assembly vectors with no allocation overhead and in a single iteration
            //       (a scratch buffer allows to record the vector in a single iteration and if it was unnecessary it doesn't need to be forwarded to the actual stream)

            var subStreamCount = mDecoderSections.Sum(x => x != null ? x.Streams.Length : 0);

            WriteToken(ArchiveMetadataToken.Header);

            if (mDecoderSections.Count > 0)
            {
                WriteToken(ArchiveMetadataToken.MainStreams);
                WritePackInfo();
                WriteUnpackInfo();
                WriteSubStreamsInfo();
                WriteToken(ArchiveMetadataToken.End);
            }

            if (subStreamCount > 0)
            {
                WriteToken(ArchiveMetadataToken.Files);
                WriteNumber(metadataCount);

                #region Types
                {
                    int emptyStreamCount = 0;

                    for (int i = 0; i < metadataCount; i++)
                        if (!metadata.HasStream(i))
                            emptyStreamCount++;

                    if (emptyStreamCount > 0)
                    {
                        WriteBitVectorWithHeader(ArchiveMetadataToken.EmptyStream,
                            Enumerable.Range(0, metadataCount)
                            .Select(x => !metadata.HasStream(x)),
                            metadataCount);

                        if (Enumerable.Range(0, metadataCount).Where(x => !metadata.HasStream(x)).Any(x => !metadata.IsDirectory(x)))
                            WriteBitVectorWithHeader(ArchiveMetadataToken.EmptyFile,
                                Enumerable.Range(0, metadataCount)
                                .Where(x => !metadata.HasStream(x))
                                .Select(x => !metadata.IsDirectory(x)),
                                emptyStreamCount);

                        if (Enumerable.Range(0, metadataCount).Where(x => !metadata.HasStream(x)).Any(x => metadata.IsDeleted(x)))
                            WriteBitVectorWithHeader(ArchiveMetadataToken.Anti,
                                Enumerable.Range(0, metadataCount)
                                .Where(x => !metadata.HasStream(x))
                                .Select(x => metadata.IsDeleted(x)),
                                emptyStreamCount);
                    }
                }
                #endregion

                #region Names
                {
                    bool hasNames = false;
                    int nameSize = 1;

                    for (int i = 0; i < subStreamCount; i++)
                    {
                        var name = metadata.GetName(i);
                        if (!string.IsNullOrEmpty(name))
                        {
                            hasNames = true;
                            nameSize += (name.Length + 1) * 2;
                        }
                        else
                        {
                            nameSize += 2;
                        }
                    }

                    if (hasNames)
                    {
                        WritePadding(2 + GetNumberSize(nameSize), 16);
                        WriteToken(ArchiveMetadataToken.Name);
                        WriteNumber(nameSize);
                        WriteByte(0);

                        System.Diagnostics.Debug.Assert((mArchiveStream.Position & 15) == 0);

                        for (int i = 0; i < subStreamCount; i++)
                        {
                            var name = metadata.GetName(i);
                            foreach (char ch in name)
                            {
                                WriteByte((byte)ch);
                                WriteByte((byte)(ch >> 8));
                            }
                            WriteByte(0);
                            WriteByte(0);
                        }
                    }
                }
                #endregion

                WriteDateVector(Enumerable.Range(0, metadataCount).Select(x => metadata.GetCreationDate(x)), ArchiveMetadataToken.CTime);
                WriteDateVector(Enumerable.Range(0, metadataCount).Select(x => metadata.GetLastAccessDate(x)), ArchiveMetadataToken.ATime);
                WriteDateVector(Enumerable.Range(0, metadataCount).Select(x => metadata.GetLastWriteDate(x)), ArchiveMetadataToken.MTime);
                // TODO: what does the start position mean? it doesn't seem to be what I thought it was.
                WriteUInt64Vector(Enumerable.Range(0, metadataCount).Select(x => default(ulong?)), ArchiveMetadataToken.StartPos);
                WriteUInt32Vector(Enumerable.Range(0, metadataCount).Select(x => {
                    var attr = metadata.GetAttributes(x);
                    return attr.HasValue ? (uint)attr.Value : default(uint?);
                }), ArchiveMetadataToken.WinAttributes);

                WriteToken(ArchiveMetadataToken.End);
            }

            WriteToken(ArchiveMetadataToken.End);

            mMetadataLength = mArchiveStream.Position - mMetadataPosition;

            // TODO: this is only a temporary implementation; the checksum can be calculated on the fly in the final implementation
            {
                var buffer = new byte[0x1000];
                mArchiveStream.Position = mMetadataPosition;
                var checksum = CRC.kInitCRC;
                int offset = 0;
                while (offset < mMetadataLength)
                {
                    var fetched = mArchiveStream.Read(buffer, 0, (int)Math.Min(mMetadataLength - offset, buffer.Length));
                    if (fetched <= 0 || fetched > mMetadataLength - offset)
                        throw new InternalFailureException();

                    offset += fetched;
                    checksum = CRC.Update(checksum, buffer, 0, fetched);
                }
                mMetadataChecksum = new Checksum((int)CRC.Finish(checksum));
            }

            return Utilities.CompletedTask;
        }

        #region Writer - Structured Data

        private void WriteBitVectorWithHeader(ArchiveMetadataToken token, IEnumerable<bool> bits, int count)
        {
            WriteToken(token);
            WriteNumber((count + 7) / 8);
            WriteBitVector(bits);
        }

        private void WriteBitVector(IEnumerable<bool> bits)
        {
            byte b = 0;
            byte mask = 0x80;

            foreach (bool bit in bits)
            {
                if (bit)
                    b |= mask;

                mask >>= 1;

                if (mask == 0)
                {
                    WriteByte(b);
                    mask = 0x80;
                    b = 0;
                }
            }

            if (mask != 0x80)
                WriteByte(b);
        }

        private void WriteAlignedHeaderWithBitVector(IEnumerable<bool> bits, int vectorCount, int itemCount, ArchiveMetadataToken token, int itemSize)
        {
            var vectorSize = (itemCount == vectorCount) ? 0 : (vectorCount + 7) / 8;
            var contentSize = 2 + vectorSize + itemCount * itemSize;

            // Insert padding to align the begin of the content vector at a multiple of the given item size.
            WritePadding(3 + vectorSize + GetNumberSize(contentSize), itemSize);

            WriteToken(token);
            WriteNumber(contentSize);

            if (itemCount == vectorCount)
            {
                WriteByte(1); // all items defined == true
            }
            else
            {
                WriteByte(0); // all items defined == false, followed by a bitvector for the defined items
                WriteBitVector(bits);
            }

            WriteByte(0); // content vector is inline and not packed into a separate stream

            // caller inserts content vector (itemCount * itemSize) right behind this call
        }

        private void WriteChecksumVector(IEnumerable<Checksum?> checksums)
        {
            if (checksums.Any(x => x.HasValue))
            {
                WriteToken(ArchiveMetadataToken.CRC);

                if (checksums.All(x => x.HasValue))
                {
                    WriteByte(1);
                }
                else
                {
                    WriteByte(0);
                    WriteBitVector(checksums.Select(x => x.HasValue));
                }

                foreach (var checksum in checksums)
                    if (checksum.HasValue)
                        WriteInt32(checksum.Value.Value);
            }
        }

        private void WriteDateVector(IEnumerable<DateTime?> dates, ArchiveMetadataToken token)
        {
            WriteUInt64Vector(dates.Select(x => x.HasValue ? (ulong)x.Value.ToFileTimeUtc() : default(ulong?)), token);
        }

        private void WriteUInt64Vector(IEnumerable<ulong?> vector, ArchiveMetadataToken token)
        {
            var count = vector.Count();
            var defined = vector.Count(x => x.HasValue);

            if (defined > 0)
            {
                WriteAlignedHeaderWithBitVector(vector.Select(x => x.HasValue), count, defined, token, 8);
                System.Diagnostics.Debug.Assert((mArchiveStream.Position & 7) == 0);

                foreach (var slot in vector)
                    if (slot.HasValue)
                        WriteUInt64(slot.Value);
            }
        }

        private void WriteUInt32Vector(IEnumerable<uint?> vector, ArchiveMetadataToken token)
        {
            var count = vector.Count();
            var defined = vector.Count(x => x.HasValue);

            if (defined > 0)
            {
                WriteAlignedHeaderWithBitVector(vector.Select(x => x.HasValue), count, defined, token, 4);
                System.Diagnostics.Debug.Assert((mArchiveStream.Position & 3) == 0);

                foreach (var slot in vector)
                    if (slot.HasValue)
                        WriteInt32((int)slot.Value);
            }
        }

        private void WritePackInfo()
        {
            if (mFileSections.Count > 0)
            {
#if DEBUG
                System.Diagnostics.Debug.Assert(mFileSections[0].Offset == ArchiveMetadataFormat.kHeaderLength);
                for (int i = 1; i < mFileSections.Count; i++)
                    System.Diagnostics.Debug.Assert(mFileSections[i].Offset == mFileSections[i - 1].Offset + mFileSections[i - 1].Length);
#endif

                WriteToken(ArchiveMetadataToken.PackInfo);
                WriteNumber(mFileSections[0].Offset - ArchiveMetadataFormat.kHeaderLength);
                WriteNumber(mFileSections.Count);

                WriteToken(ArchiveMetadataToken.Size);
                foreach (var fileSection in mFileSections)
                    WriteNumber(fileSection.Length);

                WriteChecksumVector(mFileSections.Select(x => x.Checksum));

                WriteToken(ArchiveMetadataToken.End);
            }
        }

        private void WriteUnpackInfo()
        {
            if (mDecoderSections.Count > 0)
            {
                WriteToken(ArchiveMetadataToken.UnpackInfo);

                WriteToken(ArchiveMetadataToken.Folder);
                WriteNumber(mDecoderSections.Count);
                WriteByte(0);

                int index = 0;
                foreach (var decoder in mDecoderSections)
                    WriteDecoderSection(decoder, ref index);

                WriteToken(ArchiveMetadataToken.CodersUnpackSize);
                foreach (var section in mDecoderSections)
                    foreach (var decoder in section.Decoders)
                        foreach (var stream in decoder.OutputStreams)
                            WriteNumber(stream.Length);

                WriteChecksumVector(mDecoderSections.Select(x => x.Checksum));

                WriteToken(ArchiveMetadataToken.End);
            }
        }

        private void WriteDecoderSection(ArchiveDecoderSection definition, ref int firstStreamIndex)
        {
            WriteNumber(definition.Decoders.Length);

            var inputOffset = new int[definition.Decoders.Length];
            var outputOffset = new int[definition.Decoders.Length];

            for (int i = 1; i < definition.Decoders.Length; i++)
            {
                inputOffset[i] = inputOffset[i - 1] + definition.Decoders[i - 1].InputStreams.Length;
                outputOffset[i] = outputOffset[i - 1] + definition.Decoders[i - 1].OutputStreams.Length;
            }

            for (int i = 0; i < definition.Decoders.Length; i++)
            {
                var decoder = definition.Decoders[i];
                var id = decoder.DecoderType.Encode();
                var multiStream = decoder.InputStreams.Length != 1 || decoder.OutputStreams.Length != 1;

                var settings = decoder.Settings;
                var hasSettings = !settings.IsDefaultOrEmpty;

                System.Diagnostics.Debug.Assert(!id.IsDefaultOrEmpty && id.Length <= 15);
                var flags = (byte)id.Length;
                if (multiStream) flags |= 0x10;
                if (hasSettings) flags |= 0x20;

                WriteByte(flags);
                foreach (var bt in id)
                    WriteByte(bt);

                if (multiStream)
                {
                    WriteNumber(decoder.InputStreams.Length);
                    WriteNumber(decoder.OutputStreams.Length);
                }

                if (hasSettings)
                {
                    WriteNumber(settings.Length);
                    foreach (var bt in settings)
                        WriteByte(bt);
                }
            }

            for (int i = 0; i < definition.Decoders.Length; i++)
            {
                var decoder = definition.Decoders[i];

                for (int j = 0; j < decoder.InputStreams.Length; j++)
                {
                    var input = decoder.InputStreams[j];

                    if (input.DecoderIndex.HasValue)
                    {
                        WriteNumber(inputOffset[i] + j);
                        WriteNumber(outputOffset[input.DecoderIndex.Value] + input.StreamIndex);
                    }
                }
            }

            int fileStreamSections = 0;
            foreach (var decoder in definition.Decoders)
                foreach (var input in decoder.InputStreams)
                    if (!input.DecoderIndex.HasValue)
                        fileStreamSections += 1;

            if (fileStreamSections > 1)
                foreach (var decoder in definition.Decoders)
                    foreach (var input in decoder.InputStreams)
                        if (!input.DecoderIndex.HasValue)
                            WriteNumber(input.StreamIndex - firstStreamIndex);

            firstStreamIndex += fileStreamSections;
        }

        private void WriteSubStreamsInfo()
        {
            WriteToken(ArchiveMetadataToken.SubStreamsInfo);

            if (mDecoderSections.Any(x => x.Streams.Length != 1))
            {
                WriteToken(ArchiveMetadataToken.NumUnpackStream);
                foreach (var decoderSection in mDecoderSections)
                    WriteNumber(decoderSection.Streams.Length);
            }

            if (mDecoderSections.Any(x => x.Streams.Length > 1))
            {
                WriteToken(ArchiveMetadataToken.Size);
                foreach (var decoderSection in mDecoderSections)
                {
                    var decodedStreams = decoderSection.Streams;
                    for (int i = 0; i < decodedStreams.Length - 1; i++)
                        WriteNumber(decodedStreams[i].Length);
                }
            }

            WriteChecksumVector(
                from decoderSection in mDecoderSections
                where !(decoderSection.Streams.Length == 1 && decoderSection.Checksum.HasValue)
                from decodedStream in decoderSection.Streams
                select decodedStream.Checksum);

            WriteToken(ArchiveMetadataToken.End);
        }

        #endregion

        #region Writer - Raw Data

        private void WriteToken(ArchiveMetadataToken token)
        {
            System.Diagnostics.Debug.Assert(0 <= (int)token && (int)token <= 25);
            WriteByte((byte)token);
        }

        private void WritePadding(int offset, int alignment)
        {
            // 7-Zip 4.50 - 4.58 contain BUG, so they do not support .7z archives with Unknown field.

            offset = (int)(mArchiveStream.Position + offset) & (alignment - 1);

            if (offset > 0)
            {
                var padding = alignment - offset;

                if (padding < 2)
                    padding += alignment;

                padding -= 2;

                WriteToken(ArchiveMetadataToken.Padding);
                WriteByte((byte)padding);

                for (int i = 0; i < padding; i++)
                    WriteByte(0);
            }
        }

        private void WriteByte(byte value)
        {
            mArchiveStream.WriteByte(value);
        }

        private void WriteInt32(int value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
            WriteByte((byte)(value >> 16));
            WriteByte((byte)(value >> 24));
        }

        private void WriteUInt64(ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                WriteByte((byte)value);
                value >>= 8;
            }
        }

        private void WriteNumber(long value)
        {
            System.Diagnostics.Debug.Assert(value >= 0);

            byte header = 0;
            byte mask = 0x80;
            byte count = 0;

            while (count < 8)
            {
                if (value < (1L << (7 * (count + 1))))
                {
                    header |= (byte)(value >> (8 * count));
                    break;
                }

                header |= mask;
                mask >>= 1;
                count += 1;
            }

            WriteByte(header);

            while (count > 0)
            {
                WriteByte((byte)value);
                value >>= 8;
                count -= 1;
            }
        }

        private int GetNumberSize(long value)
        {
            System.Diagnostics.Debug.Assert(value >= 0);

            int length = 1;

            while (length < 9 && value >= (1L << (length * 7)))
                length++;

            return length;
        }

        #endregion

        /// <summary>
        /// Updates the file header to refer to the last written metadata section.
        /// </summary>
        public async Task WriteHeader()
        {
            // TODO: wait for completion of pending metadata write (if there is any)
            // TODO: discard data from sessions which were started after writing metadata

            var header = PrepareHeader();
            mArchiveStream.Position = 0;
            await mArchiveStream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
        }

        public EncoderSession BeginEncoding(EncoderDefinition definition, bool calculateChecksums)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            var storage = new Stream[definition.StorageCount];
            for (int i = 0; i < storage.Length; i++)
                storage[i] = mStreamProvider?.CreateBufferStream() ?? new MemoryStream();

            var session = definition.CreateEncoderSession(this, mDecoderSections.Count, storage, calculateChecksums);
            mDecoderSections.Add(null);
            mEncoderSessions.Add(session);
            return session;
        }

        /// <summary>
        /// Allows to copy complete sections from existing archives into this archive.
        /// </summary>
        public async Task TransferSectionAsync(Stream stream, ArchiveMetadata metadata, int section)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (section < 0 || section >= metadata.DecoderSections.Length)
                throw new ArgumentOutOfRangeException(nameof(section));

            var decoderSection = metadata.DecoderSections[section];
            var count = decoderSection.Streams.Length;
            if (count == 0)
                throw new InvalidOperationException();

            // TODO: wait for pending writes
            // TODO: translate and append decoder section

            foreach (var decoder in decoderSection.Decoders)
            {
                foreach (var input in decoder.InputStreams)
                {
                    if (!input.DecoderIndex.HasValue)
                    {
                        var fileSection = metadata.FileSections[input.StreamIndex];
                        var offset = mAppendPosition;
                        var length = fileSection.Length;
                        mAppendPosition = checked(offset + length);
                        mFileSections.Add(new ArchiveFileSection(offset, length, fileSection.Checksum));
                        using (var fileSectionStream = new ConstrainedReadStream(mArchiveStream, fileSection.Offset, fileSection.Length))
                            await fileSectionStream.CopyToAsync(mArchiveStream).ConfigureAwait(false);
                    }
                }
            }
        }

#if DEBUG
        // This method is currently not implemented so we don't include it in the nuget release build.

        /// <summary>
        /// Allows to copy partial sections from an existing archive, reencoding selected entries on the fly.
        /// </summary>
        public Task TranscodeSectionAsync(Stream stream, ArchiveMetadata metadata, int section, Func<int, Task<bool>> selector, EncoderDefinition definition)
        {
            throw new NotImplementedException();
        }
#endif

        #region Internal Methods - Encoder Session

        private byte[] PrepareHeader()
        {
            var metadataOffset = mMetadataPosition - ArchiveMetadataFormat.kHeaderLength;
            if (metadataOffset < 0)
                throw new InternalFailureException();

            uint crc = CRC.kInitCRC;
            crc = CRC.Update(crc, metadataOffset);
            crc = CRC.Update(crc, mMetadataLength);
            crc = CRC.Update(crc, mMetadataChecksum.Value);
            crc = CRC.Finish(crc);

            var buffer = new byte[ArchiveMetadataFormat.kHeaderLength];

            var signature = ArchiveMetadataFormat.kFileSignature;
            for (int i = 0; i < signature.Length; i++)
                buffer[i] = signature[i];

            buffer[6] = kMajorVersion;
            buffer[7] = kMinorVersion;
            PutInt32(buffer, 8, (int)crc);
            PutInt64(buffer, 12, metadataOffset);
            PutInt64(buffer, 20, mMetadataLength);
            PutInt32(buffer, 28, mMetadataChecksum.Value);

            return buffer;
        }

        internal void CompleteEncoderSession(EncoderSession session, int section, ArchiveDecoderSection definition, EncoderStorage[] storageList)
        {
            if (!mEncoderSessions.Remove(session))
                throw new InternalFailureException();

            System.Diagnostics.Debug.Assert(mDecoderSections[section] == null);
            mDecoderSections[section] = definition;

            // TODO: we can write storage lazily (just remember the streams in a list) and don't have to block the caller

            foreach (var storage in storageList)
            {
                var stream = storage.GetFinalStream();
                var offset = mAppendPosition;
                var length = stream.Length;
                mAppendPosition = checked(offset + length);
                var checksum = storage.GetFinalChecksum();
                mFileSections.Add(new ArchiveFileSection(offset, length, checksum));
                mArchiveStream.Position = offset;
                stream.Position = 0;
                stream.CopyTo(mArchiveStream);
            }
        }

        #endregion
    }

    public abstract class ArchiveWriterStreamProvider
    {
        public abstract Stream CreateBufferStream();
    }

    public abstract class ArchiveMetadataProvider
    {
        public abstract int GetCount();
        public abstract string GetName(int index);
        public abstract bool HasStream(int index);
        public abstract bool IsDirectory(int index);
        public abstract bool IsDeleted(int index);
        public abstract long GetLength(int index);
        public abstract Checksum? GetChecksum(int index);
        public abstract ArchivedAttributes? GetAttributes(int index);
        public abstract DateTime? GetCreationDate(int index);
        public abstract DateTime? GetLastWriteDate(int index);
        public abstract DateTime? GetLastAccessDate(int index);
    }

    public sealed class ArchiveMetadataRecorder : ArchiveMetadataProvider
    {
        private struct Entry
        {
            public string Name;
            public bool HasStream;
            public bool IsDirectory;
            public bool IsDeleted;
            public long Length;
            public Checksum? Checksum;
            public ArchivedAttributes? Attributes;
            public DateTime? CreationDate;
            public DateTime? LastWriteDate;
            public DateTime? LastAccessDate;
        }

        private DateTimeKind mUnspecifiedDateTimeKind = DateTimeKind.Unspecified;
        private List<Entry> mEntries = new List<Entry>();

        public ArchiveMetadataRecorder() { }

        public ArchiveMetadataRecorder(DateTimeKind unspecifiedDateTimeKind)
        {
            switch (unspecifiedDateTimeKind)
            {
                case DateTimeKind.Unspecified:
                case DateTimeKind.Utc:
                case DateTimeKind.Local:
                    mUnspecifiedDateTimeKind = unspecifiedDateTimeKind;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(unspecifiedDateTimeKind));
            }
        }

        private void CheckName(ref string name)
        {
            if (String.IsNullOrEmpty(name))
                throw new InvalidOperationException("Name cannot be empty.");

            name = name.Replace('\\', '/');

            Utilities.NeedsReview(); // TODO: we need to hardcode this so you can't write invalid filenames on non-windows platforms

            var invalid = Path.GetInvalidFileNameChars();
            var offset = 0;
            for (;;)
            {
                var ending = name.IndexOf('/', offset);
                var part = ending < 0 ? name.Substring(offset) : name.Substring(offset, ending - offset);

                if (String.IsNullOrEmpty(part))
                    throw new InvalidOperationException("Name contains empty path component.");

                if (part == ".")
                    throw new InvalidOperationException("Relative path component '.' is not allowed.");

                if (part == "..")
                    throw new InvalidOperationException("Relative path component '..' is not allowed.");

                if (part.IndexOfAny(invalid) >= 0)
                    throw new InvalidOperationException("Name contains invalid characters.");

                if (ending < 0)
                    break;

                offset = ending + 1;
            }
        }

        private void CheckAttributes(ref ArchivedAttributes? attr, bool isFile)
        {
            if (attr.HasValue)
            {
                if (isFile)
                {
                    if ((attr.Value & ArchivedAttributesExtensions.DirectoryAttribute) != 0)
                        throw new InvalidOperationException("Directory attribute cannot be set on a file.");
                }
                else
                {
                    // Automatically add the directory attribute for directories.
                    attr = attr.Value | ArchivedAttributesExtensions.DirectoryAttribute;
                }

                if ((attr & ArchivedAttributesExtensions.InvalidAttributes) != 0)
                    throw new InvalidOperationException("Invalid attributes have been set.");

                if ((attr & ArchivedAttributesExtensions.ForbiddenAttributes) != 0)
                    throw new InvalidOperationException("Some attributes are set which should not be present in a 7z archive.");

                attr = attr.Value & ~ArchivedAttributesExtensions.StrippedAttributes;
            }
        }

        private void CheckDate(ref DateTime? date)
        {
            if (date.HasValue)
            {
                var kind = date.Value.Kind;

                if (kind == DateTimeKind.Unspecified)
                {
                    if (mUnspecifiedDateTimeKind == DateTimeKind.Unspecified)
                        throw new InvalidOperationException("You did not specify how to treat DateTime values which do not provide their own DateTimeKind.");

                    kind = mUnspecifiedDateTimeKind;
                    date = new DateTime(date.Value.Ticks, kind);
                }

                if (kind == DateTimeKind.Local)
                    date = date.Value.ToUniversalTime();

                System.Diagnostics.Debug.Assert(date.Value.Kind == DateTimeKind.Utc);
            }
        }

#if !(BUILD_PORTABLE && NET_45)
        public void AppendFile(string name, long length, Checksum? checksum, FileAttributes? attributes, DateTime? creationDate, DateTime? lastWriteDate, DateTime? lastAccessDate)
        {
            var translatedAttributes = default(ArchivedAttributes?);
            if (attributes.HasValue)
                translatedAttributes = (ArchivedAttributes)(int)attributes.Value;

            AppendFile(name, length, checksum, translatedAttributes, creationDate, lastWriteDate, lastAccessDate);
        }
#endif

        public void AppendFile(string name, long length, Checksum? checksum, ArchivedAttributes? attributes, DateTime? creationDate, DateTime? lastWriteDate, DateTime? lastAccessDate)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            CheckName(ref name);
            CheckAttributes(ref attributes, true);
            CheckDate(ref creationDate);
            CheckDate(ref lastWriteDate);
            CheckDate(ref lastAccessDate);

            // TODO: replicate the checks when the metadata is queried from the provider (in particular don't forget to check that timestamps are UTC)

            mEntries.Add(new Entry {
                Name = name,
                HasStream = true,
                IsDirectory = false,
                IsDeleted = false,
                Length = length,
                Checksum = checksum,
                Attributes = attributes,
                CreationDate = creationDate,
                LastWriteDate = lastWriteDate,
                LastAccessDate = lastAccessDate,
            });
        }

#if !(BUILD_PORTABLE && NET_45)
        public void AppendDirectory(string name, FileAttributes? attributes, DateTime? creationDate, DateTime? lastWriteDate, DateTime? lastAccessDate)
        {
            var translatedAttributes = default(ArchivedAttributes?);
            if (attributes.HasValue)
                translatedAttributes = (ArchivedAttributes)(int)attributes.Value;

            AppendDirectory(name, translatedAttributes, creationDate, lastWriteDate, lastAccessDate);
        }
#endif

        public void AppendDirectory(string name, ArchivedAttributes? attributes, DateTime? creationDate, DateTime? lastWriteDate, DateTime? lastAccessDate)
        {
            CheckName(ref name);
            CheckAttributes(ref attributes, false);
            CheckDate(ref creationDate);
            CheckDate(ref lastWriteDate);
            CheckDate(ref lastAccessDate);

            // TODO: check attributes and reject invalid ones (replicate the check when writing the attributes so other metadata providers get the check too)

            Utilities.NeedsReview(); // TODO: which dates are recorded for a directory?

            mEntries.Add(new Entry {
                Name = name,
                IsDirectory = true,
                Attributes = attributes,
            });
        }

        public void AppendFileDeletion(string name)
        {
            CheckName(ref name);

            mEntries.Add(new Entry {
                Name = name,
                IsDeleted = true,
            });
        }

        public void AppendDirectoryDeletion(string name)
        {
            CheckName(ref name);

            mEntries.Add(new Entry {
                Name = name,
                IsDirectory = true,
                IsDeleted = true,
            });
        }

        public override int GetCount() => mEntries.Count;
        public override string GetName(int index) => mEntries[index].Name;
        public override bool HasStream(int index) => mEntries[index].HasStream;
        public override bool IsDirectory(int index) => mEntries[index].IsDirectory;
        public override bool IsDeleted(int index) => mEntries[index].IsDeleted;
        public override long GetLength(int index) => mEntries[index].Length;
        public override Checksum? GetChecksum(int index) => mEntries[index].Checksum;
        public override ArchivedAttributes? GetAttributes(int index) => mEntries[index].Attributes;
        public override DateTime? GetCreationDate(int index) => mEntries[index].CreationDate;
        public override DateTime? GetLastWriteDate(int index) => mEntries[index].LastWriteDate;
        public override DateTime? GetLastAccessDate(int index) => mEntries[index].LastAccessDate;
    }
}
