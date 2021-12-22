using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip.Reader
{
    internal sealed class Bcj2ArchiveDecoder : DecoderNode
    {
        private class RangeDecoder
        {
            internal ReaderNode mStream;
            private byte[] mBuffer;
            internal uint Range;
            internal uint Code;

            public RangeDecoder(ReaderNode stream)
            {
                mStream = stream;
                mBuffer = new byte[1];
                Range = 0xFFFFFFFF;
                for (int i = 0; i < 5; i++)
                    Code = (Code << 8) | ReadByte();
            }

            public byte ReadByte()
            {
                if (mStream.Read(mBuffer, 0, 1) == 0)
                    throw new EndOfStreamException();

                return mBuffer[0];
            }

            public void Dispose()
            {
                mStream.Dispose();
            }
        }

        private class StatusDecoder
        {
            private const int numMoveBits = 5;

            private const int kNumBitModelTotalBits = 11;
            private const uint kBitModelTotal = 1u << kNumBitModelTotalBits;

            private uint Prob;

            public StatusDecoder()
            {
                Prob = kBitModelTotal / 2;
            }

            private void UpdateModel(uint symbol)
            {
                /*
                Prob -= (Prob + ((symbol - 1) & ((1 << numMoveBits) - 1))) >> numMoveBits;
                Prob += (1 - symbol) << (kNumBitModelTotalBits - numMoveBits);
                */
                if (symbol == 0)
                    Prob += (kBitModelTotal - Prob) >> numMoveBits;
                else
                    Prob -= (Prob) >> numMoveBits;
            }

            public uint Decode(RangeDecoder decoder)
            {
                uint newBound = (decoder.Range >> kNumBitModelTotalBits) * Prob;
                if (decoder.Code < newBound)
                {
                    decoder.Range = newBound;
                    Prob += (kBitModelTotal - Prob) >> numMoveBits;
                    if (decoder.Range < kTopValue)
                    {
                        decoder.Code = (decoder.Code << 8) | decoder.ReadByte();
                        decoder.Range <<= 8;
                    }
                    return 0;
                }
                else
                {
                    decoder.Range -= newBound;
                    decoder.Code -= newBound;
                    Prob -= Prob >> numMoveBits;
                    if (decoder.Range < kTopValue)
                    {
                        decoder.Code = (decoder.Code << 8) | decoder.ReadByte();
                        decoder.Range <<= 8;
                    }
                    return 1;
                }
            }
        }

        private sealed class OutputStream : ReaderNode
        {
            private Bcj2ArchiveDecoder mOwner;
            public OutputStream(Bcj2ArchiveDecoder owner) { mOwner = owner; }
            public override void Dispose() { mOwner = null; }
            public override void Skip(int count) => mOwner.Skip(count);
            public override int Read(byte[] buffer, int offset, int count) => mOwner.Read(buffer, offset, count);
        }

        private const int kNumTopBits = 24;
        private const uint kTopValue = (1 << kNumTopBits);

        private OutputStream mOutput;
        private long mLength;
        private long mPosition;

        private ReaderNode mMainStream;
        private ReaderNode mCallStream;
        private ReaderNode mJumpStream;
        private RangeDecoder mRangeDecoder;
        private StatusDecoder[] mStatusDecoder;
        private long mWritten;
        private long mLimit;
        private IEnumerator<byte> mIter;
        private bool mFinished;

        public Bcj2ArchiveDecoder(ImmutableArray<byte> settings, long length)
        {
            if (!settings.IsDefaultOrEmpty)
                throw new ArgumentException();

            mOutput = new OutputStream(this);
            mLength = length;
            mLimit = length;
        }

        public override void Dispose()
        {
            Utilities.NeedsReview();

            mOutput.Dispose();
            mMainStream?.Dispose();
            mCallStream?.Dispose();
            mJumpStream?.Dispose();
            mRangeDecoder?.Dispose();
        }

        public override void SetInputStream(int index, ReaderNode stream, long length)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            switch (index)
            {
                case 0: mMainStream = stream; break;
                case 1: mCallStream = stream; break;
                case 2: mJumpStream = stream; break;
                case 3: mRangeDecoder = new RangeDecoder(stream); break;
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
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
            if (mStatusDecoder == null)
            {
                mStatusDecoder = new StatusDecoder[256 + 2];
                for (int i = 0; i < mStatusDecoder.Length; i++)
                    mStatusDecoder[i] = new StatusDecoder();

                mIter = Run().GetEnumerator();
            }

            var result = DecodeInto(buffer, offset, count);
            mPosition += result;
            return result;
        }

        private static bool IsJcc(byte b0, byte b1)
        {
            return b0 == 0x0F
                && (b1 & 0xF0) == 0x80;
        }

        private static bool IsJ(byte b0, byte b1)
        {
            return (b1 & 0xFE) == 0xE8
                || IsJcc(b0, b1);
        }

        private static int GetIndex(byte b0, byte b1)
        {
            if (b1 == 0xE8)
                return b0;
            else if (b1 == 0xE9)
                return 256;
            else
                return 257;
        }

        public int DecodeInto(byte[] buffer, int offset, int count)
        {
            if (count == 0 || mFinished)
                return 0;

            for (int i = 0; i < count; i++)
            {
                if (!mIter.MoveNext())
                {
                    mFinished = true;
                    return i;
                }

                buffer[offset + i] = mIter.Current;
            }

            return count;
        }

        public IEnumerable<byte> Run()
        {
            const uint kBurstSize = (1u << 18);
            var tempBuffer = new byte[1];

            byte prevByte = 0;
            uint processedBytes = 0;
            for (;;)
            {
                byte b = 0;
                uint i;
                for (i = 0; i < kBurstSize; i++)
                {
                    if (mMainStream.Read(tempBuffer, 0, 1) == 0)
                        yield break;

                    b = tempBuffer[0];
                    mWritten++; yield return b;
                    if (IsJ(prevByte, b))
                        break;

                    prevByte = b;
                }

                processedBytes += i;
                if (i == kBurstSize)
                    continue;

                if (mStatusDecoder[GetIndex(prevByte, b)].Decode(mRangeDecoder) == 1)
                {
                    var s = (b == 0xE8) ? mCallStream : mJumpStream;

                    uint src = 0;
                    for (i = 0; i < 4; i++)
                    {
                        if (s.Read(tempBuffer, 0, 1) == 0)
                            throw new EndOfStreamException();

                        src <<= 8;
                        src |= tempBuffer[0];
                    }

                    uint dest = src - (uint)(mWritten + 4);
                    mWritten++; yield return (byte)dest;
                    mWritten++; yield return (byte)(dest >> 8);
                    mWritten++; yield return (byte)(dest >> 16);
                    mWritten++; yield return (byte)(dest >> 24);
                    prevByte = (byte)(dest >> 24);
                    processedBytes += 4;
                }
                else
                {
                    prevByte = b;
                }
            }
        }
    }
}
