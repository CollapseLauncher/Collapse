using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// These are various reader classes which are passed to to subclasses of the metadata reader.
// This design allows the subclass to decide how to store the data, or whether to read it at all.
// In case the subclass does not want to read the data these readers will skip over it without allocating anything.

namespace ManagedLzma.SevenZip.Reader
{
    public sealed class MetadataStringReader
    {
        private ArchiveMetadataReader mReader;
        private int mCount;
        private int mIndex;

        internal MetadataStringReader(ArchiveMetadataReader reader, int count)
        {
            mReader = reader;
            mCount = count;
        }

        internal void Complete()
        {
            while (mIndex < mCount)
                ReadString();

            mReader = null;
        }

        public int Count => mCount;
        public int Index => mIndex;

        public string ReadString()
        {
            if (mReader == null)
                throw new ObjectDisposedException(null);

            if (mIndex == mCount)
                throw new InvalidOperationException();

            var text = mReader.ReadStringInternal();
            mIndex += 1;
            return text;
        }
    }

    public sealed class MetadataDateReader
    {
        private ArchiveMetadataReader mReader;
        private BitVector mVector;
        private int mCount;
        private int mIndex;

        internal MetadataDateReader(ArchiveMetadataReader reader, int count, BitVector defined)
        {
            mReader = reader;
            mVector = defined;
            mCount = count;
        }

        internal void Complete()
        {
            while (mIndex < mCount)
                ReadDate();

            mReader = null;
        }

        public int Count => mCount;
        public int Index => mIndex;

        public DateTime? ReadDate()
        {
            if (mReader == null)
                throw new ObjectDisposedException(null);

            if (mIndex == mCount)
                throw new InvalidOperationException();

            if (mVector[mIndex])
            {
                // FILETIME = 100-nanosecond intervals since January 1, 1601 (UTC)
                var date = mReader.ReadInt64Internal();
                mIndex += 1;
                return DateTime.FromFileTimeUtc(date);
            }
            else
            {
                mIndex += 1;
                return null;
            }
        }
    }

    public sealed class MetadataNumberReader
    {
        private ArchiveMetadataReader mReader;
        private BitVector mVector;
        private int mCount;
        private int mIndex;

        internal MetadataNumberReader(ArchiveMetadataReader reader, int count, BitVector defined)
        {
            mReader = reader;
            mVector = defined;
            mCount = count;
        }

        internal void Complete()
        {
            while (mIndex < mCount)
                ReadNumber();

            mReader = null;
        }

        public int Count => mCount;
        public int Index => mIndex;

        public long? ReadNumber()
        {
            if (mReader == null)
                throw new ObjectDisposedException(null);

            if (mIndex == mCount)
                throw new InvalidOperationException();

            if (mVector[mIndex])
            {
                var number = mReader.ReadInt64Internal();
                mIndex += 1;
                return number;
            }
            else
            {
                mIndex += 1;
                return null;
            }
        }
    }

    public sealed class MetadataAttributeReader
    {
        private ArchiveMetadataReader mReader;
        private BitVector mVector;
        private int mCount;
        private int mIndex;
        private bool mEnablePosixFileAttributeExtension;

        internal MetadataAttributeReader(ArchiveMetadataReader reader, int count, BitVector defined, bool enablePosixFileAttributeExtension)
        {
            mReader = reader;
            mVector = defined;
            mCount = count;
            mEnablePosixFileAttributeExtension = enablePosixFileAttributeExtension;
        }

        internal void Complete()
        {
            while (mIndex < mCount)
                ReadAttributes();

            mReader = null;
        }

        public int Count => mCount;
        public int Index => mIndex;

        public ArchivedAttributes? ReadAttributes()
        {
            if (mReader == null)
                throw new ObjectDisposedException(null);

            if (mIndex == mCount)
                throw new InvalidOperationException();

            if (mVector[mIndex])
            {
                var attr = (ArchivedAttributes)mReader.ReadInt32Internal();
                mIndex += 1;

                if (mEnablePosixFileAttributeExtension)
                {
                    // Posix file attributes are an unofficial extension abusing the fact that the
                    // official implementation only checks the lower 16 bits when reading archives.
                    // Newer versions of the UI client acknowledge this and try to parse the upper
                    // 16 bits as posix file attributes if they are nonzero. Note that this is done
                    // in the UI layer and not part of the official 7z archive implementation.
                    //
                    // Bit 15 is used by some implementations to indicate that the high 16 bits are
                    // storing posix file attributes, but since there are other implementations which
                    // don't set this bit we can't trust it and must discard it.
                    //
                    // For now we ignore posix attributes if we enabled this unofficial extension.
                    //
                    // Note that this is ok to do because the extension is designed to be compatible
                    // with readers which don't understand (but ignore) the posix attributes, like
                    // the official codebase does. The lower bits may contain windows-compatible
                    // file attributes even if the file has posix attributes, so we still need to
                    // read the lower bits regardless of whether we have posix attributes or not.
                    //
                    // Later we may want to provide an API to retrieve both kinds of attributes.
                    //
                    if ((attr & ArchivedAttributesExtensions.PosixAttributeMask) != 0)
                        attr &= ~ArchivedAttributesExtensions.PosixAttributeMask;
                }

                if ((attr & ArchivedAttributesExtensions.InvalidAttributes) != 0)
                    throw new System.IO.InvalidDataException();

                attr &= ~(ArchivedAttributesExtensions.ForbiddenAttributes | ArchivedAttributesExtensions.StrippedAttributes);

                return attr;
            }
            else
            {
                mIndex += 1;
                return null;
            }
        }
    }

    public sealed class MetadataBitReader
    {
        private ArchiveMetadataReader mReader;
        private BitVector mVector;
        private int mIndex;

        internal MetadataBitReader(ArchiveMetadataReader reader, BitVector vector)
        {
            mReader = reader;
            mVector = vector;
        }

        internal void Complete()
        {
            while (mIndex < mVector.Count)
                ReadBit();

            mReader = null;
        }

        public int Count => mVector.Count;
        public int Index => mIndex;

        public bool ReadBit()
        {
            if (mReader == null)
                throw new ObjectDisposedException(null);

            if (mIndex == mVector.Count)
                throw new InvalidOperationException();

            return mVector[mIndex++];
        }
    }
}
