using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA2
{
    public sealed class EncoderSettings
    {
        private LZMA.EncoderSettings mBaseSettings;
        private long? mBlockSize;
        private int? mBlockThreadCount;
        private int? mTotalThreadCount;

        public EncoderSettings()
        {
            mBaseSettings = new LZMA.EncoderSettings();
        }

        internal LZMA.Master.LZMA.CLzma2EncProps GetInternalSettings()
        {
            var result = new LZMA.Master.LZMA.CLzma2EncProps();
            result.mLzmaProps = mBaseSettings.GetInternalSettings();
            result.mNumTotalThreads = mTotalThreadCount ?? -1;
            result.mNumBlockThreads = mBlockThreadCount ?? -1;
            result.mBlockSize = mBlockSize ?? 0;
            return result;
        }
    }
}
