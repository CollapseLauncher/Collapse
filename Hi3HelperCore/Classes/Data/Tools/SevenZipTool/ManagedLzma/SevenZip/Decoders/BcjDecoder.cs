using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip.Reader
{
    internal sealed class BcjArchiveDecoder : DecoderNode
    {
        private sealed class OutputStream : ReaderNode
        {
            private BcjArchiveDecoder mOwner;
            public OutputStream(BcjArchiveDecoder owner) { mOwner = owner; }
            public override void Dispose() { mOwner = null; }
            public override void Skip(int count) => mOwner.Skip(count);
            public override int Read(byte[] buffer, int offset, int count) => mOwner.Read(buffer, offset, count);
        }

        private static readonly bool[] kMaskToAllowedStatus = { true, true, true, false, true, false, false, false };
        private static readonly byte[] kMaskToBitNumber = { 0, 1, 2, 2, 3, 3, 3, 3 };

        private ReaderNode mInput;
        private OutputStream mOutput;
        private long mLength;
        private long mPosition;

        private long mBufferPos;
        private uint mState;
        private byte[] mBuffer;
        private int mOffset;
        private int mEnding;
        private bool mInputEnd;

        public BcjArchiveDecoder(ImmutableArray<byte> settings, long length)
        {
            mBuffer = new byte[4 << 10];
            mOutput = new OutputStream(this);
            mLength = length;
        }

        public override void Dispose()
        {
            Utilities.NeedsReview();

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
            Utilities.NeedsBetterImplementation();

            var buffer = new byte[Math.Min(0x4000, count)];
            while (count > 0)
            {
                var skipped = Read(buffer, 0, Math.Min(buffer.Length, count));
                if (skipped == 0)
                    throw new InvalidOperationException(ErrorStrings.SkipBeyondEndOfStream);

                count -= skipped;
            }
        }

        private int Read(byte[] buffer, int offset, int count)
        {
            var result = DecodeInto(buffer, offset, count);
            mPosition += result;
            return result;
        }

        private int DecodeInto(byte[] buffer, int offset, int count)
        {
            if (mOffset == mEnding)
            {
                mOffset = 0;
                mEnding = 0;
            }

            while (mEnding - mOffset < 5)
            {
                if (mInputEnd)
                {
                    // if less than 5 bytes are left they are copied
                    int n = 0;
                    while (mOffset < mEnding && count > 0)
                    {
                        buffer[offset++] = mBuffer[mOffset++];
                        count--;
                        n++;
                    }
                    return n;
                }

                if (mBuffer.Length - mOffset < 5)
                {
                    Buffer.BlockCopy(mBuffer, mOffset, mBuffer, 0, mEnding - mOffset);
                    mEnding -= mOffset;
                    mOffset = 0;
                }

                int delta = mInput.Read(mBuffer, mEnding, mBuffer.Length - mEnding);
                if (delta == 0)
                    mInputEnd = true;
                else
                    mEnding += delta;
            }

            {
                int delta = x86_Convert(LZMA.P.From(mBuffer, mOffset), Math.Min(mEnding - mOffset, count), (uint)mBufferPos);
                if (delta == 0)
                    throw new NotSupportedException();

                Buffer.BlockCopy(mBuffer, mOffset, buffer, offset, delta);
                mOffset += delta;

                mBufferPos += delta;
                return delta;
            }
        }

        private static bool Test86MSByte(byte value)
        {
            return value == 0x00 || value == 0xFF;
        }

        private int x86_Convert(LZMA.P<byte> data, int size, uint ip)
        {
            int bufferPos = 0;
            uint prevMask = mState & 0x7;

            if (size < 5)
                return 0;

            ip += 5;
            int prevPosT = -1;

            for (;;)
            {
                var p = data + bufferPos;
                var limit = data + size - 4;

                while (p < limit && (p[0] & 0xFE) != 0xE8)
                    p++;

                bufferPos = (int)(p - data);
                if (p >= limit)
                    break;

                prevPosT = bufferPos - prevPosT;

                if (prevPosT > 3)
                {
                    prevMask = 0;
                }
                else
                {
                    prevMask = (prevMask << ((int)prevPosT - 1)) & 0x7;
                    if (prevMask != 0)
                    {
                        byte b = p[4 - kMaskToBitNumber[prevMask]];
                        if (!kMaskToAllowedStatus[prevMask] || Test86MSByte(b))
                        {
                            prevPosT = bufferPos;
                            prevMask = ((prevMask << 1) & 0x7) | 1;
                            bufferPos++;
                            continue;
                        }
                    }
                }

                prevPosT = bufferPos;

                if (Test86MSByte(p[4]))
                {
                    uint src = ((uint)p[4] << 24) | ((uint)p[3] << 16) | ((uint)p[2] << 8) | ((uint)p[1]);
                    uint dest;
                    for (;;)
                    {
                        dest = src - (ip + (uint)bufferPos);

                        if (prevMask == 0)
                            break;

                        int index = kMaskToBitNumber[prevMask] * 8;
                        byte b = (byte)(dest >> (24 - index));
                        if (!Test86MSByte(b))
                            break;

                        src = dest ^ ((1u << (32 - index)) - 1);
                    }

                    p[4] = (byte)(~(((dest >> 24) & 1) - 1));
                    p[3] = (byte)(dest >> 16);
                    p[2] = (byte)(dest >> 8);
                    p[1] = (byte)dest;
                    bufferPos += 5;
                }
                else
                {
                    prevMask = ((prevMask << 1) & 0x7) | 1;
                    bufferPos++;
                }
            }

            prevPosT = bufferPos - prevPosT;
            mState = ((prevPosT > 3) ? 0 : ((prevMask << ((int)prevPosT - 1)) & 0x7));
            return bufferPos;
        }
    }
}
