using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        internal const int kHash2Size = (1 << 10);
        internal const int kHash3Size = (1 << 16);
        internal const int kHash4Size = (1 << 20);

        internal const int kFix3HashSize = (kHash2Size);
        internal const int kFix4HashSize = (kHash2Size + kHash3Size);
        internal const int kFix5HashSize = (kHash2Size + kHash3Size + kHash4Size);
    }
}
