using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;

namespace ManagedLzma.SevenZip.Writer
{
    public sealed class EncoderDefinition
    {
        private ArchiveEncoderOutputSlot mContent;
        private List<EncoderNodeDefinition> mEncoders;
        private List<ArchiveEncoderInputSlot> mStorage;
        private bool mComplete;

        public int EncoderCount => mEncoders.Count;
        public int StorageCount => mStorage.Count;

        public EncoderDefinition()
        {
            mContent = new ArchiveEncoderOutputSlot(this);
            mEncoders = new List<EncoderNodeDefinition>();
            mStorage = new List<ArchiveEncoderInputSlot>();
        }

        public EncoderNodeDefinition GetEncoder(int index)
        {
            if (index < 0 || index >= mEncoders.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return mEncoders[index];
        }

        public ArchiveEncoderInputSlot GetStorage(int index)
        {
            if (index < 0 || index >= mStorage.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return mStorage[index];
        }

        public ArchiveEncoderOutputSlot GetContentSource()
        {
            return mContent;
        }

        private void CheckComplete()
        {
            if (mComplete)
                throw new InvalidOperationException("Complete encoder definitions cannot be modified.");
        }

        public EncoderNodeDefinition CreateEncoder(EncoderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            CheckComplete();

            var encoder = new EncoderNodeDefinition(this, mEncoders.Count, settings);
            mEncoders.Add(encoder);
            return encoder;
        }

        public ArchiveEncoderInputSlot CreateStorageSink()
        {
            CheckComplete();

            var storage = new ArchiveEncoderInputSlot(this, mStorage.Count);
            mStorage.Add(storage);
            return storage;
        }

        public void Connect(ArchiveEncoderOutputSlot source, ArchiveEncoderInputSlot target)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (target == null)
                throw new ArgumentNullException(nameof(target));

            if (source.Definition != this)
                throw new ArgumentException("Data source belongs to a different encoder definition.", nameof(source));

            if (target.Definition != this)
                throw new ArgumentException("Data target belongs to a different encoder definition.", nameof(target));

            if (source.IsConnected)
                throw new ArgumentException("Data source is already connected.", nameof(source));

            if (target.IsConnected)
                throw new ArgumentException("Data target is already connected.", nameof(target));

            CheckComplete();

            source.ConnectTo(target);
            target.ConnectTo(source);
        }

        public void Complete()
        {
            if (!mComplete)
            {
                if (mEncoders.Count == 0)
                    throw new InvalidOperationException("No encoders defined.");

                if (!mContent.IsConnected)
                    throw new InvalidOperationException("Content source not connected.");

                for (int i = 0; i < mEncoders.Count; i++)
                {
                    var encoder = mEncoders[i];

                    for (int j = 0; j < encoder.InputCount; j++)
                        if (!encoder.GetInput(j).IsConnected)
                            throw new InvalidOperationException(String.Format("Missing input connection #{0} for encoder #{1}.", j, i));

                    for (int j = 0; j < encoder.OutputCount; j++)
                        if (!encoder.GetOutput(j).IsConnected)
                            throw new InvalidOperationException(String.Format("Missing output connection #{0} for encoder #{1}.", j, i));
                }

                mComplete = true;
            }
        }

        internal EncoderSession CreateEncoderSession(ArchiveWriter writer, int section, Stream[] storage, bool calculateStorageChecksums)
        {
            if (!mComplete)
                throw new InvalidOperationException("Incomplete ArchiveEncoderDefinition.");

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storage.Length != mStorage.Count)
                throw new ArgumentException("Number of provided storage streams does not match number of declared storage streams.", nameof(storage));

            var storageStreams = new EncoderStorage[storage.Length];
            for (int i = 0; i < storage.Length; i++)
                storageStreams[i] = new EncoderStorage(storage[i], calculateStorageChecksums);

            int totalInputCount = 0;
            var firstInputOffset = new int[mEncoders.Count];
            for (int i = 0; i < mEncoders.Count; i++)
            {
                firstInputOffset[i] = totalInputCount;
                totalInputCount += mEncoders[i].InputCount;
            }

            var contentTarget = mContent.Target;
            var contentIndex = firstInputOffset[contentTarget.Node.Index] + contentTarget.Index;
            var contentStream = new EncoderInput(true); // TODO: make checksum calculation configurable? I think normal 7z archives skip this one, at least with copy-aes encoder graphs?

            var linkedStreams = new EncoderConnection[totalInputCount];
            for (int i = 0; i < totalInputCount; i++)
                if (i != contentIndex)
                    linkedStreams[i] = new EncoderConnection();

            var encoders = new EncoderNode[mEncoders.Count];

            for (int i = 0; i < mEncoders.Count; i++)
            {
                var settings = mEncoders[i].Settings;
                var encoder = settings.CreateEncoder();
                encoders[i] = encoder;

                for (int n = settings.GetInputSlots(), j = 0; j < n; j++)
                {
                    var source = mEncoders[i].GetInput(j).Source;
                    if (source.IsContent)
                        contentStream.SetInputStream(encoder, j);
                    else
                        linkedStreams[firstInputOffset[i] + j].SetInputStream(encoder, j);
                }

                for (int n = settings.GetOutputSlots(), j = 0; j < n; j++)
                {
                    var target = mEncoders[i].GetOutput(j).Target;
                    if (target.IsStorage)
                        storageStreams[target.Index].SetOutputStream(encoder, j);
                    else
                        linkedStreams[firstInputOffset[target.Node.Index] + target.Index].SetOutputStream(encoder, j);
                }
            }

            var session = new EncoderSession(writer, section, this, contentStream, storageStreams, linkedStreams, encoders);

            contentStream.Start();

            foreach (var stream in storageStreams)
                stream.Start();

            foreach (var stream in linkedStreams)
                stream?.Start();

            foreach (var encoder in encoders)
                encoder.Start();

            return session;
        }
    }

    public abstract class EncoderSettings
    {
        internal EncoderSettings() { }
        internal abstract CompressionMethod GetDecoderType();
        internal abstract EncoderNode CreateEncoder();
        internal abstract ImmutableArray<byte> SerializeSettings();
        internal int GetInputSlots() => GetDecoderType().GetOutputCount(); // encoder input = decoder output
        internal int GetOutputSlots() => GetDecoderType().GetInputCount(); // encoder output = decoder input
    }

    public sealed class EncoderNodeDefinition
    {
        private readonly EncoderDefinition mDefinition;
        private readonly EncoderSettings mSettings;
        private readonly ArchiveEncoderInputSlot[] mInputSlots;
        private readonly ArchiveEncoderOutputSlot[] mOutputSlots;
        private readonly int mIndex;

        public EncoderDefinition Definition => mDefinition;
        public int Index => mIndex;
        public EncoderSettings Settings => mSettings;
        public int InputCount => mInputSlots.Length;
        public int OutputCount => mOutputSlots.Length;

        internal EncoderNodeDefinition(EncoderDefinition definition, int index, EncoderSettings settings)
        {
            mDefinition = definition;
            mIndex = index;
            mSettings = settings;

            mInputSlots = new ArchiveEncoderInputSlot[settings.GetInputSlots()];
            for (int i = 0; i < mInputSlots.Length; i++)
                mInputSlots[i] = new ArchiveEncoderInputSlot(this, i);

            mOutputSlots = new ArchiveEncoderOutputSlot[settings.GetOutputSlots()];
            for (int i = 0; i < mOutputSlots.Length; i++)
                mOutputSlots[i] = new ArchiveEncoderOutputSlot(this, i);
        }

        public ArchiveEncoderInputSlot GetInput(int index)
        {
            return mInputSlots[index];
        }

        public ArchiveEncoderOutputSlot GetOutput(int index)
        {
            return mOutputSlots[index];
        }
    }

    public sealed class ArchiveEncoderInputSlot
    {
        private readonly EncoderDefinition mDefinition;
        private readonly EncoderNodeDefinition mNode;
        private ArchiveEncoderOutputSlot mSource;
        private readonly int mIndex;

        public EncoderDefinition Definition => mDefinition;
        public EncoderNodeDefinition Node => mNode;
        public int Index => mIndex;
        public bool IsStorage => mNode == null;
        public bool IsConnected => mSource != null;
        public ArchiveEncoderOutputSlot Source => mSource;

        internal ArchiveEncoderInputSlot(EncoderDefinition definition, int index)
        {
            mDefinition = definition;
            mIndex = index;
        }

        internal ArchiveEncoderInputSlot(EncoderNodeDefinition node, int index)
        {
            mDefinition = node.Definition;
            mNode = node;
            mIndex = index;
        }

        internal void ConnectTo(ArchiveEncoderOutputSlot source)
        {
            mSource = source;
        }
    }

    public sealed class ArchiveEncoderOutputSlot
    {
        private readonly EncoderDefinition mDefinition;
        private readonly EncoderNodeDefinition mNode;
        private ArchiveEncoderInputSlot mTarget;
        private readonly int mIndex;

        public EncoderDefinition Definition => mDefinition;
        public EncoderNodeDefinition Node => mNode;
        public int Index => mIndex;
        public bool IsContent => mNode == null;
        public bool IsConnected => mTarget != null;
        public ArchiveEncoderInputSlot Target => mTarget;

        internal ArchiveEncoderOutputSlot(EncoderDefinition definition)
        {
            mDefinition = definition;
        }

        internal ArchiveEncoderOutputSlot(EncoderNodeDefinition node, int index)
        {
            mDefinition = node.Definition;
            mNode = node;
            mIndex = index;
        }

        internal void ConnectTo(ArchiveEncoderInputSlot target)
        {
            mTarget = target;
        }
    }
}
