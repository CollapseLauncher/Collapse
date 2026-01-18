using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Extension
{
    public static class DispatcherQueueExtensions
    {
        static DispatcherQueueExtensions()
        {
            CurrentDispatcherQueue ??= DispatcherQueue.GetForCurrentThread();
        }

        public static DispatcherQueue CurrentDispatcherQueue
        {
            get;
            set;
        }

        public static bool HasThreadAccessSafe(this DispatcherQueue? queue)
        {
            if (queue == null) return false;

            try
            {
                return queue.HasThreadAccess;
            }
            catch (ObjectDisposedException)
            {
                return false; // Return false if an exception occurs
            }
        }

        public static Task<T> CreateObjectFromUIThread<T>()
            where T : class, new()
        {
            if (CurrentDispatcherQueue.HasThreadAccessSafe())
            {
                return Task.FromResult(new T());
            }

            TaskCompletionSource<T> tcs = new();
            Impl();

            return tcs.Task;

            void Impl()
            {
                try
                {
                    CurrentDispatcherQueue.TryEnqueue(() => tcs.SetResult(new T()));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }
        }
    }
}