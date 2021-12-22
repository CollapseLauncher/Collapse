using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        public sealed class CLzma2EncProps
        {
            #region Variables

            public CLzmaEncProps mLzmaProps;
            public long mBlockSize;
            public int mNumBlockThreads;
            public int mNumTotalThreads;

            #endregion

            #region Methods

            public CLzma2EncProps() { }
            public CLzma2EncProps(CLzma2EncProps other)
            {
                mLzmaProps = new CLzmaEncProps(other.mLzmaProps);
                mBlockSize = other.mBlockSize;
                mNumBlockThreads = other.mNumBlockThreads;
                mNumTotalThreads = other.mNumTotalThreads;
            }

            public void Lzma2EncProps_Init()
            {
                mLzmaProps = CLzmaEncProps.LzmaEncProps_Init();
                mNumTotalThreads = -1;
                mNumBlockThreads = -1;
                mBlockSize = 0;
            }

            public void Lzma2EncProps_Normalize()
            {
                CLzmaEncProps normalizedLzmaProps = new CLzmaEncProps(mLzmaProps);
                normalizedLzmaProps.LzmaEncProps_Normalize();
                int tempThreadsNormalized = normalizedLzmaProps.mNumThreads;
                int tempThreads = mLzmaProps.mNumThreads;
                int tempBlockThreads = mNumBlockThreads;
                int tempTotalThreads = mNumTotalThreads;

                if (tempBlockThreads > NUM_MT_CODER_THREADS_MAX)
                    tempBlockThreads = NUM_MT_CODER_THREADS_MAX;

                if (tempTotalThreads <= 0)
                {
                    if (tempBlockThreads <= 0)
                        tempBlockThreads = 1;

                    tempTotalThreads = tempThreadsNormalized * tempBlockThreads;
                }
                else if (tempBlockThreads <= 0)
                {
                    tempBlockThreads = tempTotalThreads / tempThreadsNormalized;

                    if (tempBlockThreads == 0)
                    {
                        tempThreads = 1;
                        tempBlockThreads = tempTotalThreads;
                    }

                    if (tempBlockThreads > NUM_MT_CODER_THREADS_MAX)
                        tempBlockThreads = NUM_MT_CODER_THREADS_MAX;
                }
                else if (tempThreads <= 0)
                {
                    tempThreads = tempTotalThreads / tempBlockThreads;
                    if (tempThreads == 0)
                        tempThreads = 1;
                }
                else
                {
                    tempTotalThreads = tempThreadsNormalized * tempBlockThreads;
                }

                mLzmaProps.mNumThreads = tempThreads;
                mNumBlockThreads = tempBlockThreads;
                mNumTotalThreads = tempTotalThreads;
                mLzmaProps.LzmaEncProps_Normalize();

                if (mBlockSize == 0)
                {
                    uint dictSize = mLzmaProps.mDictSize;
                    long blockSize = (long)dictSize << 2;

                    const uint kMinSize = 1 << 20;
                    const uint kMaxSize = 1 << 28;

                    if (blockSize < kMinSize)
                        blockSize = kMinSize;

                    if (blockSize > kMaxSize)
                        blockSize = kMaxSize;

                    if (blockSize < dictSize)
                        blockSize = dictSize;

                    mBlockSize = blockSize;
                }
            }

            #endregion
        }

        internal sealed class CLzma2EncInternal
        {
            #region Constants

            private const int LZMA2_CONTROL_LZMA = (1 << 7);
            private const int LZMA2_CONTROL_COPY_NO_RESET = 2;
            private const int LZMA2_CONTROL_COPY_RESET_DIC = 1;
            private const int LZMA2_CONTROL_EOF = 0;

            private const int LZMA2_PACK_SIZE_MAX = (1 << 16);
            private const int LZMA2_COPY_CHUNK_SIZE = LZMA2_PACK_SIZE_MAX;
            private const int LZMA2_UNPACK_SIZE_MAX = (1 << 21);
            internal const int LZMA2_KEEP_WINDOW_SIZE = LZMA2_UNPACK_SIZE_MAX;

            private const int LZMA2_CHUNK_SIZE_COMPRESSED_MAX = ((1 << 16) + 16);

            #endregion

            #region Variables

            internal CLzmaEnc mEnc;
            internal ulong mSrcPos;
            internal byte mProps;
            internal bool mNeedInitState;
            internal bool mNeedInitProp;

            #endregion

            #region Internal Methods

            internal SRes Lzma2EncInt_Init(CLzma2EncProps props)
            {
                byte[] propsEncoded = new byte[LZMA_PROPS_SIZE];
                long propsSize = LZMA_PROPS_SIZE;

                SRes res;
                if ((res = mEnc.LzmaEnc_SetProps(props.mLzmaProps)) != SZ_OK)
                    return res;

                if ((res = mEnc.LzmaEnc_WriteProperties(propsEncoded, ref propsSize)) != SZ_OK)
                    return res;

                mSrcPos = 0;
                mProps = propsEncoded[0];
                mNeedInitState = true;
                mNeedInitProp = true;
                return SZ_OK;
            }

            internal SRes Lzma2EncInt_EncodeSubblock(P<byte> outBuf, ref long packSizeRes, ISeqOutStream outStream)
            {
                long packSizeLimit = packSizeRes;
                long packSize = packSizeLimit;
                uint unpackSize = LZMA2_UNPACK_SIZE_MAX;
                uint lzHeaderSize = 5u + (mNeedInitProp ? 1u : 0u);

                packSizeRes = 0;
                if (packSize < lzHeaderSize)
                    return SZ_ERROR_OUTPUT_EOF;
                packSize -= lzHeaderSize;

                mEnc.LzmaEnc_SaveState();
                SRes res = mEnc.LzmaEnc_CodeOneMemBlock(mNeedInitState, outBuf + lzHeaderSize, ref packSize, LZMA2_PACK_SIZE_MAX, ref unpackSize);

                TR("Lzma2EncInt_EncodeSubblock:packSize", checked((int)packSize));
                TR("Lzma2EncInt_EncodeSubblock:unpackSize", unpackSize);
                DebugPrint("\npackSize = {0:0000000} unpackSize = {1:0000000}  ", packSize, unpackSize);

                if (unpackSize == 0)
                    return res;

                bool useCopyBlock;
                if (res == SZ_OK)
                    useCopyBlock = (packSize + 2 >= unpackSize || packSize > (1 << 16));
                else
                {
                    if (res != SZ_ERROR_OUTPUT_EOF)
                        return res;
                    res = SZ_OK;
                    useCopyBlock = true;
                }

                if (useCopyBlock)
                {
                    long destPos = 0;
                    DebugPrint("################# COPY           ");

                    while (unpackSize > 0)
                    {
                        uint u = (unpackSize < LZMA2_COPY_CHUNK_SIZE) ? unpackSize : LZMA2_COPY_CHUNK_SIZE;
                        if (packSizeLimit - destPos < u + 3)
                            return SZ_ERROR_OUTPUT_EOF;

                        outBuf[destPos++] = (byte)(mSrcPos == 0 ? LZMA2_CONTROL_COPY_RESET_DIC : LZMA2_CONTROL_COPY_NO_RESET);
                        outBuf[destPos++] = (byte)((u - 1) >> 8);
                        outBuf[destPos++] = (byte)(u - 1);

                        CUtils.memcpy(outBuf + destPos, mEnc.LzmaEnc_GetCurBuf() - unpackSize, u);

                        unpackSize -= u;
                        destPos += u;
                        mSrcPos += u;

                        if (outStream != null)
                        {
                            packSizeRes += destPos;
                            if (outStream.Write(outBuf, destPos) != destPos)
                                return SZ_ERROR_WRITE;
                            destPos = 0;
                        }
                        else
                        {
                            packSizeRes = destPos;
                        }

                        /* needInitState = true; */
                    }

                    mEnc.LzmaEnc_RestoreState();
                    return SZ_OK;
                }
                else
                {
                    long destPos = 0;
                    uint u = unpackSize - 1;
                    uint pm = (uint)(packSize - 1);

                    uint mode;
                    if (mSrcPos == 0)
                        mode = 3;
                    else if (!mNeedInitState)
                        mode = 0;
                    else if (!mNeedInitProp)
                        mode = 1;
                    else
                        mode = 2;

                    DebugPrint("               ");

                    outBuf[destPos++] = (byte)(LZMA2_CONTROL_LZMA | (mode << 5) | ((u >> 16) & 0x1F));
                    outBuf[destPos++] = (byte)(u >> 8);
                    outBuf[destPos++] = (byte)u;
                    outBuf[destPos++] = (byte)(pm >> 8);
                    outBuf[destPos++] = (byte)pm;

                    if (mNeedInitProp)
                        outBuf[destPos++] = mProps;

                    mNeedInitProp = false;
                    mNeedInitState = false;
                    destPos += packSize;
                    mSrcPos += unpackSize;

                    if (outStream != null && outStream.Write(outBuf, destPos) != destPos)
                        return SZ_ERROR_WRITE;

                    packSizeRes = destPos;
                    return SZ_OK;
                }
            }

            private static SRes Progress(ICompressProgress p, ulong inSize, ulong outSize)
            {
                return (p != null && p.Progress(inSize, outSize) != SZ_OK) ? SZ_ERROR_PROGRESS : SZ_OK;
            }

            internal SRes Lzma2Enc_EncodeMt1(CLzma2Enc mainEncoder, ISeqOutStream outStream, ISeqInStream inStream, ICompressProgress progress)
            {
                ulong packTotal = 0;
                SRes res = SZ_OK;

                if (mainEncoder.mOutBuf == null)
                {
                    mainEncoder.mOutBuf = IAlloc_AllocBytes(mainEncoder.mAlloc, LZMA2_CHUNK_SIZE_COMPRESSED_MAX);
                    if (mainEncoder.mOutBuf == null)
                        return SZ_ERROR_MEM;
                }

                if ((res = Lzma2EncInt_Init(mainEncoder.mProps)) != SZ_OK)
                    return res;

                if ((res = mEnc.LzmaEnc_PrepareForLzma2(inStream, LZMA2_KEEP_WINDOW_SIZE, mainEncoder.mAlloc, mainEncoder.mAllocBig)) != SZ_OK)
                    return res;

                for (;;)
                {
                    long packSize = LZMA2_CHUNK_SIZE_COMPRESSED_MAX;
                    res = Lzma2EncInt_EncodeSubblock(mainEncoder.mOutBuf, ref packSize, outStream);
                    if (res != SZ_OK)
                        break;
                    packTotal += (ulong)packSize;
                    res = Progress(progress, mSrcPos, packTotal);
                    if (res != SZ_OK)
                        break;
                    if (packSize == 0)
                        break;
                }

                mEnc.LzmaEnc_Finish();

                if (res == SZ_OK)
                {
                    if (outStream.Write(new byte[] { 0 }, 1) != 1)
                        return SZ_ERROR_WRITE;
                }

                return res;
            }

            #endregion
        }

        /* ---------- CLzmaEnc2Handle Interface ---------- */

        /* Lzma2Enc_* functions can return the following exit codes:
        Returns:
          SZ_OK           - OK
          SZ_ERROR_MEM    - Memory allocation error
          SZ_ERROR_PARAM  - Incorrect paramater in props
          SZ_ERROR_WRITE  - Write callback error
          SZ_ERROR_PROGRESS - some break from progress callback
          SZ_ERROR_THREAD - errors in multithreading functions (only for Mt version)
        */

        public sealed class CLzma2Enc
        {
            private const int LZMA2_LCLP_MAX = 4;

            #region Variables

            internal CLzma2EncProps mProps = new CLzma2EncProps();

            internal byte[] mOutBuf;

            internal ISzAlloc mAlloc;
            internal ISzAlloc mAllocBig;

            internal CLzma2EncInternal[] mCoders;

#if !_7ZIP_ST
            internal CMtCoder mMtCoder;
#endif

            #endregion

            #region Public Methods

            public CLzma2Enc(ISzAlloc alloc, ISzAlloc allocBig) // Lzma2Enc_Create
            {
                Trace.AllocSmallObject("CLzma2Enc", alloc);

                mProps.Lzma2EncProps_Init();
                mProps.Lzma2EncProps_Normalize();
                mOutBuf = null;
                mAlloc = alloc;
                mAllocBig = allocBig;

                mCoders = new CLzma2EncInternal[NUM_MT_CODER_THREADS_MAX];
                for (int i = 0; i < mCoders.Length; i++)
                {
                    mCoders[i] = new CLzma2EncInternal();
                    mCoders[i].mEnc = null;
                }

#if !_7ZIP_ST
                mMtCoder = new CMtCoder();
#endif
            }

            public void Lzma2Enc_Destroy()
            {
                for (uint i = 0; i < mCoders.Length; i++)
                {
                    CLzma2EncInternal t = mCoders[i];
                    if (t.mEnc != null)
                    {
                        t.mEnc.LzmaEnc_Destroy(mAlloc, mAllocBig);
                        t.mEnc = null;
                    }
                }

#if !_7ZIP_ST
                mMtCoder.MtCoder_Destruct();
#endif

                IAlloc_FreeBytes(mAlloc, mOutBuf);
                IAlloc_FreeObject(mAlloc, this);
            }

            public SRes Lzma2Enc_SetProps(CLzma2EncProps props)
            {
                TR("Lzma2Enc_SetProps:level", props.mLzmaProps.mLevel);
                TR("Lzma2Enc_SetProps:dictSize", props.mLzmaProps.mDictSize);
                TR("Lzma2Enc_SetProps:lc", props.mLzmaProps.mLC);
                TR("Lzma2Enc_SetProps:lp", props.mLzmaProps.mLP);
                TR("Lzma2Enc_SetProps:pb", props.mLzmaProps.mPB);
                TR("Lzma2Enc_SetProps:algo", props.mLzmaProps.mAlgo);
                TR("Lzma2Enc_SetProps:fb", props.mLzmaProps.mFB);
                TR("Lzma2Enc_SetProps:btMode", props.mLzmaProps.mBtMode);
                TR("Lzma2Enc_SetProps:numHashBytes", props.mLzmaProps.mNumHashBytes);
                TR("Lzma2Enc_SetProps:mc", props.mLzmaProps.mMC);
                TR("Lzma2Enc_SetProps:writeEndMark", props.mLzmaProps.mWriteEndMark);
                TR("Lzma2Enc_SetProps:numThreads", props.mLzmaProps.mNumThreads);
                TR("Lzma2Enc_SetProps:blockSize", checked((int)props.mBlockSize));
                TR("Lzma2Enc_SetProps:numBlockThreads", props.mNumBlockThreads);
                TR("Lzma2Enc_SetProps:numTotalThreads", props.mNumTotalThreads);

                CLzmaEncProps lzmaProps = new CLzmaEncProps(props.mLzmaProps);
                lzmaProps.LzmaEncProps_Normalize();
                if (lzmaProps.mLC + lzmaProps.mLP > LZMA2_LCLP_MAX)
                    return SZ_ERROR_PARAM;

                mProps = new CLzma2EncProps(props);
                mProps.Lzma2EncProps_Normalize();
                return SZ_OK;
            }

            private static uint LZMA2_DIC_SIZE_FROM_PROP(int p)
            {
                return (uint)(2 | (p & 1)) << (p / 2 + 11);
            }

            public byte Lzma2Enc_WriteProperties()
            {
                uint dicSize = mProps.mLzmaProps.LzmaEncProps_GetDictSize();

                int i = 0;
                while (i < 40 && dicSize > LZMA2_DIC_SIZE_FROM_PROP(i))
                    i++;

                TR("Lzma2Enc_WriteProperties", i);
                return (byte)i;
            }

            public SRes Lzma2Enc_Encode(ISeqOutStream outStream, ISeqInStream inStream, ICompressProgress progress)
            {
                for (int i = 0; i < mProps.mNumBlockThreads; i++)
                {
                    CLzma2EncInternal t = mCoders[i];
                    if (t.mEnc == null)
                    {
                        t.mEnc = LzmaEnc_Create(mAlloc);
                        if (t.mEnc == null)
                            return SZ_ERROR_MEM;
                    }
                }

#if !_7ZIP_ST
                if (mProps.mNumBlockThreads <= 1)
#endif
                    return mCoders[0].Lzma2Enc_EncodeMt1(this, outStream, inStream, progress);

#if !_7ZIP_ST
                mMtCoder.mProgress = progress;
                mMtCoder.mInStream = inStream;
                mMtCoder.mOutStream = outStream;
                mMtCoder.mAlloc = mAlloc;
                mMtCoder.mMtCallback = new CMtCallbackImp(this);

                mMtCoder.mBlockSize = mProps.mBlockSize;
                mMtCoder.mDestBlockSize = mProps.mBlockSize + (mProps.mBlockSize >> 10) + 16;
                mMtCoder.mNumThreads = mProps.mNumBlockThreads;

                return mMtCoder.MtCoder_Code();
#endif
            }

            #endregion
        }

#if !_7ZIP_ST
        internal sealed class CMtCallbackImp
        {
            #region Implementation

            private CLzma2Enc mLzma2Enc;

            internal CMtCallbackImp(CLzma2Enc enc)
            {
                mLzma2Enc = enc;
            }

            internal SRes Code(int index, P<byte> dest, ref long destSize, P<byte> src, long srcSize, bool finished)
            {
                CLzma2EncInternal p = mLzma2Enc.mCoders[index];

                SRes res = SZ_OK;
                long destLim = destSize;
                destSize = 0;

                if (srcSize != 0)
                {
                    if ((res = p.Lzma2EncInt_Init(mLzma2Enc.mProps)) != SZ_OK)
                        return res;

                    if ((res = p.mEnc.LzmaEnc_MemPrepare(src, srcSize, CLzma2EncInternal.LZMA2_KEEP_WINDOW_SIZE, mLzma2Enc.mAlloc, mLzma2Enc.mAllocBig)) != SZ_OK)
                        return res;

                    while (p.mSrcPos < (ulong)srcSize)
                    {
                        long packSize = destLim - destSize;

                        res = p.Lzma2EncInt_EncodeSubblock(dest + destSize, ref packSize, null);
                        if (res != SZ_OK)
                            break;

                        destSize += packSize;

                        if (packSize == 0)
                        {
                            res = SZ_ERROR_FAIL;
                            break;
                        }

                        if (mLzma2Enc.mMtCoder.mMtProgress.MtProgress_Set(index, p.mSrcPos, (ulong)destSize) != SZ_OK)
                        {
                            res = SZ_ERROR_PROGRESS;
                            break;
                        }
                    }

                    p.mEnc.LzmaEnc_Finish();
                    if (res != SZ_OK)
                        return res;
                }

                if (finished)
                {
                    if (destSize == destLim)
                        return SZ_ERROR_OUTPUT_EOF;

                    dest[destSize++] = 0;
                }

                return res;
            }

            #endregion
        }
#endif
    }
}
