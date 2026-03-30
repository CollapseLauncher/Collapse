using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper;

internal partial class ThreadObjectPool<T> : IDisposable where T : class
{
    private ConcurrentQueue<T> _items = new();
    private SemaphoreSlim _semaphore;
    private Func<object?> _factory;

    private          int  _countUsed;
    private readonly bool _isDisposeObjects;
    private          bool _isDisposed;

    internal ThreadObjectPool(Func<T> factory, int capacity = 0, bool isDisposeObjects = true)
    {
        capacity          = capacity == 0 ? Environment.ProcessorCount : capacity;
        _factory          = factory;
        _isDisposeObjects = isDisposeObjects;
        _semaphore        = new SemaphoreSlim(capacity, capacity);
    }

    internal ThreadObjectPool(Task<T> factoryAsync, int capacity = 0, bool isDisposeObjects = true)
    {
        capacity          = capacity == 0 ? Environment.ProcessorCount : capacity;
        _factory          = () => factoryAsync;
        _isDisposeObjects = isDisposeObjects;
        _semaphore        = new SemaphoreSlim(capacity, capacity);
    }

    internal async Task<T> GetOrCreateObjectAsync(CancellationToken token = default)
    {
        await _semaphore.WaitAsync(token);
        Interlocked.Increment(ref _countUsed);

        if (_items.TryDequeue(out T? pooled))
            return pooled;

        if (_factory is Func<Task<T>> asyncFactory)
            return await asyncFactory();

        return ((Func<T>)_factory)();
    }

    internal T GetOrCreateObject()
    {
        _semaphore.Wait();
        Interlocked.Increment(ref _countUsed);

        if (_items.TryDequeue(out T? pooled))
            return pooled;

        if (_factory is Func<Task<T>> asyncFactory)
            return asyncFactory().Result;

        return ((Func<T>)_factory)();
    }

    internal void Return(T item)
    {
        _items.Enqueue(item);
        Interlocked.Decrement(ref _countUsed);
        _semaphore.Release();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _semaphore.Dispose();

        try
        {
            if (!_isDisposeObjects)
            {
                return;
            }

            foreach (IDisposable item in _items.OfType<IDisposable>())
            {
                item.Dispose();
            }
        }
        finally
        {
            _isDisposed = true;
            _items.Clear();
            _factory = null!;
            _items = null!;
            _semaphore = null!;
        }
    }
}
