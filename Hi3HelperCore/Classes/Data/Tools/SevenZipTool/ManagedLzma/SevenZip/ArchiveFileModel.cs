using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;
using ManagedLzma.SevenZip.Reader;

namespace ManagedLzma.SevenZip.FileModel
{
    public sealed class ArchiveFileModel
    {
        /// <summary>The metadata of the archive.</summary>
        public ArchiveMetadata Metadata { get; }

        /// <summary>The hierarchic structure of files and folders within the archive.</summary>
        public ArchivedFolder RootFolder { get; }

        private readonly ImmutableList<ArchivedFile> mSectionFiles;
        private readonly ImmutableArray<int> mSections;

        internal ArchiveFileModel(ArchiveMetadata metadata, ArchivedFolder rootFolder, ImmutableArray<int> sections, ImmutableList<ArchivedFile> files)
        {
            if (metadata == null)
                throw new ArgumentNullException(nameof(metadata));

            if (rootFolder == null)
                throw new ArgumentNullException(nameof(rootFolder));

            if (files == null)
                throw new ArgumentNullException(nameof(files));

            if (sections.IsDefault)
                throw new ArgumentNullException(nameof(sections));

            if (sections.Length != metadata.DecoderSections.Length + 1)
                throw new InternalFailureException();

            this.Metadata = metadata;
            this.RootFolder = rootFolder;
            this.mSectionFiles = files;
            this.mSections = sections;
        }

        /// <summary>
        /// Returns file metadata for the given decoder section.
        /// The returned list may contain null entries when the corresponding stream should not be written to a file.
        /// </summary>
        /// <param name="sectionIndex">The decoder section for which files should be returned.</param>
        public ImmutableList<ArchivedFile> GetFilesInSection(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= Metadata.DecoderSections.Length)
                throw new ArgumentOutOfRangeException(nameof(sectionIndex));

            var offset = mSections[sectionIndex];
            var length = mSections[sectionIndex + 1] - offset;
            return mSectionFiles.GetRange(offset, length);
        }
    }

    public abstract class ArchivedItem
    {
        public abstract class Builder
        {
            public string FullName { get; set; }
            public string Name { get; set; }

            internal Builder() { }

            internal Builder(ArchivedItem original)
            {
                this.FullName = original.FullName;
                this.Name = original.Name;
            }

            internal abstract ArchivedItem ToImmutableCore();
            public ArchivedItem ToImmutable() => ToImmutableCore();
        }

        /// <summary>
        /// The full name of the item, including the names of the parent folders within the archive.
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// The name of the item.
        /// </summary>
        public string Name { get; }

        internal ArchivedItem(string FullName, string Name)
        {
            this.FullName = FullName;
            this.Name = Name;
        }
    }

    public sealed class ArchivedFile : ArchivedItem
    {
        public new sealed class Builder : ArchivedItem.Builder
        {
            public DecodedStreamIndex Stream { get; set; }
            public long Offset { get; set; }
            public long Length { get; set; }
            public Checksum? Checksum { get; set; }
            public ArchivedAttributes? Attributes { get; set; }
            public DateTime? Creation { get; set; }
            public DateTime? LastWrite { get; set; }
            public DateTime? LastAccess { get; set; }

            public Builder() { }

            public Builder(ArchivedFile original)
                : base(original)
            {
                Stream = original.Stream;
                Offset = original.Offset;
                Length = original.Length;
                Checksum = original.Checksum;
                Attributes = original.Attributes;
                Creation = original.Creation;
                LastWrite = original.LastWrite;
                LastAccess = original.LastAccess;
            }

            internal override ArchivedItem ToImmutableCore() => ToImmutable();
            public new ArchivedFile ToImmutable() => new ArchivedFile(FullName, Name, Stream, Offset, Length, Checksum, Attributes, Creation, LastWrite, LastAccess);
        }

        public DecodedStreamIndex Stream { get; }
        public long Offset { get; }
        public long Length { get; }
        public Checksum? Checksum { get; }
        public ArchivedAttributes? Attributes { get; }
        public DateTime? Creation { get; }
        public DateTime? LastWrite { get; }
        public DateTime? LastAccess { get; }

        public ArchivedFile(string FullName, string Name, DecodedStreamIndex Stream, long Offset, long Length, Checksum? Checksum, ArchivedAttributes? Attributes, DateTime? Creation, DateTime? LastWrite, DateTime? LastAccess)
            : base(FullName, Name)
        {
            this.Stream = Stream;
            this.Offset = Offset;
            this.Length = Length;
            this.Checksum = Checksum;
            this.Attributes = Attributes;
            this.Creation = Creation;
            this.LastWrite = LastWrite;
            this.LastAccess = LastAccess;
        }
    }

    public sealed class ArchivedFolder : ArchivedItem
    {
        public new sealed class Builder : ArchivedItem.Builder
        {
            private ArchivedFolder mOriginal;
            private ImmutableList<ArchivedItem>.Builder mItemsBuilder;
            public ImmutableList<ArchivedItem>.Builder Items => mItemsBuilder ?? CreateItemsBuilder();

            private ImmutableList<ArchivedItem>.Builder CreateItemsBuilder()
            {
                if (mOriginal == null)
                    mItemsBuilder = ImmutableList.CreateBuilder<ArchivedItem>();
                else
                    mItemsBuilder = mOriginal.Items.ToBuilder();

                return mItemsBuilder;
            }

            private ImmutableList<ArchivedItem> GetImmutableItems()
            {
                if (mItemsBuilder != null)
                    return mItemsBuilder.ToImmutable();
                else if (mOriginal != null)
                    return mOriginal.Items;
                else
                    return ImmutableList<ArchivedItem>.Empty;
            }

            public Builder() { }

            public Builder(ArchivedFolder original)
                : base(original)
            {
                this.mOriginal = original;
            }

            internal override ArchivedItem ToImmutableCore() => ToImmutable();
            public new ArchivedFolder ToImmutable() => new ArchivedFolder(FullName, Name, GetImmutableItems());
        }

        public ImmutableList<ArchivedItem> Items { get; }

        public ArchivedFolder(string FullName, string Name, ImmutableList<ArchivedItem> Items)
            : base(FullName, Name)
        {
            this.Items = Items;
        }
    }

    /// <summary>
    /// Reads archive metadata and constructs a class hierarchy of the contained files.
    /// </summary>
    public sealed class ArchiveFileModelMetadataReader : ArchiveMetadataReader
    {
        private static readonly char[] kFolderSeparators = { '\\', '/' };

        private bool mIsRunning;
        private int mItemCount;
        private int mItemsWithoutStream;

        private List<string> mFileNames;
        private List<bool> mItemsWithoutStreamMarkers;
        private List<bool> mEmptyFileMarkers;
        private List<bool> mDeletionMarkers;
        private List<long?> mOffsets;
        private List<ArchivedAttributes?> mAttributes;
        private List<DateTime?> mCDates;
        private List<DateTime?> mMDates;
        private List<DateTime?> mADates;

        private ArchivedFolder.Builder mRootFolder;
        private Dictionary<ArchivedFolder.Builder, List<ArchivedItem.Builder>> mItemMap;
        private HashSet<ArchivedFile.Builder> mFiles;
        private List<ArchivedFile.Builder> mStreamMap;
        private ImmutableArray<int>.Builder mSectionMap;

        public ArchiveFileModel ReadMetadata(Stream stream) => ReadMetadata(stream, null);

        public ArchiveFileModel ReadMetadata(Stream stream, PasswordStorage password)
        {
            if (mIsRunning)
                throw new InvalidOperationException("Recursive invocation.");

            try
            {
                mIsRunning = true;

                // reset fields to detect bugs
                mFileNames = null;
                mItemsWithoutStreamMarkers = null;
                mEmptyFileMarkers = null;
                mDeletionMarkers = null;
                mOffsets = null;
                mAttributes = null;
                mCDates = null;
                mMDates = null;
                mADates = null;
                mItemCount = -1;
                mItemsWithoutStream = -1;

                var metadata = ReadMetadataCore(stream, password);

                mRootFolder = new ArchivedFolder.Builder();
                mItemMap = new Dictionary<ArchivedFolder.Builder, List<ArchivedItem.Builder>>();
                mItemMap.Add(mRootFolder, new List<ArchivedItem.Builder>());
                mFiles = new HashSet<ArchivedFile.Builder>();
                mStreamMap = new List<ArchivedFile.Builder>();
                mSectionMap = ImmutableArray.CreateBuilder<int>(metadata.DecoderSections.Length + 1);

                ArchiveDecoderSection currentDecoder = null;
                int currentSectionIndex = -1;
                int currentStreamIndex = 0;
                int currentStreamCount = 0;
                int currentEmptyIndex = 0;

                for (int currentFileIndex = 0; currentFileIndex < mFileNames.Count; currentFileIndex++)
                {
                    var filename = mFileNames[currentFileIndex];
                    bool hasStream = false;
                    ArchivedFile.Builder file = null;

                    if (mItemsWithoutStreamMarkers != null && mItemsWithoutStreamMarkers[currentFileIndex])
                    {
                        var isFile = (mEmptyFileMarkers == null || mEmptyFileMarkers[currentEmptyIndex]);
                        var isDeletionMarker = (mDeletionMarkers != null && mDeletionMarkers[currentEmptyIndex]);

                        if (isDeletionMarker)
                            RemoveItem(mItemMap[mRootFolder], filename, 0);
                        else
                            file = AddItem(mItemMap[mRootFolder], filename, 0, isFile);

                        currentEmptyIndex++;
                    }
                    else
                    {
                        hasStream = true;
                        file = AddItem(mItemMap[mRootFolder], filename, 0, true);
                    }

                    if (file != null)
                    {
                        if (mOffsets != null)
                            file.Offset = mOffsets[currentFileIndex] ?? 0;

                        if (mAttributes != null)
                        {
                            var attr = mAttributes[currentFileIndex];

                            if (attr.HasValue && (attr.Value & ArchivedAttributesExtensions.DirectoryAttribute) != 0)
                                // throw new InvalidDataException();

                            file.Attributes = attr;
                        }

                        if (mCDates != null)
                            file.Creation = mCDates[currentFileIndex];

                        if (mMDates != null)
                            file.LastWrite = mMDates[currentFileIndex];

                        if (mADates != null)
                            file.LastAccess = mADates[currentFileIndex];

                        if (hasStream)
                        {
                            while (currentStreamIndex == currentStreamCount)
                            {
                                if (currentSectionIndex == metadata.DecoderSections.Length - 1)
                                    throw new InvalidDataException();

                                currentDecoder = metadata.DecoderSections[++currentSectionIndex];
                                currentStreamCount = currentDecoder.Streams.Length;
                                currentStreamIndex = 0;

                                mSectionMap.Add(mStreamMap.Count);
                            }

                            file.Stream = new DecodedStreamIndex(currentSectionIndex, currentStreamIndex);

                            var streamMetadata = currentDecoder.Streams[currentStreamIndex++];
                            file.Length = streamMetadata.Length;
                            file.Checksum = streamMetadata.Checksum;
                        }
                    }

                    if (hasStream)
                        mStreamMap.Add(file);
                }

                var finalStreamMap = ImmutableList.CreateBuilder<ArchivedFile>();
                foreach (var file in mStreamMap)
                    finalStreamMap.Add(mFiles.Contains(file) ? file.ToImmutable() : null);

                if (currentStreamIndex != currentStreamCount || currentSectionIndex != metadata.DecoderSections.Length - 1)
                    throw new InvalidDataException();

                mSectionMap.Add(finalStreamMap.Count);

                return new ArchiveFileModel(metadata, BuildFolder(mRootFolder), mSectionMap.MoveToImmutable(), finalStreamMap.ToImmutable());
            }
            finally
            {
                mIsRunning = false;
            }
        }

        private ArchivedFile.Builder AddItem(List<ArchivedItem.Builder> items, string fullName, int offset, bool isFile)
        {
            int ending = fullName.IndexOfAny(kFolderSeparators, offset);

            string itemName;
            if (ending < 0)
            {
                itemName = fullName.Substring(offset);
                if (isFile)
                    return AddFile(items, fullName, itemName);
            }
            else
            {
                itemName = fullName.Substring(offset, ending - offset);
            }

            ArchivedFolder.Builder folder = null;
            foreach (var item in items)
            {
                if (String.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase))
                {
                    folder = item as ArchivedFolder.Builder;
                    break;
                }
            }

            if (folder == null)
            {
                RemoveItem(items, itemName);
                folder = new ArchivedFolder.Builder();
                folder.FullName = ending < 0 ? fullName : fullName.Substring(0, ending);
                folder.Name = itemName;
                items.Add(folder);
                mItemMap.Add(folder, new List<ArchivedItem.Builder>());
            }

            if (ending < 0)
            {
                System.Diagnostics.Debug.Assert(!isFile);
                return null;
            }

            return AddItem(mItemMap[folder], fullName, ending + 1, isFile);
        }

        private ArchivedFile.Builder AddFile(List<ArchivedItem.Builder> items, string fullName, string name)
        {
            RemoveItem(items, name);
            var file = new ArchivedFile.Builder();
            file.FullName = fullName;
            file.Name = name;
            items.Add(file);
            mFiles.Add(file);
            return file;
        }

        private void RemoveItem(List<ArchivedItem.Builder> items, string fullName, int offset)
        {
            var ending = fullName.IndexOfAny(kFolderSeparators, offset);
            if (ending < 0)
            {
                var itemName = fullName.Substring(offset);
                RemoveItem(items, itemName);
            }
            else
            {
                var itemName = fullName.Substring(offset, ending - offset);
                foreach (var item in items)
                {
                    if (String.Equals(item.Name, itemName, StringComparison.OrdinalIgnoreCase))
                    {
                        var folder = item as ArchivedFolder.Builder;
                        if (folder != null)
                            RemoveItem(mItemMap[folder], fullName, ending + 1);

                        break;
                    }
                }
            }
        }

        private void RemoveItem(List<ArchivedItem.Builder> items, string name)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (String.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    RemoveItem(items[i]);
                    items.RemoveAt(i);
                    break;
                }
            }
        }

        private void RemoveItem(ArchivedItem.Builder item)
        {
            if (item is ArchivedFile.Builder)
                RemoveItem((ArchivedFile.Builder)item);

            if (item is ArchivedFolder.Builder)
                RemoveItem((ArchivedFolder.Builder)item);
        }

        private void RemoveItem(ArchivedFile.Builder file)
        {
            mFiles.Remove(file);
        }

        private void RemoveItem(ArchivedFolder.Builder folder)
        {
            foreach (var item in mItemMap[folder])
                RemoveItem(item);

            mItemMap.Remove(folder);
        }

        private ArchivedFolder BuildFolder(ArchivedFolder.Builder builder)
        {
            System.Diagnostics.Debug.Assert(builder.Items.Count == 0);

            foreach (var item in mItemMap[builder])
            {
                if (item is ArchivedFolder.Builder)
                    builder.Items.Add(BuildFolder((ArchivedFolder.Builder)item));
                else
                    builder.Items.Add(((ArchivedFile.Builder)item).ToImmutable());
            }

            return builder.ToImmutable();
        }

        protected override void Initialize(int count)
        {
            System.Diagnostics.Debug.Assert(count >= 0);
            mItemCount = count;
        }

        protected override void ReadNames(MetadataStringReader data)
        {
            System.Diagnostics.Debug.Assert(mFileNames == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mFileNames = new List<string>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mFileNames.Add(data.ReadString());
        }

        protected override void ReadEmptyStreamMarkers(MetadataBitReader data)
        {
            System.Diagnostics.Debug.Assert(mItemsWithoutStreamMarkers == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mItemsWithoutStream = 0;
            mItemsWithoutStreamMarkers = new List<bool>(data.Count);
            for (int i = 0; i < data.Count; i++)
            {
                var isItemWithoutStream = data.ReadBit();
                mItemsWithoutStreamMarkers.Add(isItemWithoutStream);
                if (isItemWithoutStream)
                    mItemsWithoutStream++;
            }
        }

        protected override void ReadEmptyFileMarkers(MetadataBitReader data)
        {
            System.Diagnostics.Debug.Assert(mEmptyFileMarkers == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemsWithoutStream);

            mEmptyFileMarkers = new List<bool>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mEmptyFileMarkers.Add(data.ReadBit());
        }

        protected override void ReadRemovedFileMarkers(MetadataBitReader data)
        {
            System.Diagnostics.Debug.Assert(mDeletionMarkers == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemsWithoutStream);

            mDeletionMarkers = new List<bool>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mDeletionMarkers.Add(data.ReadBit());
        }

        protected override void ReadOffsets(MetadataNumberReader data)
        {
            System.Diagnostics.Debug.Assert(mOffsets == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mOffsets = new List<long?>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mOffsets.Add(data.ReadNumber());
        }

        protected override void ReadAttributes(MetadataAttributeReader data)
        {
            System.Diagnostics.Debug.Assert(mAttributes == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mAttributes = new List<ArchivedAttributes?>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mAttributes.Add(data.ReadAttributes());
        }

        protected override void ReadCTime(MetadataDateReader data)
        {
            System.Diagnostics.Debug.Assert(mCDates == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mCDates = new List<DateTime?>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mCDates.Add(data.ReadDate());
        }

        protected override void ReadMTime(MetadataDateReader data)
        {
            System.Diagnostics.Debug.Assert(mMDates == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mMDates = new List<DateTime?>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mMDates.Add(data.ReadDate());
        }

        protected override void ReadATime(MetadataDateReader data)
        {
            System.Diagnostics.Debug.Assert(mADates == null);
            System.Diagnostics.Debug.Assert(data.Count == mItemCount);

            mADates = new List<DateTime?>(data.Count);
            for (int i = 0; i < data.Count; i++)
                mADates.Add(data.ReadDate());
        }
    }
}
