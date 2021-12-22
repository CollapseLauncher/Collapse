using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        internal sealed class CLoopThread
        {
            #region Variables

            public CThread mThread;
            public CEvent mStartEvent;
            public CEvent mFinishedEvent;
            public bool mStop;

            public Action mFunc;
            //public CMtThread mParam;
            //public THREAD_FUNC_RET_TYPE mRes;

            #endregion

            #region Private Methods

            private void LoopThreadFunc()
            {
                for (;;)
                {
                    if (Event_Wait(mStartEvent) != 0)
                        return; // SZ_ERROR_THREAD;

                    Trace.MatchObjectWait(this, "LoopThreadFunc");
                    if (mStop)
                    {
                        Trace.MatchObjectDestroy(this, "LoopThreadFunc");
                        return; // 0;
                    }
                    Trace.MatchObjectWait(this, "LoopThreadFunc");

                    /* mRes = mFunc(mParam)*/
                    mFunc();

                    if (Event_Set(mFinishedEvent) != 0)
                        return; // SZ_ERROR_THREAD;
                }
            }

            #endregion

            #region Methods

            internal CLoopThread() // LoopThread_Construct
            {
                Thread_Construct(out mThread);
                Event_Construct(out mStartEvent);
                Event_Construct(out mFinishedEvent);
            }

            internal void LoopThread_Close()
            {
                Thread_Close(ref mThread);
                Event_Close(ref mStartEvent);
                Event_Close(ref mFinishedEvent);
            }

            internal SRes LoopThread_Create()
            {
                Trace.MatchObjectCreate(this, "LoopThread_Create");
                mStop = false;
                Trace.MatchObjectWait(this, "LoopThread_Create");

                SRes res;
                if ((res = AutoResetEvent_CreateNotSignaled(out mStartEvent)) != SZ_OK)
                    return res;
                if ((res = AutoResetEvent_CreateNotSignaled(out mFinishedEvent)) != SZ_OK)
                    return res;
                return Thread_Create(out mThread, LoopThreadFunc, "LZMA 2 Loop Thread");
            }

            internal SRes LoopThread_StopAndWait()
            {
                Trace.MatchObjectWait(this, "LoopThread_StopAndWait");
                mStop = true;
                Trace.MatchObjectWait(this, "LoopThread_StopAndWait");

                if (Event_Set(mStartEvent) != 0)
                    return SZ_ERROR_THREAD;

                return Thread_Wait(mThread);
            }

            internal SRes LoopThread_StartSubThread()
            {
                return Event_Set(mStartEvent);
            }

            internal SRes LoopThread_WaitSubThread()
            {
                return Event_Wait(mFinishedEvent);
            }

            #endregion
        }

#if !_7ZIP_ST
        public const int NUM_MT_CODER_THREADS_MAX = 32;
#else
        public const int NUM_MT_CODER_THREADS_MAX = 1;
#endif

        internal sealed class CMtProgress
        {
            #region Variables

            internal ulong mTotalInSize;
            internal ulong mTotalOutSize;
            internal ICompressProgress mProgress;
            internal SRes mRes;
            internal CCriticalSection mCS = new CCriticalSection();
            internal ulong[] mInSizes = new ulong[NUM_MT_CODER_THREADS_MAX];
            internal ulong[] mOutSizes = new ulong[NUM_MT_CODER_THREADS_MAX];

            #endregion

            #region Methods

            internal void MtProgress_Init(ICompressProgress progress)
            {
                for (uint i = 0; i < NUM_MT_CODER_THREADS_MAX; i++)
                    mInSizes[i] = mOutSizes[i] = 0;

                mTotalInSize = 0;
                mTotalOutSize = 0;
                mProgress = progress;
                mRes = SZ_OK;
            }

            internal void MtProgress_Reinit(int index)
            {
                mInSizes[index] = 0;
                mOutSizes[index] = 0;
            }

            private static void UPDATE_PROGRESS(ulong size, ref ulong prev, ref ulong total)
            {
                if (size != ~0ul)
                {
                    total += size - prev;
                    prev = size;
                }
            }

            private static SRes Progress(ICompressProgress p, ulong inSize, ulong outSize)
            {
                return (p != null && p.Progress(inSize, outSize) != SZ_OK) ? SZ_ERROR_PROGRESS : SZ_OK;
            }

            public SRes MtProgress_Set(int index, ulong inSize, ulong outSize)
            {
                CriticalSection_Enter(mCS);
                UPDATE_PROGRESS(inSize, ref mInSizes[index], ref mTotalInSize);
                UPDATE_PROGRESS(outSize, ref mOutSizes[index], ref mTotalOutSize);
                if (mRes == SZ_OK)
                    mRes = Progress(mProgress, mTotalInSize, mTotalOutSize);
                SRes res = mRes;
                CriticalSection_Leave(mCS);
                return res;
            }

            internal void MtProgress_SetError(SRes res)
            {
                CriticalSection_Enter(mCS);
                if (mRes == SZ_OK)
                    mRes = res;
                CriticalSection_Leave(mCS);
            }

            #endregion
        }

        internal sealed class CMtThread
        {
            #region Variables

            internal CMtCoder mMtCoder;
            internal byte[] mOutBuf;
            internal long mOutBufSize;
            internal byte[] mInBuf;
            internal long mInBufSize;
            internal int mIndex;
            internal CLoopThread mThread;

            internal bool mStopReading;
            internal bool mStopWriting;
            internal CEvent mCanRead;
            internal CEvent mCanWrite;

            #endregion

            #region Methods

            internal CMtThread(int index, CMtCoder mtCoder) // CMtThread_Construct
            {
                Trace.MatchObjectCreate(this, "CMtThread_Construct");

                mIndex = index;
                mMtCoder = mtCoder;
                mOutBuf = null;
                mInBuf = null;

                Event_Construct(out mCanRead);
                Event_Construct(out mCanWrite);

                mThread = new CLoopThread();
            }

            internal CMtThread GET_NEXT_THREAD()
            {
                int index = mIndex + 1;
                if (index == mMtCoder.mNumThreads)
                    index = 0;

                return mMtCoder.mThreads[index];
            }

            internal SRes MtThread_Process(out bool stop)
            {
                stop = true;
                if (Event_Wait(mCanRead) != 0)
                    return SZ_ERROR_THREAD;

                CMtThread next = GET_NEXT_THREAD();

                Trace.MatchObjectWait(this, "MtThread_Process");
                if (mStopReading)
                {
                    next.mStopReading = true;
                    Trace.MatchObjectWait(this, "MtThread_Process");
                    return Event_Set(next.mCanRead) == 0 ? SZ_OK : SZ_ERROR_THREAD;
                }
                Trace.MatchObjectWait(this, "MtThread_Process");

                {
                    long size = mMtCoder.mBlockSize;
                    long destSize = mOutBufSize;

                    SRes res;
                    if ((res = CMtCoder.FullRead(mMtCoder.mInStream, mInBuf, ref size)) != SZ_OK)
                        return res;

                    stop = (size != mMtCoder.mBlockSize);

                    Trace.MatchObjectWait(this, "MtThread_Process:2");
                    next.mStopReading = stop;
                    Trace.MatchObjectWait(this, "MtThread_Process:2");

                    if (Event_Set(next.mCanRead) != 0)
                        return SZ_ERROR_THREAD;

                    if ((res = mMtCoder.mMtCallback.Code(mIndex, mOutBuf, ref destSize, mInBuf, size, stop)) != SZ_OK)
                        return res;

                    mMtCoder.mMtProgress.MtProgress_Reinit(mIndex);

                    if (Event_Wait(mCanWrite) != 0)
                        return SZ_ERROR_THREAD;

                    Trace.MatchObjectWait(this, "MtThread_Process:3");
                    if (mStopWriting)
                    {
                        Trace.MatchObjectWait(this, "MtThread_Process:3");
                        return SZ_ERROR_FAIL;
                    }
                    Trace.MatchObjectWait(this, "MtThread_Process:3");

                    if (mMtCoder.mOutStream.Write(mOutBuf, destSize) != destSize)
                        return SZ_ERROR_WRITE;

                    return Event_Set(next.mCanWrite) == 0 ? SZ_OK : SZ_ERROR_THREAD;
                }
            }

            internal void ThreadFunc()
            {
                for (;;)
                {
                    CMtThread next = GET_NEXT_THREAD();

                    bool stop;
                    SRes res = MtThread_Process(out stop);
                    if (res != SZ_OK)
                    {
                        mMtCoder.MtCoder_SetError(res);
                        mMtCoder.mMtProgress.MtProgress_SetError(res);

                        Trace.MatchObjectWait(this, "ThreadFunc");
                        next.mStopReading = true;
                        next.mStopWriting = true;
                        Trace.MatchObjectWait(this, "ThreadFunc");

                        Event_Set(next.mCanRead);
                        Event_Set(next.mCanWrite);
                        return; // res;
                    }

                    if (stop)
                        return; // 0;
                }
            }

            #endregion

            #region Private Methods

            internal void CMtThread_CloseEvents()
            {
                Event_Close(ref mCanRead);
                Event_Close(ref mCanWrite);
            }

            internal void CMtThread_Destruct()
            {
                Trace.MatchObjectDestroy(this, "CMtThread_Destruct");
                CMtThread_CloseEvents();

                if (Thread_WasCreated(mThread.mThread))
                {
                    mThread.LoopThread_StopAndWait();
                    mThread.LoopThread_Close();
                }

                if (mMtCoder.mAlloc != null)
                    IAlloc_FreeBytes(mMtCoder.mAlloc, mOutBuf);

                mOutBuf = null;

                if (mMtCoder.mAlloc != null)
                    IAlloc_FreeBytes(mMtCoder.mAlloc, mInBuf);

                mInBuf = null;
            }

            internal SRes CMtThread_Prepare()
            {
                if (mInBuf == null || mInBufSize != mMtCoder.mBlockSize)
                {
                    IAlloc_FreeBytes(mMtCoder.mAlloc, mInBuf);
                    mInBufSize = mMtCoder.mBlockSize;
                    mInBuf = IAlloc_AllocBytes(mMtCoder.mAlloc, mInBufSize);
                    if (mInBuf == null)
                        return SZ_ERROR_MEM;
                }

                if (mOutBuf == null || mOutBufSize != mMtCoder.mDestBlockSize)
                {
                    IAlloc_FreeBytes(mMtCoder.mAlloc, mOutBuf);
                    mOutBufSize = mMtCoder.mDestBlockSize;
                    mOutBuf = IAlloc_AllocBytes(mMtCoder.mAlloc, mOutBufSize);
                    if (mOutBuf == null)
                        return SZ_ERROR_MEM;
                }

                Trace.MatchObjectWait(this, "CMtThread_Prepare");
                mStopReading = false;
                mStopWriting = false;
                Trace.MatchObjectWait(this, "CMtThread_Prepare");

                if (AutoResetEvent_CreateNotSignaled(out mCanRead) != SZ_OK)
                    return SZ_ERROR_THREAD;

                if (AutoResetEvent_CreateNotSignaled(out mCanWrite) != SZ_OK)
                    return SZ_ERROR_THREAD;

                return SZ_OK;
            }

            #endregion
        }

        internal sealed class CMtCoder
        {
            #region Variables

            public long mBlockSize;
            public long mDestBlockSize;
            public int mNumThreads;

            public ISeqInStream mInStream;
            public ISeqOutStream mOutStream;
            public ICompressProgress mProgress;
            public ISzAlloc mAlloc;

            public CMtCallbackImp mMtCallback;
            public CCriticalSection mCS = new CCriticalSection();
            public SRes mRes;

            public CMtProgress mMtProgress = new CMtProgress();
            public CMtThread[] mThreads;

            #endregion

            #region Internal Methods

            internal CMtCoder() // MtCoder_Construct
            {
                mAlloc = null;

                mThreads = new CMtThread[NUM_MT_CODER_THREADS_MAX];
                for (int i = 0; i < mThreads.Length; i++)
                    mThreads[i] = new CMtThread(i, this);

                CriticalSection_Init(out mCS);
                CriticalSection_Init(out mMtProgress.mCS);
            }

            internal void MtCoder_Destruct()
            {
                for (uint i = 0; i < mThreads.Length; i++)
                    mThreads[i].CMtThread_Destruct();

                CriticalSection_Delete(mCS);
                CriticalSection_Delete(mMtProgress.mCS);
            }

            internal SRes MtCoder_Code()
            {
                int numThreads = mNumThreads;
                SRes res = SZ_OK;
                mRes = SZ_OK;

                mMtProgress.MtProgress_Init(mProgress);

                for (uint i = 0; i < numThreads; i++)
                {
                    if ((res = mThreads[i].CMtThread_Prepare()) != SZ_OK)
                        return res;
                }

                for (uint i = 0; i < numThreads; i++)
                {
                    CMtThread t = mThreads[i];
                    CLoopThread lt = t.mThread;

                    if (!Thread_WasCreated(lt.mThread))
                    {
                        lt.mFunc = t.ThreadFunc;
                        if (lt.LoopThread_Create() != SZ_OK)
                        {
                            res = SZ_ERROR_THREAD;
                            break;
                        }
                    }
                }

                if (res == SZ_OK)
                {
                    int i;
                    for (i = 0; i < numThreads; i++)
                    {
                        CMtThread t = mThreads[i];
                        if (t.mThread.LoopThread_StartSubThread() != SZ_OK)
                        {
                            res = SZ_ERROR_THREAD;
                            Trace.MatchObjectWait(mThreads[0], "MtCoder_Code");
                            mThreads[0].mStopReading = true;
                            Trace.MatchObjectWait(mThreads[0], "MtCoder_Code");
                            break;
                        }
                    }

                    Event_Set(mThreads[0].mCanWrite);
                    Event_Set(mThreads[0].mCanRead);

                    for (int j = 0; j < i; j++)
                        mThreads[j].mThread.LoopThread_WaitSubThread();
                }

                for (uint i = 0; i < numThreads; i++)
                    mThreads[i].CMtThread_CloseEvents();

                return (res == SZ_OK) ? mRes : res;
            }

            #endregion

            #region Methods

            internal void MtCoder_SetError(SRes res)
            {
                CriticalSection_Enter(mCS);
                if (mRes == SZ_OK)
                    mRes = res;
                CriticalSection_Leave(mCS);
            }

            internal static SRes FullRead(ISeqInStream stream, P<byte> data, ref long processedSize)
            {
                long size = processedSize;
                processedSize = 0;
                while (size != 0)
                {
                    long curSize = size;
                    SRes res = stream.Read(data, ref curSize);
                    processedSize += curSize;
                    data += curSize;
                    size -= curSize;
                    if (res != SZ_OK)
                        return res;
                    if (curSize == 0)
                        return SZ_OK;
                }
                return SZ_OK;
            }

            #endregion
        }
    }
}
