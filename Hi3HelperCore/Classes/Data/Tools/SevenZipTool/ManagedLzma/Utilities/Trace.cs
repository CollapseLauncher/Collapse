using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Globalization;
using D = System.Diagnostics;

namespace ManagedLzma.LZMA
{
    namespace Master
    {
        partial class LZMA
        {
            private static void TR(string str, int arg)
            {
#if !DISABLE_TRACE
                Trace.Match(str, arg);
#endif
            }

            private static void TR(string str, uint arg)
            {
#if !DISABLE_TRACE
                Trace.Match(str, arg);
#endif
            }

            private static void TRS(string str1, string str2)
            {
#if !DISABLE_TRACE
                Trace.Match(str1, str2);
#endif
            }

            private static SRes TSZ(string kind)
            {
#if DISABLE_TRACE
                return SZ_OK;
#else
                return new SRes(Trace.MatchStatusCode(kind));
#endif
            }
        }
    }

    internal static class Trace
    {
#if !DISABLE_TRACE

        #region Constants

        private const int CMD_INIT_TRACE = 10;
        private const int CMD_STRING_MAP = 11;

        private const int CMD_THREAD_CTOR = 100;
        private const int CMD_THREAD_DTOR = 101;
        private const int CMD_THREAD_WAIT = 102;

        private const int CMD_OBJECT_CTOR = 110;
        private const int CMD_OBJECT_DTOR = 111;
        private const int CMD_OBJECT_WAIT1 = 112;

        private const int CMD_STATUS_CODE = 120;

        #endregion

        private sealed class Session : IDisposable
        {
            #region Inner Class - Record

            private const int kInvalidId = 0xBADF00D;
            private const int kAllocId = unchecked((int)0xCAFEBABE);
            private const int kReleaseId = unchecked((int)0xBEEFBEEF);

            private struct Record
            {
                private int mThreadHandle;
                private int mArg1;
                private int mArg2;

                public Record(int threadId, int arg1, int arg2)
                {
                    mThreadHandle = threadId;
                    mArg1 = arg1;
                    mArg2 = arg2;
                }

                public bool CheckThread(int threadId)
                {
                    return mThreadHandle == threadId;
                }

                public void Match(int threadId, int arg1, int arg2)
                {
                    if (mThreadHandle != threadId || mArg1 != arg1 || mArg2 != arg2)
                        throw new InvalidDataException();
                }
            }

            #endregion

            #region Variables

            internal string mKey;
            private Thread mPipeThread;

            private byte mSyncCount = 0xAB;

            private object mSync = new object();
            private int mMainThreadId;
            private Thread mMainThread = Thread.CurrentThread;
            private List<Thread> mRunningThreads = new List<Thread>();
            private List<Exception> mError = new List<Exception>();
            private bool mShutdown;

            private object mStringSync = new object();
            private Dictionary<int, string> mStringById = new Dictionary<int, string>();
            private Dictionary<string, int> mIdByString = new Dictionary<string, int>();

            private object mObjectSync = new object();
            private Dictionary<object, int> mObjectHandleMap = new Dictionary<object, int>();
            private Dictionary<int, Queue<Record>> mRecordByObject = new Dictionary<int, Queue<Record>>();

            private object mThreadSync = new object();
            private Dictionary<Thread, int> mThreadHandleMap = new Dictionary<Thread, int>();

            #endregion

            #region Public Methods

            public Session(string id)
            {
                mKey = id;
                mPipeThread = new Thread(PipeThread) { Name = "ManagedTracePipeThread" };
                mPipeThread.Start(id);
            }

            public Context InitMainThread()
            {
                if (Thread.CurrentThread != mMainThread)
                    throw new InvalidOperationException();

                int threadId;
                lock (mSync)
                    while ((threadId = mMainThreadId) == 0)
                        Monitor.Wait(mSync);

                Thread thread = Thread.CurrentThread;
                lock (mThreadSync)
                    mThreadHandleMap.Add(thread, threadId);

                return new Context(this, threadId, true);
            }

            public void EnsureEnd()
            {
                if (Thread.CurrentThread != mMainThread)
                    throw new InvalidOperationException();

                mContext.EnsureEnd();

                mPipeThread.Join();

                lock (mSync)
                {
                    while (mRunningThreads.Count != 0)
                        Monitor.Wait(mSync);

                    if (mError.Count != 0)
                        throw new Exception(string.Format(CultureInfo.InvariantCulture, "Recorded {0} exceptions.", mError.Count), mError[0]);
                }
            }

            public void Dispose()
            {
                if (Thread.CurrentThread != mMainThread)
                    throw new InvalidOperationException();

                lock (mSync)
                    mShutdown = true;

                mPipeThread.Join();

                lock (mSync)
                {
                    while (mRunningThreads.Count != 0)
                    {
                        foreach (Thread thread in mRunningThreads)
                        {
                            var state = thread.ThreadState;
                            if (state == ThreadState.WaitSleepJoin || state == ThreadState.Running)
                                thread.Abort();
                            else
                            {
                                if (System.Diagnostics.Debugger.IsAttached)
                                    System.Diagnostics.Debugger.Break();
                                else
                                    System.Diagnostics.Debugger.Launch();
                            }
                        }

                        Monitor.Wait(mSync);
                    }
                }

                mContext.Dispose();
                mContext = null;
            }

            public int GetThreadId()
            {
                return GetThreadId(Thread.CurrentThread);
            }

            public int GetThreadId(Thread thread)
            {
                lock (mThreadSync)
                    return mThreadHandleMap[thread];
            }

            public string GetString(int id)
            {
                lock (mStringSync)
                {
                    string text;
                    while (!mStringById.TryGetValue(id, out text))
                        Monitor.Wait(mStringSync);

                    return text;
                }
            }

            public int GetStringId(string str)
            {
                lock (mStringSync)
                {
                    int id;
                    while (!mIdByString.TryGetValue(str, out id))
                        Monitor.Wait(mStringSync);

                    return id;
                }
            }

            #endregion

            #region Public Methods - Matching

            public Thread MatchThreadStart(int threadId, Action fun)
            {
                Thread thread = new Thread(TracedThread) { Name = "TracedManagedThread[" + threadId.ToString() + "]" };
                mThreadHandleMap.Add(thread, threadId);
                thread.Start(fun);
                return thread;
            }

            public void MatchThreadWait(Thread thread, int threadId)
            {
                lock (mThreadSync)
                    if (mThreadHandleMap[thread] != threadId)
                        throw new InvalidDataException();

                thread.Join();
            }

            public void MatchThreadClose(Thread thread, int threadId)
            {
                if (thread.ThreadState != ThreadState.Stopped)
                    throw new InvalidDataException();

                lock (mThreadSync)
                {
                    if (mThreadHandleMap[thread] != threadId)
                        throw new InvalidDataException();

                    mThreadHandleMap.Remove(thread);
                }
            }

            public void MatchObjectCTor(object obj, int objectId, int arg)
            {
                lock (mObjectSync)
                {
                    mObjectHandleMap.Add(obj, objectId);
                    mRecordByObject.Add(objectId, new Queue<Record>());
                    Monitor.PulseAll(mObjectSync);
                }
            }

            public void MatchObjectDTor(object obj, string argStr, out int argId)
            {
                var threadId = GetThreadId();

                Queue<Record> queue;
                lock (mObjectSync)
                {
                    int objId;
                    while (!mObjectHandleMap.TryGetValue(obj, out objId))
                        Monitor.Wait(mObjectSync);

                    while ((queue = mRecordByObject[objId]).Count == 0
                        || !queue.Peek().CheckThread(threadId))
                        Monitor.Wait(mObjectSync);

                    mObjectHandleMap.Remove(obj);
                    mRecordByObject.Remove(objId);
                }

                argId = GetStringId(argStr);
                queue.Dequeue().Match(threadId, argId, kReleaseId);

                if (queue.Count != 0)
                    throw new InvalidDataException();
            }

            private Record GetNextRecord(int threadId, object obj)
            {
                Record record;
                lock (mObjectSync)
                {
                    int objId;
                    while (!mObjectHandleMap.TryGetValue(obj, out objId))
                        Monitor.Wait(mObjectSync);

                    Queue<Record> queue;
                    while ((queue = mRecordByObject[objId]).Count == 0
                        || !queue.Peek().CheckThread(threadId))
                        Monitor.Wait(mObjectSync);

                    record = queue.Dequeue();
                    Monitor.PulseAll(mObjectSync);
                }

                return record;
            }

            public void MatchObjectWait(object obj, int arg1)
            {
                var threadId = GetThreadId();
                GetNextRecord(threadId, obj).Match(threadId, arg1, kInvalidId);
            }

            #endregion

            #region Private Methods

            private void TracedThread(object fun)
            {
                Thread thread = Thread.CurrentThread;
                try
                {
                    lock (mSync)
                        mRunningThreads.Add(thread);

                    using (mContext = new Context(this, GetThreadId(thread), false))
                    {
                        ((Action)fun)();
                        mContext.EnsureEnd();
                    }
                }
                catch (Exception ex)
                {
                    lock (mSync)
                        mError.Add(ex);
                }
                finally
                {
                    lock (mSync)
                    {
                        mRunningThreads.Remove(thread);
                        Monitor.PulseAll(mSync);
                    }

                    mContext = null;
                }
            }

            private void PipeThread(object id)
            {
                try
                {
                    using (var pipe = new NamedPipeClientStream(".", (string)id + "\\root", PipeDirection.In))
                    {
                        pipe.Connect();

                        BinaryReader rd = new BinaryReader(pipe);

                        for (;;)
                        {
                            lock (mSync)
                                if (mShutdown)
                                    return;

                            int op = pipe.ReadByte();
                            if (op < 0)
                                return;

                            ProcessPipeThreadEvent(rd, op);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (mSync)
                        mError.Add(ex);
                }
            }

            private void SyncCommandEnd(BinaryReader rd)
            {
                byte num = rd.ReadByte();
                if (num != mSyncCount)
                    throw new InvalidDataException();
                mSyncCount++;
            }

            private void ProcessPipeThreadEvent(BinaryReader rd, int op)
            {
                switch (op)
                {
                    case CMD_INIT_TRACE:
                        {
                            lock (mSync)
                            {
                                if (mMainThreadId != 0)
                                    throw new InvalidDataException();

                                mMainThreadId = rd.ReadInt32();
                                SyncCommandEnd(rd);

                                if (mMainThreadId == 0)
                                    throw new InvalidDataException();

                                Monitor.PulseAll(mSync);
                            }
                        }
                        break;
                    case CMD_STRING_MAP:
                        {
                            int len = rd.ReadUInt16();
                            string text = Encoding.ASCII.GetString(rd.ReadBytes(len));

                            lock (mStringSync)
                            {
                                int id = mStringById.Count + 1;
                                mStringById.Add(id, text);
                                mIdByString.Add(text, id);
                                Monitor.PulseAll(mStringSync);
                            }
                        }
                        break;
                    case CMD_OBJECT_DTOR:
                        {
                            int thread = rd.ReadInt32();
                            int handle = rd.ReadInt32();
                            int arg = rd.ReadInt32();
                            SyncCommandEnd(rd);

                            lock (mObjectSync)
                            {
                                mRecordByObject[handle].Enqueue(new Record(thread, arg, kReleaseId));
                                Monitor.PulseAll(mObjectSync);
                            }
                        }
                        break;
                    case CMD_OBJECT_WAIT1:
                        {
                            int thread = rd.ReadInt32();
                            int handle = rd.ReadInt32();
                            int arg = rd.ReadInt32();
                            SyncCommandEnd(rd);

                            lock (mObjectSync)
                            {
                                mRecordByObject[handle].Enqueue(new Record(thread, arg, kInvalidId));
                                Monitor.PulseAll(mObjectSync);
                            }
                        }
                        break;
                }
            }

            #endregion
        }

        private sealed class Context : IDisposable
        {
            #region Inner Class - Arg Flags

            [Flags]
            private enum ArgFlags : byte
            {
                None = 0,
                Int1 = 0x01,
                Str1 = 0x02,
                Int2 = 0x04,
                Str2 = 0x08,
                Int3 = 0x10,
                Str3 = 0x20,
                Escape = 0x80,
            }

            #endregion

            #region Variables

            private Session mSession;
            private string mKey;
            private NamedPipeClientStream mPipe;
            private BinaryReader mReader;
            private BinaryWriter mWriter;
            private Dictionary<int, string> mStringById = new Dictionary<int, string>();
            private Dictionary<string, int> mIdByString = new Dictionary<string, int>();
            private byte mSyncCount = 0xAB;
            private bool mIsMainThread;

            #endregion

            #region Public Methods

            public Context(Session session, int id, bool isMainThread)
            {
                mIsMainThread = isMainThread;
                mSession = session;
                mKey = mSession.mKey + "\\" + id.ToString("x8");
                mPipe = new NamedPipeClientStream(".", mKey, PipeDirection.InOut);
                mPipe.Connect();
                mReader = new BinaryReader(mPipe);
                mWriter = new BinaryWriter(mPipe);
            }

            public void Dispose()
            {
                if (mPipe != null)
                {
                    mPipe.Close();
                    mPipe = null;
                }
            }

            public void EnsureEnd()
            {
                SendAck(0xFF, 0xBADF00D);

                if (mPipe.ReadByte() != -1)
                    throw new InvalidDataException();
            }

            public Session Session
            {
                get { return mSession; }
            }

            public bool IsMainThread
            {
                get { return mIsMainThread; }
            }

            public Thread MatchThreadStart(Action fun)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Escape | (ArgFlags)CMD_THREAD_CTOR))
                    throw new InvalidDataException();

                int threadId = mReader.ReadInt32();
                SyncCommands();

                Thread thread = mSession.MatchThreadStart(threadId, fun);
                SendAck((byte)cmd, threadId);
                return thread;
            }

            public void MatchThreadWait(Thread thread)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Escape | (ArgFlags)CMD_THREAD_WAIT))
                    throw new InvalidDataException();

                int threadId = mReader.ReadInt32();
                SyncCommands();

                mSession.MatchThreadWait(thread, threadId);
                SendAck((byte)cmd, threadId);
            }

            public void MatchThreadClose(Thread thread)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Escape | (ArgFlags)CMD_THREAD_DTOR))
                    throw new InvalidDataException();

                int threadId = mReader.ReadInt32();
                SyncCommands();

                mSession.MatchThreadClose(thread, threadId);
                SendAck((byte)cmd, threadId);
            }

            public void MatchObjectCreate(object obj, string arg)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Escape | (ArgFlags)CMD_OBJECT_CTOR))
                    throw new InvalidDataException();

                int objectId = mReader.ReadInt32();
                int strId = mReader.ReadInt32();
                SyncCommands();

                string str = GetString(strId);
                if (str != arg)
                    throw new InvalidDataException();

                int id = GetStringId(arg);
                mSession.MatchObjectCTor(obj, objectId, id);
                SendAck((byte)cmd, id);
            }

            public void MatchObjectDestroy(object obj, string arg)
            {
                int id;
                mSession.MatchObjectDTor(obj, arg, out id);
                SendAck(CMD_OBJECT_DTOR, id);
            }

            public void MatchObjectWait(object obj, string arg1)
            {
                int id = GetStringId(arg1);
                mSession.MatchObjectWait(obj, id);
                SendAck(CMD_OBJECT_WAIT1, id);
            }

            public int MatchStatusCode(string arg)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Escape | (ArgFlags)CMD_STATUS_CODE))
                    throw new InvalidDataException();

                int str1 = mReader.ReadInt32();
                int res = mReader.ReadInt32();
                SyncCommands();

                CheckString(arg, str1);

                SendAck((byte)cmd, str1 ^ res);
                return res;
            }

            public void Match(string arg1, string arg2)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Str1 | ArgFlags.Str2))
                    throw new InvalidDataException();

                int str1 = mReader.ReadInt32();
                int str2 = mReader.ReadInt32();
                SyncCommands();

                CheckString(arg1, str1);
                CheckString(arg2, str2);
                SendAck((byte)cmd, str1 ^ str2);
            }

            public void Match(string arg1, int arg2)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Str1 | ArgFlags.Int2))
                    throw new InvalidDataException();

                int str1 = mReader.ReadInt32();
                int str2 = mReader.ReadInt32();
                SyncCommands();

                CheckString(arg1, str1);

                if (arg2 != str2)
                    throw new InvalidDataException();

                SendAck((byte)cmd, str1 ^ str2);
            }

            public void Match(int arg1, int arg2)
            {
                var cmd = (ArgFlags)mReader.ReadByte();
                if (cmd != (ArgFlags.Int1 | ArgFlags.Int2))
                    throw new InvalidDataException();

                int str1 = mReader.ReadInt32();
                int str2 = mReader.ReadInt32();
                SyncCommands();

                if (arg1 != str1)
                    throw new InvalidDataException();

                if (arg2 != str2)
                    throw new InvalidDataException();

                SendAck((byte)cmd, str1 ^ str2);
            }

            #endregion

            #region Private Methods

            private void SendAck(byte cmd, int arg)
            {
                mWriter.Write(cmd);
                mWriter.Write(arg);
            }

            private void SyncCommands()
            {
                byte num = mReader.ReadByte();
                if (num != mSyncCount)
                    throw new InvalidDataException();
                mSyncCount++;
            }

            private int GetStringId(string str)
            {
                if (str == null)
                    return 0;

                int id;
                if (!mIdByString.TryGetValue(str, out id))
                {
                    id = mSession.GetStringId(str);
                    mStringById.Add(id, str);
                    mIdByString.Add(str, id);
                }

                return id;
            }

            private string GetString(int id)
            {
                if (id < 0)
                    throw new InvalidDataException();

                if (id == 0)
                    return null;

                string str;
                if (!mStringById.TryGetValue(id, out str))
                {
                    str = mSession.GetString(id);
                    mStringById.Add(id, str);
                    mIdByString.Add(str, id);
                }

                return str;
            }

            private void CheckString(string str, int arg)
            {
                string argStr = GetString(arg);
                if (argStr != str)
                    throw new InvalidDataException();
            }

            #endregion
        }

        [ThreadStatic]
        private static Context mContext;

#endif

        #region Public Methods

        public static void InitSession(Guid id)
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            if (mContext != null)
                throw new InvalidOperationException("This thread is already traced.");

            mContext = new Session(@"LZMA\TEST\" + id.ToString("N")).InitMainThread();
#endif
        }

        public static void StopSession()
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            if (mContext == null)
                throw new InvalidOperationException("This thread is not traced.");

            if (!mContext.IsMainThread)
                throw new InvalidOperationException("This thread is not the main thread of the session.");

            Session root = mContext.Session;
            try { root.EnsureEnd(); }
            finally { root.Dispose(); }
#endif
        }

#if !DISABLE_TRACE
        public static Thread MatchThreadStart(Action fun)
        {
            return mContext.MatchThreadStart(fun);
        }
#endif

#if !DISABLE_TRACE
        public static void MatchThreadWait(Thread thread)
        {
            mContext.MatchThreadWait(thread);
        }
#endif

#if !DISABLE_TRACE
        public static void MatchThreadClose(Thread thread)
        {
            mContext.MatchThreadClose(thread);
        }
#endif

        public static void MatchObjectCreate(object obj, string arg)
        {
#if !DISABLE_TRACE
            mContext.MatchObjectCreate(obj, arg);
#endif
        }

        public static void MatchObjectDestroy(object obj, string arg)
        {
#if !DISABLE_TRACE
            mContext.MatchObjectDestroy(obj, arg);
#endif
        }

        public static void MatchObjectWait(object obj, string arg)
        {
#if !DISABLE_TRACE
            mContext.MatchObjectWait(obj, arg);
#endif
        }

        public static int MatchStatusCode(string arg)
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            return mContext.MatchStatusCode(arg);
#endif
        }

        public static void Match(int arg1, int arg2 = 0)
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            mContext.Match(arg1, arg2);
#endif
        }

        public static void Match(string arg1, int arg2 = 0)
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            mContext.Match(arg1, arg2);
#endif
        }

        public static void Match(string arg1, uint arg2)
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            mContext.Match(arg1, (int)arg2);
#endif
        }

        public static void Match(string arg1, string arg2)
        {
#if DISABLE_TRACE
            throw new NotSupportedException();
#else
            mContext.Match(arg1, arg2);
#endif
        }

        #endregion

        internal static void AllocSmallObject(string type, Master.LZMA.ISzAlloc alloc)
        {
        }
    }
}
