using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;

namespace ManagedLzma.SevenZip.Writer
{
    public sealed class Lzma2EncoderSettings : EncoderSettings
    {
        private readonly LZMA2.EncoderSettings mSettings;

        internal override CompressionMethod GetDecoderType() => CompressionMethod.LZMA2;

        public Lzma2EncoderSettings(LZMA2.EncoderSettings settings)
        {
            mSettings = settings;
        }

        private static uint LZMA2_DIC_SIZE_FROM_PROP(int p)
        {
            return (uint)(2 | (p & 1)) << (p / 2 + 11);
        }

        private static byte Lzma2Enc_WriteProperties(LZMA2.EncoderSettings mProps)
        {
            uint dicSize = mProps.GetInternalSettings().mLzmaProps.LzmaEncProps_GetDictSize();

            int i = 0;
            while (i < 40 && dicSize > LZMA2_DIC_SIZE_FROM_PROP(i))
                i++;

            return (byte)i;
        }

        internal override ImmutableArray<byte> SerializeSettings()
        {
            return ImmutableArray.Create(Lzma2Enc_WriteProperties(mSettings));
        }

        internal override EncoderNode CreateEncoder()
        {
            return new Lzma2EncoderNode(mSettings);
        }
    }

    internal sealed class Lzma2EncoderNode : EncoderNode
    {
        private LZMA2.AsyncEncoder mEncoder;
        private Task mTask;
        private IStreamReader mInput;
        private IStreamWriter mOutput;

        public Lzma2EncoderNode(LZMA2.EncoderSettings settings)
        {
            mEncoder = new LZMA2.AsyncEncoder(settings);
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
