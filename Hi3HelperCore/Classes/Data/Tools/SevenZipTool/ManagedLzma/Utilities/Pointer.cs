using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA
{
    internal static class P
    {
        public static P<T> From<T>(T[] buffer, int offset)
        {
            return new P<T>(buffer, offset);
        }

        public static P<T> From<T>(T[] buffer, uint offset)
        {
            return new P<T>(buffer, offset);
        }

#if BUILD_TESTING
        public static P<byte> From(Testing.PZ p)
        {
            return From(p.Buffer, p.Offset);
        }
#endif
    }

    internal struct P<T>
    {
        public static P<T> Null
        {
            get { return default(P<T>); }
        }

        public readonly T[] mBuffer;
        public readonly int mOffset;

        public P(T[] buffer, int offset = 0)
        {
            mBuffer = buffer;
            mOffset = offset;
        }

        public P(T[] buffer, uint offset)
            : this(buffer, (int)offset)
        {
        }

        public bool IsNull
        {
            get { return mBuffer == null; }
        }

        public T this[int index]
        {
            get { return mBuffer[mOffset + index]; }
            set { mBuffer[mOffset + index] = value; }
        }

        public T this[uint index]
        {
            get { return this[(int)index]; }
            set { this[(int)index] = value; }
        }

        public T this[long index]
        {
            get { return this[checked((int)index)]; }
            set { this[checked((int)index)] = value; }
        }

        public static bool operator <(P<T> left, P<T> right)
        {
            CUtils.Assert(left.mBuffer == right.mBuffer);
            return left.mOffset < right.mOffset;
        }

        public static bool operator <=(P<T> left, P<T> right)
        {
            CUtils.Assert(left.mBuffer == right.mBuffer);
            return left.mOffset <= right.mOffset;
        }

        public static bool operator >(P<T> left, P<T> right)
        {
            CUtils.Assert(left.mBuffer == right.mBuffer);
            return left.mOffset > right.mOffset;
        }

        public static bool operator >=(P<T> left, P<T> right)
        {
            CUtils.Assert(left.mBuffer == right.mBuffer);
            return left.mOffset >= right.mOffset;
        }

        public static int operator -(P<T> left, P<T> right)
        {
            CUtils.Assert(left.mBuffer == right.mBuffer);
            return left.mOffset - right.mOffset;
        }

        public static P<T> operator -(P<T> left, int right)
        {
            return new P<T>(left.mBuffer, left.mOffset - right);
        }

        public static P<T> operator +(P<T> left, int right)
        {
            return new P<T>(left.mBuffer, left.mOffset + right);
        }

        public static P<T> operator +(P<T> left, long right)
        {
            return new P<T>(left.mBuffer, checked((int)(left.mOffset + right)));
        }

        public static P<T> operator +(int left, P<T> right)
        {
            return new P<T>(right.mBuffer, left + right.mOffset);
        }

        public static P<T> operator -(P<T> left, uint right)
        {
            return left - (int)right;
        }

        public static P<T> operator +(P<T> left, uint right)
        {
            return left + (int)right;
        }

        public static P<T> operator +(uint left, P<T> right)
        {
            return (int)left + right;
        }

        public static P<T> operator ++(P<T> self)
        {
            return new P<T>(self.mBuffer, self.mOffset + 1);
        }

        public static P<T> operator --(P<T> self)
        {
            return new P<T>(self.mBuffer, self.mOffset - 1);
        }

        // This allows us to treat null as Pointer<T>.
        public static implicit operator P<T>(T[] buffer)
        {
            return new P<T>(buffer);
        }

        #region Identity

        public override int GetHashCode()
        {
            int hash = mOffset;
            if (mBuffer != null)
                hash += mBuffer.GetHashCode();
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return mBuffer == null;

            // This will invoke the implicit conversion when obj is T[]
            var other = obj as P<T>?;
            return other.HasValue && this == other.Value;
        }

        public bool Equals(P<T> other)
        {
            return mBuffer == other.mBuffer
                && mOffset == other.mOffset;
        }

        public static bool operator ==(P<T> left, P<T> right)
        {
            return left.mBuffer == right.mBuffer
                && left.mOffset == right.mOffset;
        }

        public static bool operator !=(P<T> left, P<T> right)
        {
            return left.mBuffer != right.mBuffer
                || left.mOffset != right.mOffset;
        }

        #endregion
    }
}
