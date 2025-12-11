using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper;

internal class ThreadObjectPool<T> : IDisposable where T : class
{
    private ConcurrentQueue<T> Items = new();
    private SemaphoreSlim Semaphore;
    private Func<object?> _factory;

    private int _countUsed;
    private readonly int _capacity;
    private readonly bool _isDisposeObjects;
    private bool _isDisposed;

    internal ThreadObjectPool(Func<T> factory, int capacity = 0, bool isDisposeObjects = true)
    {
        _capacity = capacity == 0 ? Environment.ProcessorCount : capacity;
        _factory = factory;
        _isDisposeObjects = isDisposeObjects;
        Semaphore = new SemaphoreSlim(_capacity, _capacity);
    }

    internal ThreadObjectPool(Task<T> factoryAsync, int capacity = 0, bool isDisposeObjects = true)
    {
        _capacity = capacity == 0 ? Environment.ProcessorCount : capacity;
        _factory = () => factoryAsync;
        _isDisposeObjects = isDisposeObjects;
        Semaphore = new SemaphoreSlim(_capacity, _capacity);
    }

    internal async Task<T> GetOrCreateObjectAsync(CancellationToken token = default)
    {
        await Semaphore.WaitAsync(token);
        Interlocked.Increment(ref _countUsed);

        if (Items.TryDequeue(out T? pooled))
            return pooled;

        if (_factory is Func<Task<T>> asyncFactory)
            return await asyncFactory();

        return ((Func<T>)_factory)();
    }

    internal T GetOrCreateObject()
    {
        Semaphore.Wait();
        Interlocked.Increment(ref _countUsed);

        if (Items.TryDequeue(out T? pooled))
            return pooled;

        if (_factory is Func<Task<T>> asyncFactory)
            return asyncFactory().Result;

        return ((Func<T>)_factory)();
    }

    internal void Return(T item)
    {
        Items.Enqueue(item);
        Interlocked.Decrement(ref _countUsed);
        Semaphore.Release();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Semaphore.Dispose();

        try
        {
            if (!_isDisposeObjects)
            {
                return;
            }

            foreach (IDisposable item in Items.OfType<IDisposable>())
            {
                item.Dispose();
            }
        }
        finally
        {
            _isDisposed = true;
            Items.Clear();
            _factory = null!;
            Items = null!;
            Semaphore = null!;
        }
    }
}
