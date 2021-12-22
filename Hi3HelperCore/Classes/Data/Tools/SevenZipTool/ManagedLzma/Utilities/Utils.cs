using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA
{
    internal interface IBufferPool
    {
        P<byte> Allocate(int size);
        void Release(P<byte> buffer);
    }

    internal static class CUtils
    {
        public static void memcpy(P<byte> dst, P<byte> src, long size)
        {
            memcpy(dst, src, checked((int)size));
        }

        public static void memcpy(P<byte> dst, P<byte> src, int size)
        {
            if (dst.mBuffer == src.mBuffer && src.mOffset < dst.mOffset + size && dst.mOffset < src.mOffset + size)
            {
                System.Diagnostics.Debugger.Break();
                throw new InvalidOperationException("memcpy cannot handle overlapping regions correctly");
            }

            Buffer.BlockCopy(src.mBuffer, src.mOffset, dst.mBuffer, dst.mOffset, size);
        }

        public static void memmove(P<byte> dst, P<byte> src, uint size)
        {
            Buffer.BlockCopy(src.mBuffer, src.mOffset, dst.mBuffer, dst.mOffset, checked((int)size));
        }

        public static T[] Init<T>(int sz1, Func<T> init)
        {
            T[] buffer = new T[sz1];
            for (int i = 0; i < sz1; i++)
                buffer[i] = init();
            return buffer;
        }

        public static T[][] Init<T>(int sz1, int sz2)
        {
            T[][] buffer = new T[sz1][];
            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = new T[sz2];
            return buffer;
        }

        [System.Diagnostics.DebuggerHidden]
        public static void Assert(bool expr)
        {
            if (!expr)
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();

                throw new InvalidOperationException("Assertion failed.");
            }
        }
    }
}
