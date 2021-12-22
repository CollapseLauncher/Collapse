using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip.Reader
{
    internal sealed class LzmaArchiveDecoder : DecoderNode
    {
        private sealed class OutputStream : ReaderNode
        {
            private LzmaArchiveDecoder mOwner;
            public OutputStream(LzmaArchiveDecoder owner) { mOwner = owner; }
            public override void Dispose() { mOwner = null; }
            public override void Skip(int count) => mOwner.Skip(count);
            public override int Read(byte[] buffer, int offset, int count) => mOwner.Read(buffer, offset, count);
        }

        // TODO: can we get rid of that buffer?
        private byte[] mBuffer = new byte[4 << 10];
        private int mBufferOffset;
        private int mBufferEnding;

        private LZMA.Decoder mDecoder;
        private ReaderNode mInput;
        private ReaderNode mOutput;
        private long mOutputLength;
        private long mPosition;

        public LzmaArchiveDecoder(ImmutableArray<byte> settings, long length)
        {
            mDecoder = new LZMA.Decoder(LZMA.DecoderSettings.CreateFrom(settings));
            mOutput = new OutputStream(this);
            mOutputLength = length;
        }

        public override void Dispose()
        {
            mDecoder.Dispose();
            mOutput.Dispose();
            mInput?.Dispose();
        }

        public override void SetInputStream(int index, ReaderNode stream, long length)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            mInput = stream;
        }

        public override ReaderNode GetOutputStream(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return mOutput;
        }

        private void Skip(int count)
        {
            while (count > 0)
            {
                if (mDecoder.IsOutputComplete)
                    throw new InvalidDataException();

                if (mDecoder.AvailableOutputLength > 0)
                {
                    var result = mDecoder.SkipOutputData(count);
                    mPosition += result;
                    count -= result;
                }
                else
                {
                    Decode();
                }
            }
        }

        private int Read(byte[] buffer, int offset, int count)
        {
            for (;;)
            {
                if (mDecoder.IsOutputComplete)
                    return 0;

                if (mDecoder.AvailableOutputLength > 0)
                {
                    var result = mDecoder.ReadOutputData(buffer, offset, count);
                    mPosition += result;
                    return result;
                }

                Decode();
            }
        }

        private void Decode()
        {
            if (mBufferOffset == mBufferEnding)
            {
#if DEBUG
                // Avoid confusing people if they break into the debugger before mBufferOffset has been updated.
                mBufferOffset = 0;
                mBufferEnding = 0;
#endif

                mBufferEnding = mInput.Read(mBuffer, 0, mBuffer.Length);
                mBufferOffset = mDecoder.Decode(mBuffer, 0, mBufferEnding, null, mBufferEnding == 0);
            }
            else
            {
                mBufferOffset += mDecoder.Decode(mBuffer, mBufferOffset, mBufferEnding - mBufferOffset, null, false);
            }
        }
    }
}
