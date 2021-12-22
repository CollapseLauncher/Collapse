using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;

namespace ManagedLzma.SevenZip.Writer
{
    public sealed class LzmaEncoderSettings : EncoderSettings
    {
        private readonly LZMA.EncoderSettings mSettings;

        internal override CompressionMethod GetDecoderType() => CompressionMethod.LZMA;

        public LzmaEncoderSettings(LZMA.EncoderSettings settings)
        {
            mSettings = settings;
        }

        internal override ImmutableArray<byte> SerializeSettings()
        {
            return mSettings.GetSerializedSettings();
        }

        internal override EncoderNode CreateEncoder()
        {
            return new LzmaEncoderNode(mSettings);
        }
    }

    internal sealed class LzmaEncoderNode : EncoderNode
    {
        private LZMA.AsyncEncoder mEncoder;
        private Task mTask;
        private IStreamReader mInput;
        private IStreamWriter mOutput;

        public LzmaEncoderNode(LZMA.EncoderSettings settings)
        {
            mEncoder = new LZMA.AsyncEncoder(settings);
        }

        public override IStreamWriter GetInputSink(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return null;
        }

        public override void SetInputSource(int index, IStreamReader stream)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (mInput != null)
                throw new InvalidOperationException();

            mInput = stream;
        }

        public override IStreamReader GetOutputSource(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return null;
        }

        public override void SetOutputSink(int index, IStreamWriter stream)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (mOutput != null)
                throw new InvalidOperationException();

            mOutput = stream;
        }

        public override void Start()
        {
            if (mTask != null)
                throw new InvalidOperationException();

            mTask = mEncoder.EncodeAsync(mInput, mOutput);
        }

        public override void Dispose()
        {
            mEncoder.Dispose();

            // We must observe potential exceptions on our tasks, otherwise the whole process will get killed.
            try { mTask?.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
        }
    }
}
