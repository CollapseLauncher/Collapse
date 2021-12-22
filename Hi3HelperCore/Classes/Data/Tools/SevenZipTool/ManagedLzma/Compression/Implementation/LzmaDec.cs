using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        /* ---------- LZMA Properties ---------- */

        public sealed class CLzmaProps
        {
            private const int LZMA_DIC_MIN = (1 << 12);

            #region Variables

            public int mLC;
            public int mLP;
            public int mPB;
            public uint mDicSize;

            #endregion

            #region Methods

            public CLzmaProps() { }
            public CLzmaProps(CLzmaProps other)
            {
                this.mLC = other.mLC;
                this.mLP = other.mLP;
                this.mPB = other.mPB;
                this.mDicSize = other.mDicSize;
            }

            /* LzmaProps_Decode - decodes properties
            Returns:
              SZ_OK
              SZ_ERROR_UNSUPPORTED - Unsupported properties
            */

            public SRes LzmaProps_Decode(P<byte> data, uint size)
            {
                if (size < LZMA_PROPS_SIZE)
                    return SZ_ERROR_UNSUPPORTED;

                uint dicSize = data[1] | ((uint)data[2] << 8) | ((uint)data[3] << 16) | ((uint)data[4] << 24);
                if (dicSize < LZMA_DIC_MIN)
                    dicSize = LZMA_DIC_MIN;
                mDicSize = dicSize;

                byte d = data[0];
                if (d >= (9 * 5 * 5))
                    return SZ_ERROR_UNSUPPORTED;

                mLC = d % 9;
                d /= 9;
                mPB = d / 5;
                mLP = d % 5;

                return SZ_OK;
            }

            #endregion
        }


        /* ---------- LZMA Decoder state ---------- */

        /* LZMA_REQUIRED_INPUT_MAX = number of required input bytes for worst case.
           Num bits = log2((2^11 / 31) ^ 22) + 26 < 134 + 26 = 160; */

        public const int LZMA_REQUIRED_INPUT_MAX = 20;

        public sealed class CLzmaDec
        {
            #region Constants

            private enum ELzmaDummy
            {
                DUMMY_ERROR, /* unexpected end of input stream */
                DUMMY_LIT,
                DUMMY_MATCH,
                DUMMY_REP,
            }

            private const int kNumTopBits = 24;
            private const uint kTopValue = (1u << kNumTopBits);

            private const int kNumBitModelTotalBits = 11;
            private const int kBitModelTotal = (1 << kNumBitModelTotalBits);
            private const int kNumMoveBits = 5;

            private const int RC_INIT_SIZE = 5;

            private const int kNumPosBitsMax = 4;
            private const int kNumPosStatesMax = (1 << kNumPosBitsMax);

            private const int kLenNumLowBits = 3;
            private const int kLenNumLowSymbols = (1 << kLenNumLowBits);
            private const int kLenNumMidBits = 3;
            private const int kLenNumMidSymbols = (1 << kLenNumMidBits);
            private const int kLenNumHighBits = 8;
            private const int kLenNumHighSymbols = (1 << kLenNumHighBits);

            private const int kLenChoice = 0;
            private const int kLenChoice2 = (kLenChoice + 1);
            private const int kLenLow = (kLenChoice2 + 1);
            private const int kLenMid = (kLenLow + (kNumPosStatesMax << kLenNumLowBits));
            private const int kLenHigh = (kLenMid + (kNumPosStatesMax << kLenNumMidBits));
            private const int kNumLenProbs = (kLenHigh + kLenNumHighSymbols);


            private const int kNumStates = 12;
            private const uint kNumLitStates = 7;

            private const int kStartPosModelIndex = 4;
            private const int kEndPosModelIndex = 14;
            private const int kNumFullDistances = (1 << (kEndPosModelIndex >> 1));

            private const int kNumPosSlotBits = 6;
            private const int kNumLenToPosStates = 4;

            private const int kNumAlignBits = 4;
            private const int kAlignTableSize = (1 << kNumAlignBits);

            private const int kMatchMinLen = 2;
            private const int kMatchSpecLenStart = (kMatchMinLen + kLenNumLowSymbols + kLenNumMidSymbols + kLenNumHighSymbols);

            private const int kIsMatch = 0;
            private const int kIsRep = (kIsMatch + (kNumStates << kNumPosBitsMax));
            private const int kIsRepG0 = (kIsRep + kNumStates);
            private const int kIsRepG1 = (kIsRepG0 + kNumStates);
            private const int kIsRepG2 = (kIsRepG1 + kNumStates);
            private const int kIsRep0Long = (kIsRepG2 + kNumStates);
            private const int kPosSlot = (kIsRep0Long + (kNumStates << kNumPosBitsMax));
            private const int kSpecPos = (kPosSlot + (kNumLenToPosStates << kNumPosSlotBits));
            private const int kAlign = (kSpecPos + kNumFullDistances - kEndPosModelIndex);
            private const int kLenCoder = (kAlign + kAlignTableSize);
            private const int kRepLenCoder = (kLenCoder + kNumLenProbs);
            private const int kLiteral = (kRepLenCoder + kNumLenProbs);

            private const uint LZMA_BASE_SIZE = 1846;
            private const uint LZMA_LIT_SIZE = 768;

            #endregion

            #region Variables

            internal CLzmaProps mProp = new CLzmaProps();
            internal ushort[] mProbs;
            internal P<byte> mDic;
            internal P<byte> mBuf;
            internal uint mRange, mCode;
            internal long mDicPos;
            internal long mDicBufSize;
            internal uint mProcessedPos;
            internal uint mCheckDicSize;
            internal uint mState;
            internal uint[] mReps = new uint[4];
            internal uint mRemainLen;
            internal bool mNeedFlush;
            internal bool mNeedInitState;
            internal uint mNumProbs;
            internal uint mTempBufSize;
            internal byte[] mTempBuf = new byte[LZMA_REQUIRED_INPUT_MAX];

            #endregion

            #region Public Methods

            public void LzmaDec_Construct()
            {
                //#if Literal != LZMA_BASE_SIZE
                //StopCompilingDueBUG
                //#endif

                mDic = null;
                mProbs = null;
            }

            public void LzmaDec_Init()
            {
                mDicPos = 0;
                LzmaDec_InitDicAndState(true, true);
            }

            /* ---------- Interfaces ---------- */

            /* There are 3 levels of interfaces:
                 1) Dictionary Interface
                 2) Buffer Interface
                 3) One Call Interface
               You can select any of these interfaces, but don't mix functions from different
               groups for same object. */


            /* There are two variants to allocate state for Dictionary Interface:
                 1) LzmaDec_Allocate / LzmaDec_Free
                 2) LzmaDec_AllocateProbs / LzmaDec_FreeProbs
               You can use variant 2, if you set dictionary buffer manually.
               For Buffer Interface you must always use variant 1.

            LzmaDec_Allocate* can return:
              SZ_OK
              SZ_ERROR_MEM         - Memory allocation error
              SZ_ERROR_UNSUPPORTED - Unsupported properties
            */

            public SRes LzmaDec_AllocateProbs(P<byte> props, uint propsSize, ISzAlloc alloc)
            {
                SRes res;
                CLzmaProps propNew = new CLzmaProps();
                if ((res = propNew.LzmaProps_Decode(props, propsSize)) != SZ_OK) return res;
                if ((res = LzmaDec_AllocateProbs2(propNew, alloc)) != SZ_OK) return res;
                mProp = new CLzmaProps(propNew);
                return SZ_OK;
            }

            public void LzmaDec_FreeProbs(ISzAlloc alloc)
            {
                alloc.FreeUInt16(alloc, mProbs);
                mProbs = null;
            }

            public SRes LzmaDec_Allocate(P<byte> props, uint propsSize, ISzAlloc alloc)
            {
                CLzmaProps propNew = new CLzmaProps();

                SRes res;
                if ((res = propNew.LzmaProps_Decode(props, propsSize)) != SZ_OK)
                    return res;

                if ((res = LzmaDec_AllocateProbs2(propNew, alloc)) != SZ_OK)
                    return res;

                long dicBufSize = propNew.mDicSize;
                if (mDic == null || dicBufSize != mDicBufSize)
                {
                    LzmaDec_FreeDict(alloc);
                    mDic = alloc.AllocBytes(alloc, dicBufSize);
                    if (mDic == null)
                    {
                        LzmaDec_FreeProbs(alloc);
                        return SZ_ERROR_MEM;
                    }
                }

                mDicBufSize = dicBufSize;
                mProp = new CLzmaProps(propNew);
                return SZ_OK;
            }

            public void LzmaDec_Free(ISzAlloc alloc)
            {
                LzmaDec_FreeProbs(alloc);
                LzmaDec_FreeDict(alloc);
            }

            /* ---------- Dictionary Interface ---------- */

            /* You can use it, if you want to eliminate the overhead for data copying from
               dictionary to some other external buffer.
               You must work with CLzmaDec variables directly in this interface.

               STEPS:
                 LzmaDec_Constr()
                 LzmaDec_Allocate()
                 for (each new stream)
                 {
                   LzmaDec_Init()
                   while (it needs more decompression)
                   {
                     LzmaDec_DecodeToDic()
                     use data from CLzmaDec::dic and update CLzmaDec::dicPos
                   }
                 }
                 LzmaDec_Free()
            */

            /* LzmaDec_DecodeToDic
   
               The decoding to internal dictionary buffer (CLzmaDec::dic).
               You must manually update CLzmaDec::dicPos, if it reaches CLzmaDec::dicBufSize !!!

            finishMode:
              It has meaning only if the decoding reaches output limit (dicLimit).
              LZMA_FINISH_ANY - Decode just dicLimit bytes.
              LZMA_FINISH_END - Stream must be finished after dicLimit.

            Returns:
              SZ_OK
                status:
                  LZMA_STATUS_FINISHED_WITH_MARK
                  LZMA_STATUS_NOT_FINISHED
                  LZMA_STATUS_NEEDS_MORE_INPUT
                  LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK
              SZ_ERROR_DATA - Data error
            */

            public SRes LzmaDec_DecodeToDic(long dicLimit, P<byte> src, ref long srcLen, ELzmaFinishMode finishMode, out ELzmaStatus status)
            {
                long inSize = srcLen;
                srcLen = 0;

                LzmaDec_WriteRem(dicLimit);

                status = ELzmaStatus.LZMA_STATUS_NOT_SPECIFIED;

                while (mRemainLen != kMatchSpecLenStart)
                {
                    if (mNeedFlush)
                    {
                        while (inSize > 0 && mTempBufSize < RC_INIT_SIZE)
                        {
                            mTempBuf[mTempBufSize] = src[0];
                            mTempBufSize++;
                            src++;
                            srcLen++;
                            inSize--;
                        }

                        if (mTempBufSize < RC_INIT_SIZE)
                        {
                            status = ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT;
                            return SZ_OK;
                        }

                        if (mTempBuf[0] != 0)
                            return SZ_ERROR_DATA;

                        LzmaDec_InitRc(mTempBuf);
                        mTempBufSize = 0;
                    }

                    bool checkEndMarkNow = false;

                    if (mDicPos >= dicLimit)
                    {
                        if (mRemainLen == 0 && mCode == 0)
                        {
                            status = ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK;
                            return SZ_OK;
                        }

                        if (finishMode == ELzmaFinishMode.LZMA_FINISH_ANY)
                        {
                            status = ELzmaStatus.LZMA_STATUS_NOT_FINISHED;
                            return SZ_OK;
                        }

                        if (mRemainLen != 0)
                        {
                            status = ELzmaStatus.LZMA_STATUS_NOT_FINISHED;
                            return SZ_ERROR_DATA;
                        }

                        checkEndMarkNow = true;
                    }

                    if (mNeedInitState)
                        LzmaDec_InitStateReal();

                    if (mTempBufSize == 0)
                    {
                        P<byte> bufLimit;
                        if (inSize < LZMA_REQUIRED_INPUT_MAX || checkEndMarkNow)
                        {
                            ELzmaDummy dummyRes = LzmaDec_TryDummy(src, inSize);

                            if (dummyRes == ELzmaDummy.DUMMY_ERROR)
                            {
                                CUtils.memcpy(mTempBuf, src, inSize);
                                mTempBufSize = (uint)inSize;
                                srcLen += inSize;
                                status = ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT;
                                return SZ_OK;
                            }

                            if (checkEndMarkNow && dummyRes != ELzmaDummy.DUMMY_MATCH)
                            {
                                status = ELzmaStatus.LZMA_STATUS_NOT_FINISHED;
                                return SZ_ERROR_DATA;
                            }

                            bufLimit = src;
                        }
                        else
                        {
                            bufLimit = src + inSize - LZMA_REQUIRED_INPUT_MAX;
                        }

                        mBuf = src;

                        if (LzmaDec_DecodeReal2(dicLimit, bufLimit) != 0)
                            return SZ_ERROR_DATA;

                        long processed = mBuf - src;
                        srcLen += processed;
                        src += processed;
                        inSize -= processed;
                    }
                    else
                    {
                        uint rem = mTempBufSize;
                        uint lookAhead = 0;

                        while (rem < LZMA_REQUIRED_INPUT_MAX && lookAhead < inSize)
                            mTempBuf[rem++] = src[lookAhead++];

                        mTempBufSize = rem;

                        if (rem < LZMA_REQUIRED_INPUT_MAX || checkEndMarkNow)
                        {
                            ELzmaDummy dummyRes = LzmaDec_TryDummy(mTempBuf, rem);

                            if (dummyRes == ELzmaDummy.DUMMY_ERROR)
                            {
                                srcLen += lookAhead;
                                status = ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT;
                                return SZ_OK;
                            }

                            if (checkEndMarkNow && dummyRes != ELzmaDummy.DUMMY_MATCH)
                            {
                                status = ELzmaStatus.LZMA_STATUS_NOT_FINISHED;
                                return SZ_ERROR_DATA;
                            }
                        }

                        mBuf = mTempBuf;

                        if (LzmaDec_DecodeReal2(dicLimit, mBuf) != 0)
                            return SZ_ERROR_DATA;

                        lookAhead -= rem - (uint)(mBuf - mTempBuf);
                        srcLen += lookAhead;
                        src += lookAhead;
                        inSize -= lookAhead;
                        mTempBufSize = 0;
                    }
                }

                if (mCode != 0)
                    return SZ_ERROR_DATA;

                status = ELzmaStatus.LZMA_STATUS_FINISHED_WITH_MARK;
                return SZ_OK;
            }


            /* ---------- Buffer Interface ---------- */

            /* It's zlib-like interface.
               See LzmaDec_DecodeToDic description for information about STEPS and return results,
               but you must use LzmaDec_DecodeToBuf instead of LzmaDec_DecodeToDic and you don't need
               to work with CLzmaDec variables manually.

            finishMode:
              It has meaning only if the decoding reaches output limit (*destLen).
              LZMA_FINISH_ANY - Decode just destLen bytes.
              LZMA_FINISH_END - Stream must be finished after (*destLen).
            */

            public SRes LzmaDec_DecodeToBuf(P<byte> dest, ref long destLen, P<byte> src, ref long srcLen, ELzmaFinishMode finishMode, out ELzmaStatus status)
            {
                long outSize = destLen;
                long inSize = srcLen;
                srcLen = destLen = 0;
                for (;;)
                {
                    long inSizeCur = inSize;

                    if (mDicPos == mDicBufSize)
                        mDicPos = 0;
                    long dicPos = mDicPos;

                    long outSizeCur;
                    ELzmaFinishMode curFinishMode;
                    if (outSize > mDicBufSize - dicPos)
                    {
                        outSizeCur = mDicBufSize;
                        curFinishMode = ELzmaFinishMode.LZMA_FINISH_ANY;
                    }
                    else
                    {
                        outSizeCur = dicPos + outSize;
                        curFinishMode = finishMode;
                    }

                    SRes res = LzmaDec_DecodeToDic(outSizeCur, src, ref inSizeCur, curFinishMode, out status);
                    src += inSizeCur;
                    inSize -= inSizeCur;
                    srcLen += inSizeCur;
                    outSizeCur = mDicPos - dicPos;
                    CUtils.memcpy(dest, mDic + dicPos, outSizeCur);
                    dest += outSizeCur;
                    outSize -= outSizeCur;
                    destLen += outSizeCur;
                    if (res != SZ_OK)
                        return res;
                    if (outSizeCur == 0 || outSize == 0)
                        return SZ_OK;
                }
            }


            /* ---------- One Call Interface ---------- */

            /* LzmaDecode

            finishMode:
              It has meaning only if the decoding reaches output limit (*destLen).
              LZMA_FINISH_ANY - Decode just destLen bytes.
              LZMA_FINISH_END - Stream must be finished after (*destLen).

            Returns:
              SZ_OK
                status:
                  LZMA_STATUS_FINISHED_WITH_MARK
                  LZMA_STATUS_NOT_FINISHED
                  LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK
              SZ_ERROR_DATA - Data error
              SZ_ERROR_MEM  - Memory allocation error
              SZ_ERROR_UNSUPPORTED - Unsupported properties
              SZ_ERROR_INPUT_EOF - It needs more bytes in input buffer (src).
            */

            public static SRes LzmaDecode(P<byte> dest, ref long destLen, P<byte> src, ref long srcLen, P<byte> propData, uint propSize, ELzmaFinishMode finishMode, out ELzmaStatus status, ISzAlloc alloc)
            {
                long outSize = destLen;
                long inSize = srcLen;
                destLen = 0;
                srcLen = 0;
                status = ELzmaStatus.LZMA_STATUS_NOT_SPECIFIED;

                if (inSize < RC_INIT_SIZE)
                    return SZ_ERROR_INPUT_EOF;

                CLzmaDec decoder = new CLzmaDec();
                decoder.LzmaDec_Construct();

                SRes res;
                if ((res = decoder.LzmaDec_AllocateProbs(propData, propSize, alloc)) != SZ_OK)
                    return res;

                decoder.mDic = dest;
                decoder.mDicBufSize = outSize;
                decoder.LzmaDec_Init();
                srcLen = inSize;

                res = decoder.LzmaDec_DecodeToDic(outSize, src, ref srcLen, finishMode, out status);

                destLen = decoder.mDicPos;

                if (res == SZ_OK && status == ELzmaStatus.LZMA_STATUS_NEEDS_MORE_INPUT)
                    res = SZ_ERROR_INPUT_EOF;

                decoder.LzmaDec_FreeProbs(alloc);
                return res;
            }

            #endregion

            #region Internal Methods

            internal void LzmaDec_InitDicAndState(bool initDic, bool initState)
            {
                mNeedFlush = true;
                mRemainLen = 0;
                mTempBufSize = 0;

                if (initDic)
                {
                    mProcessedPos = 0;
                    mCheckDicSize = 0;
                    mNeedInitState = true;
                }

                if (initState)
                    mNeedInitState = true;
            }

            #endregion

            #region Private Methods

            /* First LZMA-symbol is always decoded.
            And it decodes new LZMA-symbols while (buf < bufLimit), but "buf" is without last normalization
            Out:
              Result:
                SZ_OK - OK
                SZ_ERROR_DATA - Error
              p.remainLen:
                < kMatchSpecLenStart : normal remain
                = kMatchSpecLenStart : finished
                = kMatchSpecLenStart + 1 : Flush marker
                = kMatchSpecLenStart + 2 : State Init Marker
            */

            private SRes LzmaDec_DecodeReal(long limit, P<byte> bufLimit)
            {
                P<ushort> probs = mProbs;

                uint state = mState;
                uint rep0 = mReps[0];
                uint rep1 = mReps[1];
                uint rep2 = mReps[2];
                uint rep3 = mReps[3];
                uint pbMask = (1u << mProp.mPB) - 1;
                uint lpMask = (1u << mProp.mLP) - 1;
                int lc = mProp.mLC;

                P<byte> dic = mDic;
                long dicBufSize = mDicBufSize;
                long dicPos = mDicPos;

                uint processedPos = mProcessedPos;
                uint checkDicSize = mCheckDicSize;
                uint len = 0;

                P<byte> buf = mBuf;
                uint range = mRange;
                uint code = mCode;

                do
                {
                    uint bound;
                    uint ttt;
                    uint posState = processedPos & pbMask;

                    P<ushort> prob = probs + kIsMatch + (state << kNumPosBitsMax) + posState;
                    if (_IF_BIT_0(prob, out ttt, out bound, ref range, ref code, ref buf))
                    {
                        _UPDATE_0(prob, ttt, bound, ref range);
                        prob = probs + kLiteral;
                        if (checkDicSize != 0 || processedPos != 0)
                            prob += (LZMA_LIT_SIZE * (((processedPos & lpMask) << lc)
                                + (dic[(dicPos == 0 ? dicBufSize : dicPos) - 1] >> (8 - lc))));

                        uint symbol;
                        if (state < kNumLitStates)
                        {
                            state -= (state < 4) ? state : 3;
                            symbol = 1;
                            do { _GET_BIT(prob + symbol, ref symbol, out ttt, out bound, ref range, ref code, ref buf); }
                            while (symbol < 0x100);
                        }
                        else
                        {
                            uint matchByte = mDic[(dicPos - rep0) + ((dicPos < rep0) ? dicBufSize : 0)];
                            uint offs = 0x100;
                            state -= (state < 10) ? 3u : 6u;
                            symbol = 1;
                            do
                            {
                                uint bit;
                                P<ushort> probLit;
                                matchByte <<= 1;
                                bit = (matchByte & offs);
                                probLit = prob + offs + bit + symbol;
                                if (_GET_BIT2(probLit, ref symbol, out ttt, out bound, ref range, ref code, ref buf))
                                    offs &= bit;
                                else
                                    offs &= ~bit;
                            }
                            while (symbol < 0x100);
                        }
                        dic[dicPos++] = (byte)symbol;
                        processedPos++;
                        continue;
                    }
                    else
                    {
                        _UPDATE_1(prob, ttt, bound, ref range, ref code);
                        prob = probs + kIsRep + state;
                        if (_IF_BIT_0(prob, out ttt, out bound, ref range, ref code, ref buf))
                        {
                            _UPDATE_0(prob, ttt, bound, ref range);
                            state += kNumStates;
                            prob = probs + kLenCoder;
                        }
                        else
                        {
                            _UPDATE_1(prob, ttt, bound, ref range, ref code);
                            if (checkDicSize == 0 && processedPos == 0)
                                return SZ_ERROR_DATA;
                            prob = probs + kIsRepG0 + state;
                            if (_IF_BIT_0(prob, out ttt, out bound, ref range, ref code, ref buf))
                            {
                                _UPDATE_0(prob, ttt, bound, ref range);
                                prob = probs + kIsRep0Long + (state << kNumPosBitsMax) + posState;
                                if (_IF_BIT_0(prob, out ttt, out bound, ref range, ref code, ref buf))
                                {
                                    _UPDATE_0(prob, ttt, bound, ref range);
                                    dic[dicPos] = dic[(dicPos - rep0) + ((dicPos < rep0) ? dicBufSize : 0)];
                                    dicPos++;
                                    processedPos++;
                                    state = state < kNumLitStates ? 9u : 11u;
                                    continue;
                                }
                                _UPDATE_1(prob, ttt, bound, ref range, ref code);
                            }
                            else
                            {
                                uint distance;
                                _UPDATE_1(prob, ttt, bound, ref range, ref code);
                                prob = probs + kIsRepG1 + state;
                                if (_IF_BIT_0(prob, out ttt, out bound, ref range, ref code, ref buf))
                                {
                                    _UPDATE_0(prob, ttt, bound, ref range);
                                    distance = rep1;
                                }
                                else
                                {
                                    _UPDATE_1(prob, ttt, bound, ref range, ref code);
                                    prob = probs + kIsRepG2 + state;
                                    if (_IF_BIT_0(prob, out ttt, out bound, ref range, ref code, ref buf))
                                    {
                                        _UPDATE_0(prob, ttt, bound, ref range);
                                        distance = rep2;
                                    }
                                    else
                                    {
                                        _UPDATE_1(prob, ttt, bound, ref range, ref code);
                                        distance = rep3;
                                        rep3 = rep2;
                                    }
                                    rep2 = rep1;
                                }
                                rep1 = rep0;
                                rep0 = distance;
                            }
                            state = state < kNumLitStates ? 8u : 11u;
                            prob = probs + kRepLenCoder;
                        }
                        {
                            uint limit2, offset;
                            P<ushort> probLen = prob + kLenChoice;
                            if (_IF_BIT_0(probLen, out ttt, out bound, ref range, ref code, ref buf))
                            {
                                _UPDATE_0(probLen, ttt, bound, ref range);
                                probLen = prob + kLenLow + (posState << kLenNumLowBits);
                                offset = 0;
                                limit2 = (1 << kLenNumLowBits);
                            }
                            else
                            {
                                _UPDATE_1(probLen, ttt, bound, ref range, ref code);
                                probLen = prob + kLenChoice2;
                                if (_IF_BIT_0(probLen, out ttt, out bound, ref range, ref code, ref buf))
                                {
                                    _UPDATE_0(probLen, ttt, bound, ref range);
                                    probLen = prob + kLenMid + (posState << kLenNumMidBits);
                                    offset = kLenNumLowSymbols;
                                    limit2 = (1 << kLenNumMidBits);
                                }
                                else
                                {
                                    _UPDATE_1(probLen, ttt, bound, ref range, ref code);
                                    probLen = prob + kLenHigh;
                                    offset = kLenNumLowSymbols + kLenNumMidSymbols;
                                    limit2 = (1 << kLenNumHighBits);
                                }
                            }
                            _TREE_DECODE(probLen, limit2, out len, out ttt, out bound, ref range, ref code, ref buf);
                            len += offset;
                        }

                        if (state >= kNumStates)
                        {
                            uint distance;
                            prob = probs + kPosSlot +
                                ((len < kNumLenToPosStates ? len : kNumLenToPosStates - 1) << kNumPosSlotBits);
                            _TREE_6_DECODE(prob, out distance, out ttt, out bound, ref range, ref code, ref buf);
                            if (distance >= kStartPosModelIndex)
                            {
                                uint posSlot = (uint)distance;
                                int numDirectBits = (int)(((distance >> 1) - 1));
                                distance = (2 | (distance & 1));
                                if (posSlot < kEndPosModelIndex)
                                {
                                    distance <<= numDirectBits;
                                    prob = probs + kSpecPos + distance - posSlot - 1;
                                    {
                                        uint mask = 1;
                                        uint i = 1;
                                        do
                                        {
                                            if (_GET_BIT2(prob + i, ref i, out ttt, out bound, ref range, ref code, ref buf))
                                                distance |= mask;
                                            mask <<= 1;
                                        }
                                        while (--numDirectBits != 0);
                                    }
                                }
                                else
                                {
                                    numDirectBits -= kNumAlignBits;
                                    do
                                    {
                                        _NORMALIZE(ref range, ref code, ref buf);
                                        range >>= 1;

                                        {
                                            uint t;
                                            code -= range;
                                            t = (0 - ((uint)code >> 31)); /* (uint)((Int32)code >> 31) */
                                            distance = (distance << 1) + (t + 1);
                                            code += range & t;
                                        }
                                        /*
                                        distance <<= 1;
                                        if (code >= range)
                                        {
                                          code -= range;
                                          distance |= 1;
                                        }
                                        */
                                    }
                                    while (--numDirectBits != 0);
                                    prob = probs + kAlign;
                                    distance <<= kNumAlignBits;
                                    {
                                        uint i = 1;
                                        if (_GET_BIT2(prob + i, ref i, out ttt, out bound, ref range, ref code, ref buf)) distance |= 1;
                                        if (_GET_BIT2(prob + i, ref i, out ttt, out bound, ref range, ref code, ref buf)) distance |= 2;
                                        if (_GET_BIT2(prob + i, ref i, out ttt, out bound, ref range, ref code, ref buf)) distance |= 4;
                                        if (_GET_BIT2(prob + i, ref i, out ttt, out bound, ref range, ref code, ref buf)) distance |= 8;
                                    }
                                    if (distance == (uint)0xFFFFFFFF)
                                    {
                                        len += kMatchSpecLenStart;
                                        state -= kNumStates;
                                        break;
                                    }
                                }
                            }
                            rep3 = rep2;
                            rep2 = rep1;
                            rep1 = rep0;
                            rep0 = distance + 1;
                            if (checkDicSize == 0)
                            {
                                if (distance >= processedPos)
                                    return SZ_ERROR_DATA;
                            }
                            else if (distance >= checkDicSize)
                                return SZ_ERROR_DATA;
                            state = (state < kNumStates + kNumLitStates) ? kNumLitStates : kNumLitStates + 3u;
                        }

                        len += kMatchMinLen;

                        if (limit == dicPos)
                            return SZ_ERROR_DATA;
                        {
                            long rem = limit - dicPos;
                            uint curLen = ((rem < len) ? (uint)rem : len);
                            long pos = (dicPos - rep0) + ((dicPos < rep0) ? dicBufSize : 0);

                            processedPos += curLen;

                            len -= curLen;
                            if (pos + curLen <= dicBufSize)
                            {
                                P<byte> dest = dic + dicPos;
                                long src = (long)pos - (long)dicPos;
                                P<byte> lim = dest + curLen;
                                dicPos += curLen;
                                do { dest[0] = dest[src]; }
                                while (++dest != lim);
                            }
                            else
                            {
                                do
                                {
                                    dic[dicPos++] = dic[pos];
                                    if (++pos == dicBufSize)
                                        pos = 0;
                                }
                                while (--curLen != 0);
                            }
                        }
                    }
                }
                while (dicPos < limit && buf < bufLimit);
                _NORMALIZE(ref range, ref code, ref buf);
                mBuf = buf;
                mRange = range;
                mCode = code;
                mRemainLen = len;
                mDicPos = dicPos;
                mProcessedPos = processedPos;
                mReps[0] = rep0;
                mReps[1] = rep1;
                mReps[2] = rep2;
                mReps[3] = rep3;
                mState = state;

                return SZ_OK;
            }

            private void LzmaDec_WriteRem(long limit)
            {
                if (mRemainLen != 0 && mRemainLen < kMatchSpecLenStart)
                {
                    P<byte> dic = mDic;
                    long dicPos = mDicPos;
                    long dicBufSize = mDicBufSize;
                    uint len = mRemainLen;
                    uint rep0 = mReps[0];
                    if (limit - dicPos < len)
                        len = (uint)(limit - dicPos);

                    if (mCheckDicSize == 0 && mProp.mDicSize - mProcessedPos <= len)
                        mCheckDicSize = mProp.mDicSize;

                    mProcessedPos += len;
                    mRemainLen -= len;

                    while (len != 0)
                    {
                        len--;
                        dic[dicPos] = dic[(dicPos - rep0) + ((dicPos < rep0) ? dicBufSize : 0)];
                        dicPos++;
                    }

                    mDicPos = dicPos;
                }
            }

            private SRes LzmaDec_DecodeReal2(long limit, P<byte> bufLimit)
            {
                do
                {
                    long limit2 = limit;
                    if (mCheckDicSize == 0)
                    {
                        uint rem = mProp.mDicSize - mProcessedPos;
                        if (limit - mDicPos > rem)
                            limit2 = mDicPos + rem;
                    }

                    SRes res;
                    if ((res = LzmaDec_DecodeReal(limit2, bufLimit)) != SZ_OK)
                        return res;

                    if (mProcessedPos >= mProp.mDicSize)
                        mCheckDicSize = mProp.mDicSize;

                    LzmaDec_WriteRem(limit);
                }
                while (mDicPos < limit && mBuf < bufLimit && mRemainLen < kMatchSpecLenStart);

                if (mRemainLen > kMatchSpecLenStart)
                    mRemainLen = kMatchSpecLenStart;

                return SZ_OK;
            }

            private ELzmaDummy LzmaDec_TryDummy(P<byte> buf, long inSize)
            {
                uint range = mRange;
                uint code = mCode;
                P<byte> bufLimit = buf + inSize;
                P<ushort> probs = mProbs;
                uint state = mState;
                ELzmaDummy res;

                {
                    bool xxx;

                    uint bound;
                    uint ttt;
                    uint posState = mProcessedPos & ((1u << mProp.mPB) - 1);

                    P<ushort> prob = probs + kIsMatch + (state << kNumPosBitsMax) + posState;
                    if (!_IF_BIT_0_CHECK(out xxx, prob, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                        return ELzmaDummy.DUMMY_ERROR;
                    if (xxx)
                    {
                        _UPDATE_0_CHECK(bound, ref range);

                        /* if (bufLimit - buf >= 7) return DUMMY_LIT; */

                        prob = probs + kLiteral;
                        if (mCheckDicSize != 0 || mProcessedPos != 0)
                            prob += LZMA_LIT_SIZE *
                                (((mProcessedPos & ((1 << mProp.mLP) - 1)) << mProp.mLC) +
                                (mDic[(mDicPos == 0 ? mDicBufSize : mDicPos) - 1] >> (8 - mProp.mLC)));

                        if (state < kNumLitStates)
                        {
                            uint symbol = 1;
                            do
                            {
                                if (!_GET_BIT_CHECK(prob + symbol, ref symbol, out ttt, ref bound, ref range, ref code, ref buf, bufLimit))
                                    return ELzmaDummy.DUMMY_ERROR;
                            }
                            while (symbol < 0x100);
                        }
                        else
                        {
                            uint matchByte = mDic[mDicPos - mReps[0] + (mDicPos < mReps[0] ? mDicBufSize : 0)];
                            uint offs = 0x100;
                            uint symbol = 1;
                            do
                            {
                                matchByte <<= 1;
                                uint bit = matchByte & offs;
                                P<ushort> probLit = prob + offs + bit + symbol;
                                if (!_GET_BIT2_CHECK(probLit, ref symbol, delegate { offs &= ~bit; }, delegate { offs &= bit; }, out ttt, ref bound, ref range, ref code, ref buf, bufLimit))
                                    return ELzmaDummy.DUMMY_ERROR;
                            }
                            while (symbol < 0x100);
                        }
                        res = ELzmaDummy.DUMMY_LIT;
                    }
                    else
                    {
                        uint len;
                        _UPDATE_1_CHECK(bound, ref range, ref code);

                        prob = probs + kIsRep + state;
                        if (!_IF_BIT_0_CHECK(out xxx, prob, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                            return ELzmaDummy.DUMMY_ERROR;
                        if (xxx)
                        {
                            _UPDATE_0_CHECK(bound, ref range);
                            state = 0;
                            prob = probs + kLenCoder;
                            res = ELzmaDummy.DUMMY_MATCH;
                        }
                        else
                        {
                            _UPDATE_1_CHECK(bound, ref range, ref code);
                            res = ELzmaDummy.DUMMY_REP;
                            prob = probs + kIsRepG0 + state;
                            if (!_IF_BIT_0_CHECK(out xxx, prob, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                                return ELzmaDummy.DUMMY_ERROR;
                            if (xxx)
                            {
                                _UPDATE_0_CHECK(bound, ref range);
                                prob = probs + kIsRep0Long + (state << kNumPosBitsMax) + posState;
                                if (!_IF_BIT_0_CHECK(out xxx, prob, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                                    return ELzmaDummy.DUMMY_ERROR;
                                if (xxx)
                                {
                                    _UPDATE_0_CHECK(bound, ref range);
                                    if (!_NORMALIZE_CHECK(ref range, ref code, ref buf, bufLimit))
                                        return ELzmaDummy.DUMMY_ERROR;
                                    return ELzmaDummy.DUMMY_REP;
                                }
                                else
                                {
                                    _UPDATE_1_CHECK(bound, ref range, ref code);
                                }
                            }
                            else
                            {
                                _UPDATE_1_CHECK(bound, ref range, ref code);
                                prob = probs + kIsRepG1 + state;
                                if (!_IF_BIT_0_CHECK(out xxx, prob, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                                    return ELzmaDummy.DUMMY_ERROR;
                                if (xxx)
                                {
                                    _UPDATE_0_CHECK(bound, ref range);
                                }
                                else
                                {
                                    _UPDATE_1_CHECK(bound, ref range, ref code);
                                    prob = probs + kIsRepG2 + state;
                                    if (!_IF_BIT_0_CHECK(out xxx, prob, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                                        return ELzmaDummy.DUMMY_ERROR;
                                    if (xxx)
                                    {
                                        _UPDATE_0_CHECK(bound, ref range);
                                    }
                                    else
                                    {
                                        _UPDATE_1_CHECK(bound, ref range, ref code);
                                    }
                                }
                            }
                            state = kNumStates;
                            prob = probs + kRepLenCoder;
                        }
                        {
                            uint limit, offset;
                            P<ushort> probLen = prob + kLenChoice;
                            if (!_IF_BIT_0_CHECK(out xxx, probLen, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                                return ELzmaDummy.DUMMY_ERROR;
                            if (xxx)
                            {
                                _UPDATE_0_CHECK(bound, ref range);
                                probLen = prob + kLenLow + (posState << kLenNumLowBits);
                                offset = 0;
                                limit = 1 << kLenNumLowBits;
                            }
                            else
                            {
                                _UPDATE_1_CHECK(bound, ref range, ref code);
                                probLen = prob + kLenChoice2;
                                if (!_IF_BIT_0_CHECK(out xxx, probLen, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                                    return ELzmaDummy.DUMMY_ERROR;
                                if (xxx)
                                {
                                    _UPDATE_0_CHECK(bound, ref range);
                                    probLen = prob + kLenMid + (posState << kLenNumMidBits);
                                    offset = kLenNumLowSymbols;
                                    limit = 1 << kLenNumMidBits;
                                }
                                else
                                {
                                    _UPDATE_1_CHECK(bound, ref range, ref code);
                                    probLen = prob + kLenHigh;
                                    offset = kLenNumLowSymbols + kLenNumMidSymbols;
                                    limit = 1 << kLenNumHighBits;
                                }
                            }
                            if (!_TREE_DECODE_CHECK(probLen, limit, out len, out ttt, ref bound, ref range, ref code, ref buf, bufLimit))
                                return ELzmaDummy.DUMMY_ERROR;
                            len += offset;
                        }

                        if (state < 4)
                        {
                            prob = probs + kPosSlot + ((len < kNumLenToPosStates ? len : kNumLenToPosStates - 1) << kNumPosSlotBits);
                            uint posSlot;
                            if (!_TREE_DECODE_CHECK(prob, 1 << kNumPosSlotBits, out posSlot, out ttt, ref bound, ref range, ref code, ref buf, bufLimit))
                                return ELzmaDummy.DUMMY_ERROR;
                            if (posSlot >= kStartPosModelIndex)
                            {
                                int numDirectBits = (((int)posSlot >> 1) - 1);

                                /* if (bufLimit - buf >= 8) return DUMMY_MATCH; */

                                if (posSlot < kEndPosModelIndex)
                                {
                                    prob = probs + kSpecPos + ((2 | (posSlot & 1)) << numDirectBits) - posSlot - 1;
                                }
                                else
                                {
                                    numDirectBits -= kNumAlignBits;
                                    do
                                    {
                                        if (!_NORMALIZE_CHECK(ref range, ref code, ref buf, bufLimit))
                                            return ELzmaDummy.DUMMY_ERROR;

                                        range >>= 1;

                                        //code -= range & (((code - range) >> 31) - 1);
                                        if (code >= range)
                                            code -= range;
                                    }
                                    while (--numDirectBits != 0);
                                    prob = probs + kAlign;
                                    numDirectBits = kNumAlignBits;
                                }
                                {
                                    uint i = 1;
                                    do
                                    {
                                        if (!_GET_BIT_CHECK(prob + i, ref i, out ttt, ref bound, ref range, ref code, ref buf, bufLimit))
                                            return ELzmaDummy.DUMMY_ERROR;
                                    }
                                    while (--numDirectBits != 0);
                                }
                            }
                        }
                    }
                }
                if (!_NORMALIZE_CHECK(ref range, ref code, ref buf, bufLimit))
                    return ELzmaDummy.DUMMY_ERROR;
                return res;
            }

            private void LzmaDec_InitRc(P<byte> data)
            {
                mCode = ((uint)data[1] << 24) | ((uint)data[2] << 16) | ((uint)data[3] << 8) | ((uint)data[4]);
                mRange = 0xFFFFFFFF;
                mNeedFlush = false;
            }

            private void LzmaDec_InitStateReal()
            {
                uint numProbs = kLiteral + (LZMA_LIT_SIZE << (mProp.mLC + mProp.mLP));
                for (uint i = 0; i < numProbs; i++)
                    mProbs[i] = kBitModelTotal >> 1;
                mReps[0] = 1;
                mReps[1] = 1;
                mReps[2] = 1;
                mReps[3] = 1;
                mState = 0;
                mNeedInitState = false;
            }

            private void LzmaDec_FreeDict(ISzAlloc alloc)
            {
                alloc.FreeBytes(alloc, mDic.mBuffer);
                mDic = null;
            }

            private SRes LzmaDec_AllocateProbs2(CLzmaProps propNew, ISzAlloc alloc)
            {
                uint numProbs = LzmaProps_GetNumProbs(propNew);
                if (mProbs == null || numProbs != mNumProbs)
                {
                    LzmaDec_FreeProbs(alloc);
                    mProbs = alloc.AllocUInt16(alloc, numProbs);
                    mNumProbs = numProbs;
                    if (mProbs == null)
                        return SZ_ERROR_MEM;
                }
                return SZ_OK;
            }

            #endregion

            #region Macros

            private static void _NORMALIZE(ref uint range, ref uint code, ref P<byte> buf)
            {
                if (range < kTopValue)
                {
                    range <<= 8;
                    code = (code << 8) | buf[0];
                    buf++;
                }
            }

            private static bool _IF_BIT_0(P<ushort> p, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf)
            {
                ttt = p[0];
                _NORMALIZE(ref range, ref code, ref buf);
                bound = (range >> kNumBitModelTotalBits) * ttt;
                return code < bound;
            }

            private static void _UPDATE_0(P<ushort> p, uint ttt, uint bound, ref uint range)
            {
                range = bound;
                p[0] = (ushort)(ttt + ((kBitModelTotal - ttt) >> kNumMoveBits));
            }

            private static void _UPDATE_1(P<ushort> p, uint ttt, uint bound, ref uint range, ref uint code)
            {
                range -= bound;
                code -= bound;
                p[0] = (ushort)(ttt - (ttt >> kNumMoveBits));
            }

            private static bool _GET_BIT2(P<ushort> p, ref uint i, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf)
            {
                if (_IF_BIT_0(p, out ttt, out bound, ref range, ref code, ref buf))
                {
                    _UPDATE_0(p, ttt, bound, ref range);
                    i = (i + i);
                    return false; // bit == 0
                }
                else
                {
                    _UPDATE_1(p, ttt, bound, ref range, ref code);
                    i = (i + i) + 1;
                    return true; // bit == 1
                }
            }

            private static void _GET_BIT(P<ushort> p, ref uint i, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf)
            {
                _GET_BIT2(p, ref i, out ttt, out bound, ref range, ref code, ref buf);
            }

            private static void _TREE_GET_BIT(P<ushort> probs, ref uint i, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf)
            {
                _GET_BIT(probs + i, ref i, out ttt, out bound, ref range, ref code, ref buf);
            }

            private static void _TREE_DECODE(P<ushort> probs, uint limit, out uint i, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf)
            {
                i = 1;
                do { _TREE_GET_BIT(probs, ref i, out ttt, out bound, ref range, ref code, ref buf); }
                while (i < limit);
                i -= limit;
            }

            /* #define _LZMA_SIZE_OPT */

#if _LZMA_SIZE_OPT
            //#define TREE_6_DECODE(probs, i) TREE_DECODE(probs, (1 << 6), i)
#else
            //#define TREE_6_DECODE(probs, i) \
            //  { i = 1; \
            //  TREE_GET_BIT(probs, i); \
            //  TREE_GET_BIT(probs, i); \
            //  TREE_GET_BIT(probs, i); \
            //  TREE_GET_BIT(probs, i); \
            //  TREE_GET_BIT(probs, i); \
            //  TREE_GET_BIT(probs, i); \
            //  i -= 0x40; }
#endif

            private static void _TREE_6_DECODE(P<ushort> probs, out uint i, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf)
            {
                _TREE_DECODE(probs, 1 << 6, out i, out ttt, out bound, ref range, ref code, ref buf);
            }

            private static bool _NORMALIZE_CHECK(ref uint range, ref uint code, ref P<byte> buf, P<byte> bufLimit)
            {
                if (range < kTopValue)
                {
                    if (buf >= bufLimit)
                        return false; // ELzmaDummy.DUMMY_ERROR;

                    range <<= 8;
                    code = (code << 8) | buf[0];
                    buf++;
                }

                return true;
            }

            private static bool _IF_BIT_0_CHECK(out bool result, P<ushort> p, out uint ttt, out uint bound, ref uint range, ref uint code, ref P<byte> buf, P<byte> bufLimit)
            {
                ttt = p[0];
                if (!_NORMALIZE_CHECK(ref range, ref code, ref buf, bufLimit))
                {
                    result = false;
                    bound = 0;
                    return false;
                }
                bound = (range >> kNumBitModelTotalBits) * ttt;
                result = code < bound;
                return true;
            }

            private static void _UPDATE_0_CHECK(uint bound, ref uint range)
            {
                range = bound;
            }

            private static void _UPDATE_1_CHECK(uint bound, ref uint range, ref uint code)
            {
                range -= bound;
                code -= bound;
            }

            private static bool _GET_BIT2_CHECK(P<ushort> p, ref uint i, Action A0, Action A1, out uint ttt, ref uint bound, ref uint range, ref uint code, ref P<byte> buf, P<byte> bufLimit)
            {
                bool xxx;
                if (!_IF_BIT_0_CHECK(out xxx, p, out ttt, out bound, ref range, ref code, ref buf, bufLimit))
                    return false;
                if (xxx)
                {
                    _UPDATE_0_CHECK(bound, ref range);
                    i = (i + i);
                    A0();
                }
                else
                {
                    _UPDATE_1_CHECK(bound, ref range, ref code);
                    i = (i + i) + 1;
                    A1();
                }
                return true;
            }

            private static bool _GET_BIT_CHECK(P<ushort> p, ref uint i, out uint ttt, ref uint bound, ref uint range, ref uint code, ref P<byte> buf, P<byte> bufLimit)
            {
                return _GET_BIT2_CHECK(p, ref i, delegate { }, delegate { }, out ttt, ref bound, ref range, ref code, ref buf, bufLimit);
            }

            private static bool _TREE_DECODE_CHECK(P<ushort> probs, uint limit, out uint i, out uint ttt, ref uint bound, ref uint range, ref uint code, ref P<byte> buf, P<byte> bufLimit)
            {
                i = 1;
                do
                {
                    if (!_GET_BIT_CHECK(probs + i, ref i, out ttt, ref bound, ref range, ref code, ref buf, bufLimit))
                        return false;
                }
                while (i < limit);
                i -= limit;
                return true;
            }

            private static uint LzmaProps_GetNumProbs(CLzmaProps p)
            {
                return LZMA_BASE_SIZE + (LZMA_LIT_SIZE << (p.mLC + p.mLP));
            }

            #endregion
        }

        /* There are two types of LZMA streams:
             0) Stream with end mark. That end mark adds about 6 bytes to compressed size.
             1) Stream without end mark. You must know exact uncompressed size to decompress such stream. */

        public enum ELzmaFinishMode
        {
            LZMA_FINISH_ANY,   /* finish at any point */
            LZMA_FINISH_END    /* block must be finished at the end */
        }

        /* ELzmaFinishMode has meaning only if the decoding reaches output limit !!!

           You must use LZMA_FINISH_END, when you know that current output buffer
           covers last bytes of block. In other cases you must use LZMA_FINISH_ANY.

           If LZMA decoder sees end marker before reaching output limit, it returns SZ_OK,
           and output value of destLen will be less than output buffer size limit.
           You can check status result also.

           You can use multiple checks to test data integrity after full decompression:
             1) Check Result and "status" variable.
             2) Check that output(destLen) = uncompressedSize, if you know real uncompressedSize.
             3) Check that output(srcLen) = compressedSize, if you know real compressedSize.
                You must use correct finish mode in that case. */

        public enum ELzmaStatus
        {
            LZMA_STATUS_NOT_SPECIFIED,               /* use main error code instead */
            LZMA_STATUS_FINISHED_WITH_MARK,          /* stream was finished with end mark. */
            LZMA_STATUS_NOT_FINISHED,                /* stream was not finished */
            LZMA_STATUS_NEEDS_MORE_INPUT,            /* you must provide more input bytes */
            LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK  /* there is probability that stream was finished without end mark */
        }

        /* ELzmaStatus is used only as output value for function call */
    }
}
