using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace master._7zip.Legacy
{
    class BcjDecoderStream : DecoderStream
    {
        private static readonly bool[] kMaskToAllowedStatus = { true, true, true, false, true, false, false, false };
        private static readonly byte[] kMaskToBitNumber = { 0, 1, 2, 2, 3, 3, 3, 3 };

        private Stream mStream;
        private long _bufferPos;
        private uint mState;
        private byte[] mBuffer = new byte[4 << 10];
        private int mOffset;
        private int mEnding;
        private bool mInputEnd;

        public BcjDecoderStream(Stream stream, byte[] info, long limit)
        {
            if (info != null && info.Length != 0)
                throw new NotSupportedException();

            mStream = stream;
        }

        private static bool Test86MSByte(byte b)
        {
            return ((b) == 0 || (b) == 0xFF);
        }

        public override int Read(byte[] buffer, int offset, int count)
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

                int delta = mStream.Read(mBuffer, mEnding, mBuffer.Length - mEnding);
                if (delta == 0)
                    mInputEnd = true;
                else
                    mEnding += delta;
            }

            unsafe
            {
                fixed (byte* pBuffer = mBuffer)
                {
                    int delta = x86_Convert(pBuffer + mOffset, Math.Min(mEnding - mOffset, count), (uint)_bufferPos);
                    if (delta == 0)
                        throw new NotSupportedException();

                    Buffer.BlockCopy(mBuffer, mOffset, buffer, offset, delta);
                    mOffset += delta;

                    _bufferPos += delta;
                    return delta;
                }
            }
        }

        private unsafe int x86_Convert(byte* data, int size, uint ip)
        {
            int bufferPos = 0;
            uint prevMask = mState & 0x7;

            if (size < 5)
                return 0;

            ip += 5;
            int prevPosT = -1;

            for (;;)
            {
                byte* p = data + bufferPos;
                byte* limit = data + size - 4;

                while (p < limit && (*p & 0xFE) != 0xE8)
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
