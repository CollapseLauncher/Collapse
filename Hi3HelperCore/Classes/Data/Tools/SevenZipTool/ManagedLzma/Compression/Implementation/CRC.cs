using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA.Master
{
    internal static class CRCUtils
    {
        private static uint[] mTable;

        static CRCUtils()
        {
            const uint kCrcPoly = 0xEDB88320;

            mTable = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint r = i;
                for (int j = 0; j < 8; j++)
                    r = (r >> 1) ^ (kCrcPoly & ~((r & 1) - 1));

                mTable[i] = r;
            }
        }

        public static uint CRC(this byte bt)
        {
            return mTable[bt];
        }
    }
}
