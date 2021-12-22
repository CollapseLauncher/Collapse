using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedLzma
{
    internal sealed class AsyncInputProvider : LZMA.Master.LZMA.ISeqInStream
    {
        private IStreamReader mStream;
        private bool mCompleted;

        internal AsyncInputProvider(IStreamReader stream)
        {
            mStream = stream;
        }

        LZMA.Master.LZMA.SRes LZMA.Master.LZMA.ISeqInStream.Read(LZMA.P<byte> buf, ref long size)
        {
            System.Diagnostics.Debug.Assert(size > 0);

            if (mCompleted)
            {
                size = 0;
                return LZMA.Master.LZMA.SZ_OK;
            }

            var capacity = size < Int32.MaxValue ? (int)size : Int32.MaxValue;

            int fetched;
            try { fetched = mStream.ReadAsync(buf.mBuffer, buf.mOffset, capacity, StreamMode.Partial).GetAwaiter().GetResult(); }
            catch (OperationCanceledException)
            {
                size = 0;
                return LZMA.Master.LZMA.SZ_ERROR_FAIL;
            }

            if (fetched < 0 || fetched > capacity)
                throw new InvalidOperationException("IInputStream.ReadAsync returned an invalid result.");

            if (fetched == 0)
                mCompleted = true;

            size = fetched;
            return LZMA.Master.LZMA.SZ_OK;
        }
    }

    internal sealed class AsyncOutputProvider : LZMA.Master.LZMA.ISeqOutStream
    {
        private IStreamWriter mStream;

        internal AsyncOutputProvider(IStreamWriter stream)
        {
            mStream = stream;
        }

        long LZMA.Master.LZMA.ISeqOutStream.Write(LZMA.P<byte> buf, long size)
        {
            System.Diagnostics.Debug.Assert(size > 0);

            var buffer = buf.mBuffer;
            var offset = buf.mOffset;
            var result = size;

            while (size > Int32.MaxValue)
            {
                int written;
                try { written = mStream.WriteAsync(buffer, offset, Int32.MaxValue, StreamMode.Partial).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { return 0; }

                if (written <= 0)
                    throw new InvalidOperationException("IOutputStream.WriteAsync returned an invalid result.");

                offset += written;
                size -= written;
            }

            if (size > 0)
            {
                int written;
                try { written = mStream.WriteAsync(buffer, offset, (int)size, StreamMode.Complete).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { return 0; }

                if (written != size)
                    throw new InvalidOperationException("IOutputStream.WriteAsync returned an invalid result.");
            }

            return result;
        }
    }

#if DEBUG

    // These classes are currently not used so we don't include them in nuget release builds.

    internal sealed class AsyncInputQueue : IStreamReader, IStreamWriter
    {
        private sealed class Frame
        {
            public byte[] mBuffer;
            public int mOrigin;
            public int mOffset;
            public int mEnding;
            public AsyncTaskCompletionSource<int> mCompletion = AsyncTaskCompletionSource<int>.Create();
        }

        private object mSyncObject;
        private Queue<Frame> mQueue = new Queue<Frame>();
        private Task mDisposeTask;
        private bool mRunning;
        private bool mCompleted;

        public Task<int> ReadAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            Utilities.CheckStreamArguments(buffer, offset, length, mode);

            int total = 0;

            while (length > 0)
            {
                Frame frame;
                lock (mSyncObject)
                {
                    System.Diagnostics.Debug.Assert(mRunning);

                    if (mDisposeTask != null)
                        throw new OperationCanceledException();

                    while (mQueue.Count == 0)
                    {
                        if (mCompleted)
                            return Task.FromResult(total);

                        Monitor.Wait(mSyncObject);

                        if (mDisposeTask != null)
                            throw new OperationCanceledException();
                    }

                    frame = mQueue.Peek();
                }

                System.Diagnostics.Debug.Assert(frame.mOffset < frame.mEnding);
                var processed = Math.Min(frame.mEnding - frame.mOffset, length);
                System.Diagnostics.Debug.Assert(processed > 0);
                Buffer.BlockCopy(frame.mBuffer, frame.mOffset, buffer, offset, processed);
                frame.mOffset += processed;
                total += processed;
                offset += processed;
                length -= processed;

                if (frame.mOffset == frame.mEnding)
                {
                    frame.mCompletion.SetResult(frame.mOffset - frame.mOrigin);

                    lock (mSyncObject)
                    {
                        System.Diagnostics.Debug.Assert(mRunning);
                        var other = mQueue.Dequeue();
                        System.Diagnostics.Debug.Assert(other == frame);

                        if (mDisposeTask != null)
                            throw new OperationCanceledException();
                    }
                }

                if (mode == StreamMode.Partial)
                    break;
            }

            return Task.FromResult(total);
        }

        public Task<int> WriteAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));

            var frame = new Frame();
            frame.mBuffer = buffer;
            frame.mOrigin = offset;
            frame.mOffset = offset;
            frame.mEnding = offset + length;

            lock (mSyncObject)
            {
                if (mDisposeTask != null)
                    throw new ObjectDisposedException(null);

                if (mCompleted)
                    throw new InvalidOperationException();

                mQueue.Enqueue(frame);
                Monitor.PulseAll(mSyncObject);
            }

            return frame.mCompletion.Task;
        }

        public Task CompleteAsync()
        {
            return Task.Run(() => {
                lock (mSyncObject)
                {
                    mCompleted = true;
                    Monitor.PulseAll(mSyncObject);

                    while (mQueue.Count > 0)
                        Monitor.Wait(mSyncObject);
                }
            });
        }
    }

    internal sealed class AsyncOutputQueue : IStreamReader, IStreamWriter
    {
        private sealed class Frame
        {
            public byte[] mBuffer;
            public int mOrigin;
            public int mOffset;
            public int mEnding;
            public AsyncTaskCompletionSource<int> mCompletion = AsyncTaskCompletionSource<int>.Create();
            public StreamMode mMode;
        }

        private object mSyncObject;
        private Queue<Frame> mQueue = new Queue<Frame>();
        private Task mDisposeTask;
        private bool mRunning;

        public Task<int> ReadAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            Utilities.CheckStreamArguments(buffer, offset, length, mode);

            var frame = new Frame();
            frame.mBuffer = buffer;
            frame.mOrigin = offset;
            frame.mOffset = offset;
            frame.mEnding = offset + length;
            frame.mMode = mode;

            lock (mSyncObject)
            {
                if (mDisposeTask != null)
                    throw new ObjectDisposedException(null);

                mQueue.Enqueue(frame);
                Monitor.Pulse(mSyncObject);
            }

            return frame.mCompletion.Task;
        }

        public Task<int> WriteAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            Utilities.CheckStreamArguments(buffer, offset, length, mode);

            int processed = 0;

            while (length > 0)
            {
                Frame frame;
                lock (mSyncObject)
                {
                    System.Diagnostics.Debug.Assert(mRunning);

                    if (mDisposeTask != null)
                        throw new OperationCanceledException();

                    while (mQueue.Count == 0)
                    {
                        Monitor.Wait(mSyncObject);

                        if (mDisposeTask != null)
                            throw new OperationCanceledException();
                    }

                    frame = mQueue.Peek();
                }

                var capacity = frame.mEnding - frame.mOffset;
                var copySize = Math.Min(capacity, length > Int32.MaxValue ? Int32.MaxValue : (int)length);
                Buffer.BlockCopy(buffer, offset, frame.mBuffer, frame.mOffset, copySize);
                frame.mOffset += copySize;
                offset += copySize;
                processed += copySize;
                length -= copySize;

                if (copySize == capacity || frame.mMode == StreamMode.Partial)
                {
                    frame.mCompletion.SetResult(frame.mOffset - frame.mOrigin);

                    lock (mSyncObject)
                    {
                        System.Diagnostics.Debug.Assert(mRunning);
                        var other = mQueue.Dequeue();
                        System.Diagnostics.Debug.Assert(other == frame);
                    }
                }
            }

            return Task.FromResult(processed);
        }

        public Task CompleteAsync()
        {
            throw new NotImplementedException();
        }
    }
#endif
}
