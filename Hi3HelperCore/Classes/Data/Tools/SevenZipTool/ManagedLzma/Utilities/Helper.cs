using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

#if BUILD_TESTING
namespace ManagedLzma.LZMA
{
    using Master;
    using Testing;

    public class Helper : IHelper
    {
        public Helper(Guid id)
        {
            Trace.InitSession(id);
        }

        public void Dispose()
        {
            Trace.StopSession();
        }

        public void LzmaCompress(SharedSettings s)
        {
            switch (s.Variant)
            {
                case 1:
                    {
                        long s_WrittenSize = s.Dst.Length;

                        var props = LZMA.CLzmaEncProps.LzmaEncProps_Init();
                        props.mLevel = s.ActualLevel;
                        props.mDictSize = (uint)s.ActualDictSize;
                        props.mLC = s.ActualLC;
                        props.mLP = s.ActualLP;
                        props.mPB = s.ActualPB;
                        props.mAlgo = s.ActualAlgo;
                        props.mFB = s.ActualFB;
                        props.mBtMode = s.ActualBTMode;
                        props.mNumHashBytes = s.ActualNumHashBytes;
                        props.mMC = s.ActualMC;
                        props.mWriteEndMark = s.ActualWriteEndMark;
                        props.mNumThreads = s.ActualNumThreads;

                        var enc = LZMA.LzmaEnc_Create(LZMA.ISzAlloc.SmallAlloc);
                        var res = enc.LzmaEnc_SetProps(props);
                        if (res != LZMA.SZ_OK)
                            throw new Exception("SetProps failed: " + res);
                        res = enc.LzmaEnc_MemEncode(P.From(s.Dst), ref s_WrittenSize, P.From(s.Src), s.Src.Length,
                            s.ActualWriteEndMark != 0, null, LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);
                        if (res != LZMA.SZ_OK)
                            throw new Exception("MemEncode failed: " + res);

                        s.Enc = new PZ(new byte[LZMA.LZMA_PROPS_SIZE]);
                        long s_Enc_Length = s.Enc.Length;
                        res = enc.LzmaEnc_WriteProperties(P.From(s.Enc), ref s_Enc_Length);
                        if (res != LZMA.SZ_OK)
                            throw new Exception("WriteProperties failed: " + res);
                        if (s.Enc.Length != s.Enc.Buffer.Length)
                            throw new NotSupportedException();

                        enc.LzmaEnc_Destroy(LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);
                        s.WrittenSize = (int)s_WrittenSize;
                        s.Enc.Length = (int)s_Enc_Length;
                    }
                    break;
                default:
                    {
                        long s_WrittenSize = s.Dst.Length;
                        s.Enc = new PZ(new byte[LZMA.LZMA_PROPS_SIZE]);
                        long s_Enc_Length = s.Enc.Length;
                        var res = LZMA.LzmaCompress(
                            P.From(s.Dst), ref s_WrittenSize,
                            P.From(s.Src), s.Src.Length,
                            s.Enc.Buffer, ref s_Enc_Length,
                            s.ActualLevel, s.ActualDictSize, s.ActualLC, s.ActualLP, s.ActualPB, s.ActualFB, s.ActualNumThreads);
                        s.WrittenSize = (int)s_WrittenSize;
                        s.Enc.Length = (int)s_Enc_Length;
                        if (res != LZMA.SZ_OK)
                            throw new Exception("LzmaCompress failed: " + res);
                        if (s.Enc.Length != s.Enc.Buffer.Length)
                            throw new NotSupportedException();
                    }
                    break;
            }
        }

        public void LzmaUncompress(SharedSettings s)
        {
            switch (s.Variant)
            {
                case 1:
                    {
                        var decoder = new LZMA.CLzmaDec();
                        decoder.LzmaDec_Construct();

                        var res = decoder.LzmaDec_Allocate(P.From(s.Enc), checked((uint)s.Enc.Length), LZMA.ISzAlloc.SmallAlloc);
                        if (res != LZMA.SZ_OK)
                            throw new Exception("Allocate failed: " + res);

                        decoder.LzmaDec_Init();

                        P<byte> dstPtr = P.From(s.Dst);
                        long s_WrittenSize = s.Dst.Length;
                        s.WrittenSize = 0;

                        P<byte> srcPtr = P.From(s.Src);
                        long s_UsedSize = s.Src.Length;
                        s.UsedSize = 0;

                        for (;;)
                        {
                            LZMA.ELzmaStatus status;
                            res = decoder.LzmaDec_DecodeToBuf(dstPtr, ref s_WrittenSize, srcPtr, ref s_UsedSize, LZMA.ELzmaFinishMode.LZMA_FINISH_END, out status);
                            if (res != LZMA.SZ_OK)
                                throw new Exception("DecodeToBuf failed: " + res);

                            s.WrittenSize += checked((int)s_WrittenSize);
                            s.UsedSize += checked((int)s_UsedSize);
                            dstPtr += s_WrittenSize;
                            srcPtr += s_UsedSize;
                            s_WrittenSize = dstPtr.mBuffer.Length - dstPtr.mOffset;
                            s_UsedSize = srcPtr.mBuffer.Length - srcPtr.mOffset;

                            if (status == LZMA.ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT
                                || status == LZMA.ELzmaStatus.LZMA_STATUS_NOT_FINISHED)
                                continue;

                            if (status == LZMA.ELzmaStatus.LZMA_STATUS_FINISHED_WITH_MARK)
                            {
                                if (s.ActualWriteEndMark == 0)
                                    throw new Exception("Finished with mark even though we didn't want to write one.");
                                break;
                            }

                            if (status == LZMA.ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK)
                            {
                                if (s.ActualWriteEndMark != 0)
                                    break;
                            }

                            throw new NotSupportedException("Unsupported status case: " + status);
                        }

                        decoder.LzmaDec_Free(LZMA.ISzAlloc.SmallAlloc);

                        s.WrittenSize = (int)dstPtr.mOffset;
                        s.UsedSize = (int)srcPtr.mOffset;
                    }
                    break;
                default:
                    {
                        long s_WrittenSize = s.Dst.Length;
                        long s_UsedSize = s.Src.Length;
                        var res = LZMA.LzmaUncompress(
                            P.From(s.Dst), ref s_WrittenSize,
                            P.From(s.Src), ref s_UsedSize,
                            P.From(s.Enc), s.Enc.Length);
                        if (res != LZMA.SZ_OK)
                            throw new Exception("LzmaUcompress failed: " + res);
                        s.WrittenSize = (int)s_WrittenSize;
                        s.UsedSize = (int)s_UsedSize;
                    }
                    break;
            }
        }
    }

    public class Helper2 : IHelper
    {
        public Helper2(Guid id)
        {
            Trace.InitSession(id);
        }

        public void Dispose()
        {
            Trace.StopSession();
        }

        private sealed class CSeqInStream : LZMA.ISeqInStream
        {
            private SharedSettings s;

            public CSeqInStream(SharedSettings s)
            {
                this.s = s;
            }

            public LZMA.SRes Read(P<byte> buf, ref long size)
            {
                size = Math.Min(size, s.Src.Length);
                int sz = checked((int)size);
                Buffer.BlockCopy(s.Src.Buffer, s.Src.Offset, buf.mBuffer, buf.mOffset, sz);
                s.Src.Offset += sz;
                s.Src.Length -= sz;
                return LZMA.SZ_OK;
            }
        }

        private sealed class CSeqOutStream : LZMA.ISeqOutStream
        {
            private SharedSettings s;

            public CSeqOutStream(SharedSettings s)
            {
                this.s = s;
            }

            public long Write(P<byte> buf, long size)
            {
                size = Math.Min(size, s.Dst.Length);
                int sz = checked((int)size);
                Buffer.BlockCopy(buf.mBuffer, buf.mOffset, s.Dst.Buffer, s.Dst.Offset, sz);
                s.Dst.Offset += sz;
                s.Dst.Length -= sz;
                return size;
            }
        }

        public void LzmaCompress(SharedSettings s)
        {
            int srcBase = s.Src.Offset;
            int dstBase = s.Dst.Offset;

            LZMA.CLzma2EncProps props = new LZMA.CLzma2EncProps();
            props.Lzma2EncProps_Init();

            props.mLzmaProps.mLevel = s.ActualLevel;
            props.mLzmaProps.mDictSize = (uint)s.ActualDictSize;
            props.mLzmaProps.mLC = s.ActualLC;
            props.mLzmaProps.mLP = s.ActualLP;
            props.mLzmaProps.mPB = s.ActualPB;
            props.mLzmaProps.mAlgo = s.ActualAlgo;
            props.mLzmaProps.mFB = s.ActualFB;
            props.mLzmaProps.mBtMode = s.ActualBTMode;
            props.mLzmaProps.mNumHashBytes = s.ActualNumHashBytes;
            props.mLzmaProps.mMC = s.ActualMC;
            props.mLzmaProps.mWriteEndMark = s.ActualWriteEndMark;
            props.mLzmaProps.mNumThreads = s.ActualNumThreads;
            props.mBlockSize = s.ActualBlockSize;
            props.mNumBlockThreads = s.ActualNumBlockThreads;
            props.mNumTotalThreads = s.ActualNumTotalThreads;

            LZMA.ISeqInStream input = new CSeqInStream(s);
            LZMA.ISeqOutStream output = new CSeqOutStream(s);

            var encoder = new LZMA.CLzma2Enc(LZMA.ISzAlloc.SmallAlloc, LZMA.ISzAlloc.BigAlloc);

            var res = encoder.Lzma2Enc_SetProps(props);
            if (res != LZMA.SZ_OK)
                throw new Exception("Lzma2Compress/SetProps failed: " + res);

            res = encoder.Lzma2Enc_Encode(output, input, null);
            if (res != LZMA.SZ_OK)
                throw new Exception("Lzma2Compress/Encode failed: " + res);

            s.WrittenSize = s.Dst.Offset - dstBase;
            s.Enc = new PZ(new[] { encoder.Lzma2Enc_WriteProperties() });

            encoder.Lzma2Enc_Destroy();
            s.Src.Length += s.Src.Offset - srcBase;
            s.Src.Offset = srcBase;
            s.Dst.Length += s.Dst.Offset - dstBase;
            s.Dst.Offset = dstBase;
        }

        public void LzmaUncompress(SharedSettings s)
        {
            if (s.Enc.Length != 1)
                throw new ArgumentException("Settings must contain a single byte.", "propLength");

            if (s.Variant == 1)
            {
                LZMA.CLzma2Dec dec = new LZMA.CLzma2Dec();
                dec.Lzma2Dec_Construct();
                var res = dec.Lzma2Dec_Allocate(s.Enc[0], LZMA.ISzAlloc.SmallAlloc);
                if (res != LZMA.SZ_OK)
                    throw new Exception("Lzma2Dec_Allocate failed: " + res);
                dec.Lzma2Dec_Init();
                for (;;)
                {
                    long s_WrittenSize = s.Dst.Length - s.Dst.Offset;
                    long s_UsedSize = s.Src.Length - s.Src.Offset;

                    LZMA.ELzmaStatus status;
                    res = dec.Lzma2Dec_DecodeToBuf(P.From(s.Dst), ref s_WrittenSize, P.From(s.Src), ref s_UsedSize, LZMA.ELzmaFinishMode.LZMA_FINISH_END, out status);
                    if (res != LZMA.SZ_OK)
                        throw new Exception("Lzma2Dec_DecodeToBuf failed: " + res);

                    s.Dst.Offset += checked((int)s_WrittenSize);
                    s.Src.Offset += checked((int)s_UsedSize);

                    if (status == LZMA.ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT
                        || status == LZMA.ELzmaStatus.LZMA_STATUS_NOT_FINISHED)
                        continue;

                    if (status == LZMA.ELzmaStatus.LZMA_STATUS_FINISHED_WITH_MARK)
                    {
                        if (s.ActualWriteEndMark == 0)
                            throw new Exception("Finished with mark even though we didn't want to write one.");
                        break;
                    }

                    if (status == LZMA.ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK)
                    {
                        if (s.ActualWriteEndMark != 0)
                            break;
                    }

                    throw new NotSupportedException("Unsupported status case: " + status);
                }
                s.WrittenSize = s.Dst.Offset;
                s.Dst.Offset = 0;
                s.UsedSize = s.Src.Offset;
                s.Src.Offset = 0;
            }
            else
            {
                long s_WrittenSize = s.Dst.Length;
                long s_UsedSize = s.Src.Length;

                LZMA.ELzmaStatus status;
                var res = LZMA.CLzma2Dec.Lzma2Decode(
                    P.From(s.Dst), ref s_WrittenSize,
                    P.From(s.Src), ref s_UsedSize,
                    s.Enc[0], LZMA.ELzmaFinishMode.LZMA_FINISH_END, out status,
                    LZMA.ISzAlloc.SmallAlloc);
                if (res != LZMA.SZ_OK)
                    throw new Exception("LzmaUncompress failed: " + res);
                switch (status)
                {
                    case LZMA.ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT:
                        throw new EndOfStreamException();
                    case LZMA.ELzmaStatus.LZMA_STATUS_FINISHED_WITH_MARK:
                        if (s.ActualWriteEndMark == 0)
                            throw new InvalidDataException();
                        break;
                    case LZMA.ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK:
                        if (s.ActualWriteEndMark != 0)
                            throw new InvalidDataException();
                        break;
                    default:
                        throw new Exception(status.ToString());
                }

                s.WrittenSize = checked((int)s_WrittenSize);
                s.UsedSize = checked((int)s_UsedSize);
            }
        }
    }
}
#endif
