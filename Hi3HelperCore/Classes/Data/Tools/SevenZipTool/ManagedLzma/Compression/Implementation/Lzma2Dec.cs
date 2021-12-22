using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        public sealed class CLzma2Dec
        {
            #region Constants

            /*
            00000000  -  EOS
            00000001 U U  -  Uncompressed Reset Dic
            00000010 U U  -  Uncompressed No Reset
            100uuuuu U U P P  -  LZMA no reset
            101uuuuu U U P P  -  LZMA reset state
            110uuuuu U U P P S  -  LZMA reset state + new prop
            111uuuuu U U P P S  -  LZMA reset state + new prop + reset dic

              u, U - Unpack Size
              P - Pack Size
              S - Props
            */

            private const int LZMA2_CONTROL_LZMA = (1 << 7);
            private const int LZMA2_CONTROL_COPY_NO_RESET = 2;
            private const int LZMA2_CONTROL_COPY_RESET_DIC = 1;
            private const int LZMA2_CONTROL_EOF = 0;

            private const int LZMA2_LCLP_MAX = 4;

            private enum Lzma2State
            {
                Control,
                Unpack0,
                Unpack1,
                Pack0,
                Pack1,
                Prop,
                Data,
                DataCont,
                Finished,
                Error,
            }

            #endregion

            #region Variables

            internal CLzmaDec mDecoder = new CLzmaDec();
            internal uint mPackSize;
            internal uint mUnpackSize;
            private Lzma2State mState;
            private byte mControl;
            private bool mNeedInitDic;
            private bool mNeedInitState;
            private bool mNeedInitProp;

            #endregion

            #region Private Methods

            private bool IsUncompressedState()
            {
                return (mControl & LZMA2_CONTROL_LZMA) == 0;
            }

            private int GetLzmaMode()
            {
                return (mControl >> 5) & 3;
            }

            private static bool IsThereProp(int mode)
            {
                return mode >= 2;
            }

            private static uint DicSizeFromProp(byte p)
            {
                return (2u | (p & 1u)) << ((p >> 1) + 11);
            }

            private static SRes Lzma2Dec_GetOldProps(byte prop, P<byte> props)
            {
                if (prop > 40)
                    return SZ_ERROR_UNSUPPORTED;

                uint dicSize = (prop == 40) ? 0xFFFFFFFF : DicSizeFromProp(prop);
                props[0] = (byte)LZMA2_LCLP_MAX;
                props[1] = (byte)(dicSize);
                props[2] = (byte)(dicSize >> 8);
                props[3] = (byte)(dicSize >> 16);
                props[4] = (byte)(dicSize >> 24);
                return SZ_OK;
            }

            private Lzma2State Lzma2Dec_UpdateState(byte b)
            {
                switch (mState)
                {
                    case Lzma2State.Control:
                        mControl = b;
                        TR("Lzma2Dec_UpdateState:1", checked((int)mDecoder.mDicPos));
                        TR("Lzma2Dec_UpdateState:2", b);
                        DebugPrint("\n {0:X4} ", mDecoder.mDicPos);
                        DebugPrint(" {0:X2}", b);

                        if (mControl == 0)
                            return Lzma2State.Finished;

                        if (IsUncompressedState())
                        {
                            if ((mControl & 0x7F) > 2)
                                return Lzma2State.Error;

                            mUnpackSize = 0;
                        }
                        else
                        {
                            mUnpackSize = (uint)(mControl & 0x1F) << 16;
                        }

                        return Lzma2State.Unpack0;

                    case Lzma2State.Unpack0:
                        mUnpackSize |= (uint)b << 8;
                        return Lzma2State.Unpack1;

                    case Lzma2State.Unpack1:
                        mUnpackSize |= (uint)b;
                        mUnpackSize++;

                        TR("Lzma2Dec_UpdateState:3", mUnpackSize);
                        DebugPrint(" {0:00000000}", mUnpackSize);

                        if (IsUncompressedState())
                            return Lzma2State.Data;
                        else
                            return Lzma2State.Pack0;

                    case Lzma2State.Pack0:
                        mPackSize = (uint)b << 8;
                        return Lzma2State.Pack1;

                    case Lzma2State.Pack1:
                        mPackSize |= (uint)b;
                        mPackSize++;

                        TR("Lzma2Dec_UpdateState:4", mPackSize);
                        DebugPrint(" {0:00000000}", mPackSize);

                        if (IsThereProp(GetLzmaMode()))
                            return Lzma2State.Prop;
                        else if (mNeedInitProp)
                            return Lzma2State.Error;
                        else
                            return Lzma2State.Data;

                    case Lzma2State.Prop:
                        if (b >= (9 * 5 * 5))
                            return Lzma2State.Error;

                        int lc = b % 9;
                        b /= 9;
                        mDecoder.mProp.mPB = b / 5;
                        int lp = b % 5;

                        if (lc + lp > LZMA2_LCLP_MAX)
                            return Lzma2State.Error;

                        mDecoder.mProp.mLC = lc;
                        mDecoder.mProp.mLP = lp;
                        mNeedInitProp = false;
                        return Lzma2State.Data;

                    default:
                        return Lzma2State.Error;
                }
            }

            private static void LzmaDec_UpdateWithUncompressed(CLzmaDec p, P<byte> src, long size)
            {
                CUtils.memcpy(p.mDic + p.mDicPos, src, size);
                p.mDicPos += size;
                if (p.mCheckDicSize == 0 && p.mProp.mDicSize - p.mProcessedPos <= size)
                    p.mCheckDicSize = p.mProp.mDicSize;
                p.mProcessedPos += (uint)size;
            }

            #endregion

            #region Header

            public void Lzma2Dec_Construct()
            {
                mDecoder.LzmaDec_Construct();
            }

            public void Lzma2Dec_FreeProbs(ISzAlloc alloc)
            {
                mDecoder.LzmaDec_FreeProbs(alloc);
            }

            public void Lzma2Dec_Free(ISzAlloc alloc)
            {
                mDecoder.LzmaDec_Free(alloc);
            }

            public SRes Lzma2Dec_AllocateProbs(byte prop, ISzAlloc alloc)
            {
                byte[] props = new byte[LZMA_PROPS_SIZE];

                SRes res;
                if ((res = Lzma2Dec_GetOldProps(prop, props)) != SZ_OK)
                    return res;

                return mDecoder.LzmaDec_AllocateProbs(props, LZMA_PROPS_SIZE, alloc);
            }

            public SRes Lzma2Dec_Allocate(byte prop, ISzAlloc alloc)
            {
                byte[] props = new byte[LZMA_PROPS_SIZE];

                SRes res;
                if ((res = Lzma2Dec_GetOldProps(prop, props)) != SZ_OK)
                    return res;

                return mDecoder.LzmaDec_Allocate(props, LZMA_PROPS_SIZE, alloc);
            }

            public void Lzma2Dec_Init()
            {
                mState = Lzma2State.Control;
                mNeedInitDic = true;
                mNeedInitState = true;
                mNeedInitProp = true;
                mDecoder.LzmaDec_Init();
            }

            /*
            finishMode:
              It has meaning only if the decoding reaches output limit (*destLen or dicLimit).
              LZMA_FINISH_ANY - use smallest number of input bytes
              LZMA_FINISH_END - read EndOfStream marker after decoding

            Returns:
              SZ_OK
                status:
                  LZMA_STATUS_FINISHED_WITH_MARK
                  LZMA_STATUS_NOT_FINISHED
                  LZMA_STATUS_NEEDS_MORE_INPUT
              SZ_ERROR_DATA - Data error
            */

            public SRes Lzma2Dec_DecodeToDic(long dicLimit, P<byte> src, ref long srcLen, ELzmaFinishMode finishMode, out ELzmaStatus status)
            {
                long inSize = srcLen;
                srcLen = 0;
                status = ELzmaStatus.LZMA_STATUS_NOT_SPECIFIED;

                while (mState != Lzma2State.Finished)
                {
                    long dicPos = mDecoder.mDicPos;
                    if (mState == Lzma2State.Error)
                        return SZ_ERROR_DATA;

                    if (dicPos == dicLimit && finishMode == ELzmaFinishMode.LZMA_FINISH_ANY)
                    {
                        status = ELzmaStatus.LZMA_STATUS_NOT_FINISHED;
                        return SZ_OK;
                    }

                    if (mState != Lzma2State.Data && mState != Lzma2State.DataCont)
                    {
                        if (srcLen == inSize)
                        {
                            status = ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT;
                            return SZ_OK;
                        }
                        srcLen++;
                        mState = Lzma2Dec_UpdateState(src[0]);
                        src++;
                        continue;
                    }

                    long destSizeCur = dicLimit - dicPos;
                    long srcSizeCur = inSize - srcLen;
                    ELzmaFinishMode curFinishMode = ELzmaFinishMode.LZMA_FINISH_ANY;

                    if (mUnpackSize <= destSizeCur)
                    {
                        destSizeCur = mUnpackSize;
                        curFinishMode = ELzmaFinishMode.LZMA_FINISH_END;
                    }

                    if (IsUncompressedState())
                    {
                        if (srcLen == inSize)
                        {
                            status = ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT;
                            return SZ_OK;
                        }

                        if (mState == Lzma2State.Data)
                        {
                            bool initDic = (mControl == LZMA2_CONTROL_COPY_RESET_DIC);
                            if (initDic)
                                mNeedInitProp = mNeedInitState = true;
                            else if (mNeedInitDic)
                                return SZ_ERROR_DATA;

                            mNeedInitDic = false;
                            mDecoder.LzmaDec_InitDicAndState(initDic, false);
                        }

                        if (srcSizeCur > destSizeCur)
                            srcSizeCur = destSizeCur;

                        if (srcSizeCur == 0)
                            return SZ_ERROR_DATA;

                        LzmaDec_UpdateWithUncompressed(mDecoder, src, srcSizeCur);

                        src += srcSizeCur;
                        srcLen += srcSizeCur;
                        mUnpackSize -= (uint)srcSizeCur;
                        mState = (mUnpackSize == 0) ? Lzma2State.Control : Lzma2State.DataCont;
                    }
                    else
                    {
                        long outSizeProcessed;

                        if (mState == Lzma2State.Data)
                        {
                            int mode = GetLzmaMode();
                            bool initDic = (mode == 3);
                            bool initState = (mode > 0);
                            if ((!initDic && mNeedInitDic) || (!initState && mNeedInitState))
                                return SZ_ERROR_DATA;

                            mDecoder.LzmaDec_InitDicAndState(initDic, initState);
                            mNeedInitDic = false;
                            mNeedInitState = false;
                            mState = Lzma2State.DataCont;
                        }

                        if (srcSizeCur > mPackSize)
                            srcSizeCur = mPackSize;

                        SRes res = mDecoder.LzmaDec_DecodeToDic(dicPos + destSizeCur, src, ref srcSizeCur, curFinishMode, out status);

                        src += srcSizeCur;
                        srcLen += srcSizeCur;
                        mPackSize -= (uint)srcSizeCur;

                        outSizeProcessed = mDecoder.mDicPos - dicPos;
                        mUnpackSize -= (uint)outSizeProcessed;

                        if (res != SZ_OK)
                            return res;

                        if (status == ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT)
                            return res;

                        if (srcSizeCur == 0 && outSizeProcessed == 0)
                        {
                            if (status != ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK || mUnpackSize != 0 || mPackSize != 0)
                                return SZ_ERROR_DATA;

                            mState = Lzma2State.Control;
                        }

                        if (status == ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK)
                            status = ELzmaStatus.LZMA_STATUS_NOT_FINISHED;
                    }
                }

                status = ELzmaStatus.LZMA_STATUS_FINISHED_WITH_MARK;
                return SZ_OK;
            }

            public SRes Lzma2Dec_DecodeToBuf(P<byte> dest, ref long destLen, P<byte> src, ref long srcLen, ELzmaFinishMode finishMode, out ELzmaStatus status)
            {
                long outSize = destLen;
                long inSize = srcLen;
                srcLen = 0;
                destLen = 0;

                for (;;)
                {
                    if (mDecoder.mDicPos == mDecoder.mDicBufSize)
                        mDecoder.mDicPos = 0;

                    long outSizeCur;
                    ELzmaFinishMode curFinishMode;
                    long dicPos = mDecoder.mDicPos;
                    if (outSize > mDecoder.mDicBufSize - dicPos)
                    {
                        outSizeCur = mDecoder.mDicBufSize;
                        curFinishMode = ELzmaFinishMode.LZMA_FINISH_ANY;
                    }
                    else
                    {
                        outSizeCur = dicPos + outSize;
                        curFinishMode = finishMode;
                    }

                    long srcSizeCur = inSize;
                    SRes res = Lzma2Dec_DecodeToDic(outSizeCur, src, ref srcSizeCur, curFinishMode, out status);
                    src += srcSizeCur;
                    inSize -= srcSizeCur;
                    srcLen += srcSizeCur;
                    outSizeCur = mDecoder.mDicPos - dicPos;
                    CUtils.memcpy(dest, mDecoder.mDic + dicPos, outSizeCur);
                    dest += outSizeCur;
                    outSize -= outSizeCur;
                    destLen += outSizeCur;

                    if (res != 0)
                        return res;

                    if (outSizeCur == 0 || outSize == 0)
                        return SZ_OK;
                }
            }

            #endregion

            #region Public Static Methods

            /*
            finishMode:
              It has meaning only if the decoding reaches output limit (*destLen).
              LZMA_FINISH_ANY - use smallest number of input bytes
              LZMA_FINISH_END - read EndOfStream marker after decoding

            Returns:
              SZ_OK
                status:
                  LZMA_STATUS_FINISHED_WITH_MARK
                  LZMA_STATUS_NOT_FINISHED
              SZ_ERROR_DATA - Data error
              SZ_ERROR_MEM  - Memory allocation error
              SZ_ERROR_UNSUPPORTED - Unsupported properties
              SZ_ERROR_INPUT_EOF - It needs more bytes in input buffer (src).
            */

            public static SRes Lzma2Decode(P<byte> dest, ref long destLen, P<byte> src, ref long srcLen, byte prop, ELzmaFinishMode finishMode, out ELzmaStatus status, ISzAlloc alloc)
            {
                long outSize = destLen;
                long inSize = srcLen;
                destLen = 0;
                srcLen = 0;
                status = ELzmaStatus.LZMA_STATUS_NOT_SPECIFIED;

                CLzma2Dec p = new CLzma2Dec();
                p.Lzma2Dec_Construct();
                SRes res;
                if ((res = p.Lzma2Dec_AllocateProbs(prop, alloc)) != SZ_OK)
                    return res;
                p.mDecoder.mDic = dest;
                p.mDecoder.mDicBufSize = outSize;
                p.Lzma2Dec_Init();
                srcLen = inSize;
                res = p.Lzma2Dec_DecodeToDic(outSize, src, ref srcLen, finishMode, out status);
                destLen = p.mDecoder.mDicPos;
                if (res == SZ_OK && status == ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT)
                    res = SZ_ERROR_INPUT_EOF;
                p.Lzma2Dec_FreeProbs(alloc);
                return res;
            }

            #endregion
        }
    }
}
