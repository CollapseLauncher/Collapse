using Microsoft.UI.Dispatching;
using System;

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

        public static T CreateObjectFromUIThread<T>()
            where T : class, new()
        {
            if (CurrentDispatcherQueue.HasThreadAccessSafe())
            {
                return new T();
            }

            T? obj = null;
            CurrentDispatcherQueue.TryEnqueue(() => obj = new T());

            if (obj == null)
            {
                throw new NullReferenceException("Object unexpectedly cannot be created.");
            }

            return obj;
        }
    }
}