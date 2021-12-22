using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ManagedLzma.LZMA;
using LZMA = ManagedLzma.LZMA.Master.LZMA;

namespace master._7zip.Legacy
{
    internal static class DecoderRegistry
    {
        const uint k_Copy = 0x0;
        const uint k_Delta = 3;
        const uint k_LZMA2 = 0x21;
        const uint k_LZMA = 0x030101;
        const uint k_PPMD = 0x030401;
        const uint k_BCJ = 0x03030103;
        const uint k_BCJ2 = 0x0303011B;
        const uint k_Deflate = 0x040108;
        const uint k_BZip2 = 0x040202;

        internal static Stream CreateDecoderStream(CMethodId id, Stream[] inStreams, byte[] info, IPasswordProvider pass, long limit)
        {
            switch (id.Id)
            {
                case k_Copy:
                    if (info != null)
                        throw new NotSupportedException();
                    return inStreams.Single();
                case k_LZMA:
                    return new LzmaDecoderStream(inStreams.Single(), info, limit);
                case k_LZMA2:
                    return new Lzma2DecoderStream(inStreams.Single(), info.Single(), limit);
                case CMethodId.kAESId:
                    return new AesDecoderStream(inStreams.Single(), info, pass, limit);
                case k_BCJ:
                    return new BcjDecoderStream(inStreams.Single(), info, limit);
                case k_BCJ2:
                    return new Bcj2DecoderStream(inStreams, info, limit);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
