using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Extension;

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

    public static bool HasThreadAccessSafe()
        => CurrentDispatcherQueue.HasThreadAccessSafe();

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
        where T : new()
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

    public static void TryEnqueue(DispatcherQueueHandler action)
        => TryEnqueue(DispatcherQueuePriority.Normal, action);

    public static void TryEnqueue(DispatcherQueuePriority priority, DispatcherQueueHandler action)
    {
        if (HasThreadAccessSafe())
        {
            action();
            return;
        }

        CurrentDispatcherQueue.TryEnqueue(priority, action);
    }

    public static T TryEnqueue<T>(Func<T> retAction)
        => TryEnqueue(DispatcherQueuePriority.Normal, retAction);

    public static T TryEnqueue<T>(DispatcherQueuePriority priority, Func<T> retAction)
    {
        if (HasThreadAccessSafe())
        {
            return retAction();
        }

        TaskCompletionSource<T> tcs = new();
        CurrentDispatcherQueue.TryEnqueue(priority, Impl);

        return tcs.Task.Result;

        void Impl()
        {
            try
            {
                tcs.SetResult(retAction());
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        }
    }
}