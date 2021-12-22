using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA2
{
    public sealed class DecoderSettings
    {
        private readonly byte mData;

        public DecoderSettings(byte data)
        {
            mData = data;
        }

        internal byte GetInternalData()
        {
            return mData;
        }
    }
}
