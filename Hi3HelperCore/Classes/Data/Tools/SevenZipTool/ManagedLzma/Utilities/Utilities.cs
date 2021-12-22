using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if !BUILD_PORTABLE
using System.Runtime.Serialization;
#endif

namespace ManagedLzma
{
    /// <summary>
    /// This exception is only thrown from "impossible" situations.
    /// If it is ever observed this indicates a bug in the library.
    /// </summary>
#if !BUILD_PORTABLE
    [Serializable]
#endif
    internal sealed class InternalFailureException : InvalidOperationException
    {
        public InternalFailureException()
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
#endif
        }

#if !BUILD_PORTABLE
        private InternalFailureException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
#endif
    }

    /// <summary>
    /// Controls how the completion of <see cref="IStreamReader"/> and <see cref="IStreamWriter"/> methods work.
    /// </summary>
    public enum StreamMode
    {
        /// <summary>
        /// Wait until the provided buffer section has been completely processed.
        /// </summary>
        Complete,

        /// <summary>
        /// Return after processing any amount of data from the provided buffer section.
        /// </summary>
        Partial,
    }

    /// <remarks>
    /// This API is not designed for cancellation because in the context it is used
    /// there is no sane way to resume work once a sub-task has been canceled.
    /// Note that the caller can still abort the whole operation (instead of a single call)
    /// by disposing the proper controlling object.
    /// </remarks>
    public interface IStreamReader
    {
        /// <summary>Requests to read data into the provided buffer.</summary>
        /// <param name="buffer">The buffer into which data is read. Cannot be null.</param>
        /// <param name="offset">The offset at which data is written.</param>
        /// <param name="length">The amount of data to read. Must be greater than zero.</param>
        /// <param name="mode">Determines the response if the buffer cannot be filled.</param>
        /// <returns>A task which completes when the read completes. Returns the number of bytes written.</returns>
        Task<int> ReadAsync(byte[] buffer, int offset, int length, StreamMode mode);
    }

    /// <remarks>
    /// This API is not designed for cancellation because in the context it is used
    /// there is no sane way to resume work once a sub-task has been canceled.
    /// Note that the caller can still abort the whole operation (instead of a single call)
    /// by disposing the proper controlling object.
    /// </remarks>
    public interface IStreamWriter
    {
        Task<int> WriteAsync(byte[] buffer, int offset, int length, StreamMode mode);

        /// <returns>
        /// A task which completes once all outstanding writes have been completed
        /// and any remaining data has been forwarded to the underlying receiver.
        /// In other words, once the returned task completes it is safe to dispose.
        /// </returns>
        Task CompleteAsync();
    }

    internal static class ErrorStrings
    {
        internal const string SkipBeyondEndOfStream = "Skipping beyond end of stream.";
    }

    internal static class Utilities
    {
        internal static void NeedsReview()
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
#endif
        }

        internal static void NeedsBetterImplementation()
        {
#if DEBUG
            if (System.Diagnostics.Debugger.IsAttached)
                System.Diagnostics.Debugger.Break();
#endif
        }

#if NET_45
        internal static Task CompletedTask => Task.FromResult<object>(null);
#else
        internal static Task CompletedTask => Task.CompletedTask;
#endif

        internal static void ClearBuffer<T>(ref T[] buffer)
        {
            if (buffer != null)
            {
                Array.Clear(buffer, 0, buffer.Length);
                buffer = null;
            }
        }

        internal static void CheckStreamArguments(byte[] buffer, int offset, int length, StreamMode mode)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            // Since length cannot be zero we also know offset cannot be the buffer length.
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            // Length cannot be zero.
            if (length <= 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (mode != StreamMode.Complete && mode != StreamMode.Partial)
                throw new ArgumentOutOfRangeException(nameof(mode));
        }

        [System.Diagnostics.Conditional("DEBUG")]
        internal static void DebugCheckStreamArguments(byte[] buffer, int offset, int length, StreamMode mode)
        {
            System.Diagnostics.Debug.Assert(buffer != null);
            System.Diagnostics.Debug.Assert(0 <= offset && offset < buffer.Length);
            System.Diagnostics.Debug.Assert(0 < length && length <= buffer.Length - offset);
            System.Diagnostics.Debug.Assert(mode == StreamMode.Complete || mode == StreamMode.Partial);
        }
    }

    internal struct AsyncTaskCompletionSource<T>
    {
        public static AsyncTaskCompletionSource<T> Create()
        {
#if NET_45
            return new AsyncTaskCompletionSource<T>(new TaskCompletionSource<T>());
#else
            return new AsyncTaskCompletionSource<T>(new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously));
#endif
        }

        private readonly TaskCompletionSource<T> mCompletionSource;

        private AsyncTaskCompletionSource(TaskCompletionSource<T> cps)
        {
            mCompletionSource = cps;
        }

        public Task<T> Task => mCompletionSource.Task;

        public void SetCanceled()
        {
#if NET_45
            var cps = mCompletionSource;
            System.Threading.Tasks.Task.Run(delegate { cps.SetCanceled(); });
#else
            mCompletionSource.SetCanceled();
#endif
        }

        public void SetResult(T result)
        {
#if NET_45
            var cps = mCompletionSource;
            System.Threading.Tasks.Task.Run(delegate { cps.SetResult(result); });
#else
            mCompletionSource.SetResult(result);
#endif
        }
    }
}
