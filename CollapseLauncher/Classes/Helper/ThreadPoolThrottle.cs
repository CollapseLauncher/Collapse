using System;
using System.Threading;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.Helper
{
    /// <summary>
    /// Manages the thread pool settings to throttle the number of threads.
    /// </summary>
    internal sealed partial class ThreadPoolThrottle : IDisposable
    {
        private  readonly int _previousThreadCount;
        private  readonly int _previousCompletionPortThreadCount;
        internal readonly int MultipliedThreadCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadPoolThrottle"/> class.
        /// </summary>
        /// <param name="previousThreadCount">The previous maximum number of worker threads.</param>
        /// <param name="previousCompletionPortThreadCount">The previous maximum number of asynchronous I/O threads.</param>
        /// <param name="multipliedThreadCount">The multiplied thread count.</param>
        private ThreadPoolThrottle(int previousThreadCount, int previousCompletionPortThreadCount, int multipliedThreadCount)
        {
            _previousThreadCount = previousThreadCount;
            _previousCompletionPortThreadCount = previousCompletionPortThreadCount;
            MultipliedThreadCount = multipliedThreadCount;
        }

        /// <summary>
        /// Starts the thread pool throttle by setting the maximum number of threads.
        /// </summary>
        /// <param name="multiply">The factor to multiply the processor count by to determine the maximum number of threads.</param>
        /// <returns>A <see cref="ThreadPoolThrottle"/> instance that can be used to restore the previous thread pool settings.</returns>
        public static ThreadPoolThrottle Start(int multiply = 4)
        {
            var threadCount = Environment.ProcessorCount * multiply;
            ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
            ThreadPool.SetMinThreads(Math.Max(workerThreads, threadCount),
                                     Math.Max(completionPortThreads, threadCount));
            return new ThreadPoolThrottle(workerThreads, completionPortThreads, threadCount);
        }

        /// <summary>
        /// Restores the previous thread pool settings.
        /// </summary>
        public void Dispose()
        {
            ThreadPool.SetMaxThreads(_previousThreadCount, _previousCompletionPortThreadCount);
        }
    }
}
