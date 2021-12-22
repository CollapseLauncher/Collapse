using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA.Master
{
    partial class LZMA
    {
        #region CThread

        internal sealed class CThread
        {
#if BUILD_PORTABLE
            internal System.Threading.Tasks.Task _task;
#else
            internal System.Threading.Thread _thread;
#endif
        }

        internal static void Thread_Construct(out CThread p)
        {
            p = null;
        }

        internal static bool Thread_WasCreated(CThread p)
        {
            return p != null;
        }

        internal static void Thread_Close(ref CThread p)
        {
            if (p != null)
            {
#if !DISABLE_TRACE
                Trace.MatchThreadClose(p._thread);
#elif BUILD_PORTABLE
                p._task.GetAwaiter().GetResult();
#else
                p._thread.Join();
#endif
            }

            p = null;
        }

        internal static SRes Thread_Wait(CThread p)
        {
#if BUILD_PORTABLE
            p._task.GetAwaiter().GetResult();
#else
            p._thread.Join();
#endif

#if !DISABLE_TRACE
            Trace.MatchThreadWait(p._thread);
#endif

            return TSZ("Thread_Wait");
        }

        internal static SRes Thread_Create(out CThread p, Action func, string threadName)
        {
            p = new CThread();
#if !DISABLE_TRACE
            p._thread = Trace.MatchThreadStart(func);
#elif BUILD_PORTABLE
            p._task = System.Threading.Tasks.Task.Run(func);
#else
            p._thread = new System.Threading.Thread(delegate () { func(); });
            p._thread.Name = threadName;
            p._thread.Start();
#endif
            return TSZ("Thread_Create");
        }

        #endregion

        #region CEvent

        // this is a win32 autoreset event

        internal sealed class CEvent
        {
#if DISABLE_TRACE
            public System.Threading.AutoResetEvent Event;
#endif
        }

        internal static void Event_Construct(out CEvent p)
        {
            p = null;
        }

        internal static bool Event_IsCreated(CEvent p)
        {
            return p != null;
        }

        internal static void Event_Close(ref CEvent p)
        {
            if (p != null)
            {
#if !DISABLE_TRACE
                Trace.MatchObjectDestroy(p, "Event_Close");
#elif BUILD_PORTABLE
                p.Event.Dispose();
#else
                p.Event.Close();
#endif
            }
            p = null;
        }

        internal static SRes Event_Wait(CEvent p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "Event_Wait");
#else
            p.Event.WaitOne();
#endif
            return TSZ("Event_Wait");
        }

        internal static SRes Event_Set(CEvent p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "Event_Set");
#else
            p.Event.Set();
#endif
            return TSZ("Event_Set");
        }

        internal static SRes Event_Reset(CEvent p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "Event_Reset");
#else
            p.Event.Reset();
#endif
            return TSZ("Event_Reset");
        }

        internal static SRes AutoResetEvent_CreateNotSignaled(out CEvent p)
        {
            p = new CEvent();
#if !DISABLE_TRACE
            Trace.MatchObjectCreate(p, "Event_Create");
#else
            p.Event = new System.Threading.AutoResetEvent(false);
#endif
            return TSZ("Event_Create");
        }

        #endregion

        #region CSemaphore

        internal sealed class CSemaphore
        {
#if DISABLE_TRACE
            public System.Threading.Semaphore Semaphore;
#endif
        }

        internal static void Semaphore_Construct(out CSemaphore p)
        {
            p = null;
        }

        internal static void Semaphore_Close(ref CSemaphore p)
        {
            if (p != null)
            {
#if !DISABLE_TRACE
                Trace.MatchObjectDestroy(p, "Semaphore_Close");
#elif BUILD_PORTABLE
                p.Semaphore.Dispose();
#else
                p.Semaphore.Close();
#endif
            }

            p = null;
        }

        internal static SRes Semaphore_Wait(CSemaphore p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "Semaphore_Wait");
#else
            p.Semaphore.WaitOne();
#endif
            return TSZ("Semaphore_Wait");
        }

        internal static SRes Semaphore_Create(out CSemaphore p, uint initCount, uint maxCount)
        {
            p = new CSemaphore();
#if !DISABLE_TRACE
            Trace.MatchObjectCreate(p, "Semaphore_Create");
            Trace.Match((int)initCount, (int)maxCount);
#else
            p.Semaphore = new System.Threading.Semaphore(checked((int)initCount), checked((int)maxCount));
#endif
            return TSZ("Semaphore_Create");
        }

        internal static SRes Semaphore_Release1(CSemaphore p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "Semaphore_Release");
#else
            p.Semaphore.Release();
#endif
            return TSZ("Semaphore_Release");
        }

        #endregion

        #region CCriticalSection

        internal sealed class CCriticalSection { }

        internal static SRes CriticalSection_Init(out CCriticalSection p)
        {
            p = new CCriticalSection();
#if !DISABLE_TRACE
            Trace.MatchObjectCreate(p, "CriticalSection_Init");
#endif
            return SZ_OK; // never fails in C code either
        }

        internal static void CriticalSection_Delete(CCriticalSection p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectDestroy(p, "CriticalSection_Delete");
#endif
        }

        internal static void CriticalSection_Enter(CCriticalSection p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "CriticalSection_Enter");
#else
            System.Threading.Monitor.Enter(p);
#endif
        }

        internal static void CriticalSection_Leave(CCriticalSection p)
        {
#if !DISABLE_TRACE
            Trace.MatchObjectWait(p, "CriticalSection_Leave");
#else
            System.Threading.Monitor.Exit(p);
#endif
        }

        #endregion
    }
}
