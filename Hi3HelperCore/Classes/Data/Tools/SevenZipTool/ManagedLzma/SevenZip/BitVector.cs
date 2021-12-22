using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.SevenZip.Reader
{
    internal struct BitVector
    {
        private byte[] mVector;
        private int mCount;
        private bool mValue;

        public BitVector(int count, bool value)
        {
            mVector = null;
            mCount = count;
            mValue = value;
        }

        public BitVector(int count, byte[] vector)
        {
            mVector = vector;
            mCount = count;
            mValue = false;
        }

        public int Count => mCount;

        public bool this[int index]
        {
            get
            {
                System.Diagnostics.Debug.Assert(0 <= index && index < mCount);

                if (mVector == null)
                    return mValue;

                // bits go high to low in the 7z format
                var bits = mVector[index >> 3];
                var mask = 0x80 >> (index & 7);
                return (bits & mask) != 0;
            }
        }

        public int CountSetBits()
        {
            if (mVector == null)
                return mValue ? mCount : 0;

            int bits = 0;

            // TODO: optimize
            for (int i = 0; i < mCount; i++)
                if (this[i])
                    bits++;

            return bits;
        }
    }
}
