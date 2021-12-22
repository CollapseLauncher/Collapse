using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA
{
    public sealed class DecoderSettings
    {
        private const uint LZMA_DIC_MIN = 1 << 12;
        private const int LZMA_PROPS_SIZE = 5;

        public static DecoderSettings ReadFrom(byte[] data, int offset)
        {
            byte bt = data[offset];
            byte lc = (byte)(bt % 9);
            bt /= 9;
            byte pb = (byte)(bt / 5);
            byte lp = (byte)(bt % 5);
            uint dictSize = data[offset + 1];
            dictSize |= (uint)data[offset + 2] << 8;
            dictSize |= (uint)data[offset + 3] << 16;
            dictSize |= (uint)data[offset + 4] << 24;
            return new DecoderSettings(dictSize, lc, pb, lp);
        }

        public static DecoderSettings CreateFrom(ImmutableArray<byte> data)
        {
            return ReadFrom(data.ToArray(), 0);
        }

        internal readonly uint mDictSize;
        internal readonly byte mLC;
        internal readonly byte mPB;
        internal readonly byte mLP;

        internal DecoderSettings(uint dictSize, byte lc, byte pb, byte lp)
        {
            if ((mDictSize = dictSize) < LZMA_DIC_MIN)
                mDictSize = LZMA_DIC_MIN;

            if ((mLC = lc) > 8)
                throw new ArgumentOutOfRangeException(nameof(lc));

            if ((mPB = pb) > 4)
                throw new ArgumentOutOfRangeException(nameof(pb));

            if ((mLP = lp) > 4)
                throw new ArgumentOutOfRangeException(nameof(lp));
        }

        public void WriteTo(byte[] buffer, int offset)
        {
            buffer[offset] = (byte)((mPB * 5 + mLP) * 9 + mLC);
            buffer[offset + 1] = (byte)mDictSize;
            buffer[offset + 2] = (byte)(mDictSize >> 8);
            buffer[offset + 3] = (byte)(mDictSize >> 16);
            buffer[offset + 4] = (byte)(mDictSize >> 24);
        }

        public byte[] ToArray()
        {
            var buffer = new byte[LZMA_PROPS_SIZE];
            WriteTo(buffer, 0);
            return buffer;
        }
    }
}
