using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA
{
    /// <summary>
    /// This class maintains the state of a LZMA decoder and can be used to incrementally decode input data.
    /// This class is not threadsafe and must be accessed singlethreaded or under an external lock.
    /// </summary>
    public sealed class Decoder : IDisposable
    {
        private readonly DecoderSettings mSettings;
        private Master.LZMA.CLzmaDec mDecoder;
        private Master.LZMA.ELzmaStatus mStatus;
        private int mDecoderPosition;
        private bool mInputComplete;
        private bool mOutputComplete;
        private bool mDisposed;

        /// <summary>
        /// Construct a decoder with the given settings. The amount of memory required depends on the settings.
        /// </summary>
        /// <param name="settings">The settings for the decoder must match the settings used when the data was encoded.</param>
        public Decoder(DecoderSettings settings)
        {
            mSettings = settings;

            mDecoder = new Master.LZMA.CLzmaDec();
            mDecoder.LzmaDec_Construct();
            if (mDecoder.LzmaDec_Allocate(settings.ToArray(), Master.LZMA.LZMA_PROPS_SIZE, Master.LZMA.ISzAlloc.SmallAlloc) != Master.LZMA.SZ_OK)
                throw new InvalidOperationException();
            mDecoder.LzmaDec_Init();
        }

        /// <summary>
        /// Release the memory used by the decoder. Depending on the allocator used
        /// it may require a garbage collector pass to actually collect it.
        /// </summary>
        public void Dispose()
        {
            if (!mDisposed)
            {
                mDisposed = true;
                mDecoder.LzmaDec_Free(Master.LZMA.ISzAlloc.SmallAlloc);
            }
        }

        /// <summary>
        /// The capacity of the input buffer. If this is zero you must read data from
        /// the output buffer before you can continue decoding more input data.
        /// </summary>
        public int AvailableInputCapacity
        {
            get
            {
                if (mDisposed)
                    throw new ObjectDisposedException(null);

                if (mDecoderPosition == mDecoder.mDicBufSize)
                    return (int)mDecoder.mDicBufSize;

                return checked((int)(mDecoder.mDicBufSize - mDecoder.mDicPos));
            }
        }

        /// <summary>
        /// The number of currently available bytes in the output buffer.
        /// </summary>
        public int AvailableOutputLength
        {
            get
            {
                if (mDisposed)
                    throw new ObjectDisposedException(null);

                return checked((int)(mDecoder.mDicPos - mDecoderPosition));
            }
        }

        /// <summary>
        /// Returns true if the decoded output is complete.
        /// Note that some of the decoded output may still reside in the output buffer.
        /// </summary>
        public bool IsOutputComplete
        {
            get
            {
                if (mDisposed)
                    throw new ObjectDisposedException(null);

                return mOutputComplete;
            }
        }

        /// <summary>
        /// Allows you to read data from the output buffer.
        /// </summary>
        /// <param name="buffer">The target buffer into which data should be read.</param>
        /// <param name="offset">The offset at which data should be written.</param>
        /// <param name="length">The maximum number of bytes to read from the output buffer.</param>
        /// <returns>The number of bytes read from the output buffer.</returns>
        public int ReadOutputData(byte[] buffer, int offset, int length)
        {
            if (buffer == null && length != 0)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (mDisposed)
                throw new ObjectDisposedException(null);

            length = Math.Min(length, AvailableOutputLength);

            Buffer.BlockCopy(mDecoder.mDic.mBuffer, mDecoder.mDic.mOffset + mDecoderPosition, buffer, offset, length);
            mDecoderPosition += length;
            return length;
        }

        public int SkipOutputData(int length)
        {
            Utilities.NeedsBetterImplementation();

            var buffer = new byte[Math.Min(0x4000, length)];
            return ReadOutputData(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Decode input data until either the input data has been used up, the given output limit
        /// has been reached, or the end of the decoded data has been reached.
        /// </summary>
        /// <param name="buffer">The buffer containing input data.</param>
        /// <param name="offset">The offset at which the input data begins.</param>
        /// <param name="length">The length of the input data.</param>
        /// <param name="limit">
        /// A limit for the output data to decode. Already decoded but not yet read output data is counted against the limit.
        /// Can be used when the exact output length is known, or when the output buffer is limited and no readahead is wanted.
        /// </param>
        /// <param name="eof">Indicates that no more input data is available.</param>
        /// <returns>The number of bytes read from the input.</returns>
        public int Decode(byte[] buffer, int offset, int length, int? limit, bool eof)
        {
            if (buffer == null && length != 0)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (limit < 0)
                throw new ArgumentOutOfRangeException(nameof(limit));

            if (mDisposed)
                throw new ObjectDisposedException(null);

            if (mInputComplete && !(length == 0 && eof))
                throw new InvalidOperationException("Input has already been completed.");

            var mode = eof ? Master.LZMA.ELzmaFinishMode.LZMA_FINISH_END : Master.LZMA.ELzmaFinishMode.LZMA_FINISH_ANY;

            if (mDecoderPosition == mDecoder.mDicBufSize)
            {
                mDecoder.mDicPos = 0;
                mDecoderPosition = 0;
            }

            long outputLimit = mDecoder.mDicBufSize;
            if (limit.HasValue)
                outputLimit = Math.Min(outputLimit, mDecoderPosition + limit.Value);

            long inputField = length;
            var res = mDecoder.LzmaDec_DecodeToDic(outputLimit, P.From(buffer, offset), ref inputField, mode, out mStatus);
            if (res != Master.LZMA.SZ_OK)
            {
                System.Diagnostics.Debug.Assert(res == Master.LZMA.SZ_ERROR_DATA);
                throw new InvalidDataException();
            }

            var processed = checked((int)inputField);
            System.Diagnostics.Debug.Assert(0 <= processed && processed <= length);

            if (processed == length && eof)
                mInputComplete = true;

            if (mStatus == LZMA.Master.LZMA.ELzmaStatus.LZMA_STATUS_FINISHED_WITH_MARK)
                mOutputComplete = true;
            else if (mStatus == LZMA.Master.LZMA.ELzmaStatus.LZMA_STATUS_MAYBE_FINISHED_WITHOUT_MARK && mInputComplete)
                mOutputComplete = true;

            return processed;
        }
    }

    /// <summary>
    /// This class maintains the state of a LZMA decoder in a threadsafe way.
    /// The actual decoding happens on a separate thread.
    /// </summary>
    public sealed class AsyncDecoder : IDisposable
    {
        private sealed class InputFrame
        {
            public byte[] mBuffer;
            public int mOrigin;
            public int mOffset;
            public int mEnding;
            public AsyncTaskCompletionSource<object> mCompletion = AsyncTaskCompletionSource<object>.Create();
        }

        private sealed class OutputFrame
        {
            public byte[] mBuffer;
            public int mOrigin;
            public int mOffset;
            public int mEnding;
            public AsyncTaskCompletionSource<int> mCompletion = AsyncTaskCompletionSource<int>.Create();
            public StreamMode mMode;
        }

        // immutable
        private readonly object mSyncObject;
        private readonly Action mDecodeAction;

        // multithreaded access (under lock)
        private Task mDecoderTask;
        private Task mDisposeTask;
        private AsyncTaskCompletionSource<object> mFlushControl = AsyncTaskCompletionSource<object>.Create();
        private Queue<InputFrame> mInputQueue;
        private Queue<OutputFrame> mOutputQueue;
        private int mTotalOutputCapacity;
        private bool mRunning;
        private bool mFlushed;

        // owned by decoder task
        private Decoder mDecoder;

        public AsyncDecoder(DecoderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            mSyncObject = new object();
            mDecodeAction = new Action(Decode);
            mDecoder = new Decoder(settings);
            mInputQueue = new Queue<InputFrame>();
            mOutputQueue = new Queue<OutputFrame>();
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }

        public Task DisposeAsync()
        {
            // We need to ensure that cleanup only happens once, so we need to remember that we started it.
            // We also need to make sure that the returned task completes *after* everything has been disposed.
            // Both can be covered by keeping track of the disposal via a Task object.
            //
            lock (mSyncObject)
            {
                if (mDisposeTask == null)
                {
                    if (mRunning)
                    {
                        mDisposeTask = mDecoderTask.ContinueWith(new Action<Task>(delegate {
                            lock (mSyncObject)
                                DisposeInternal();
                        }));
                    }
                    else
                    {
                        DisposeInternal();
                        mDisposeTask = Utilities.CompletedTask;
                    }
                }

                return mDisposeTask;
            }
        }

        private void DisposeInternal()
        {
            System.Diagnostics.Debug.Assert(Monitor.IsEntered(mSyncObject));
            System.Diagnostics.Debug.Assert(!mRunning);

            // mDisposeTask may not be set yet if we complete mDecoderTask from another thread.
            // However even if mDisposeTask is not set we can be sure that the decoder is not running.

            mDecoder.Dispose();

            foreach (var frame in mInputQueue)
                frame.mCompletion.SetCanceled();

            mInputQueue.Clear();

            foreach (var frame in mOutputQueue)
                frame.mCompletion.SetCanceled();

            mOutputQueue.Clear();

            if (!mFlushControl.Task.IsCompleted)
                mFlushControl.SetCanceled();
        }

        /// <summary>
        /// Notifies the decoder that the input data is complete.
        /// The returned task notifies the caller when all output data has been read.
        /// </summary>
        /// <returns>A task which completes when all output data has been read.</returns>
        public Task CompleteInputAsync()
        {
            lock (mSyncObject)
            {
                mFlushed = true;
                TryStartDecoding();
                return mFlushControl.Task;
            }
        }

        /// <summary>
        /// Pass input data to the decoder. You must not modify the specified region of the buffer until the returned task completes.
        /// </summary>
        /// <param name="buffer">The buffer containing the input data.</param>
        /// <param name="offset">The offset at which the input data begins.</param>
        /// <param name="length">The length of the input data.</param>
        /// <returns>A task which tells you when the input data has been completely read and the buffer can be reused.</returns>
        public Task WriteInputAsync(byte[] buffer, int offset, int length)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));

            var frame = new InputFrame();
            frame.mBuffer = buffer;
            frame.mOrigin = offset;
            frame.mOffset = offset;
            frame.mEnding = offset + length;
            PushInputFrame(frame);
            return frame.mCompletion.Task;
        }

        /// <summary>
        /// Read output data from the decoder. You should not use the specified region of the buffer until the returned task completes.
        /// </summary>
        /// <param name="buffer">The buffer into which output data should be written.</param>
        /// <param name="offset">The offset at which output data should be written.</param>
        /// <param name="length">The maximum number of bytes which should be written.</param>
        /// <param name="mode">
        /// Specifies whether to wait until the whole output buffer has been filled,
        /// or wether to return as soon as some data is available.
        /// </param>
        /// <returns>A task which, when completed, tells you how much data has been read.</returns>
        public Task<int> ReadOutputAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            if (offset < 0 || offset > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || length > buffer.Length - offset)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (mode != StreamMode.Complete && mode != StreamMode.Partial)
                throw new ArgumentOutOfRangeException(nameof(mode));

            var frame = new OutputFrame();
            frame.mBuffer = buffer;
            frame.mOrigin = offset;
            frame.mOffset = offset;
            frame.mEnding = offset + length;
            frame.mMode = mode;
            PushOutputFrame(frame);
            return frame.mCompletion.Task;
        }

        public async Task SkipOutputAsync(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (length == 0)
                return;

            Utilities.NeedsBetterImplementation();

            var buffer = new byte[Math.Min(0x4000, length)];
            while (length > 0)
            {
                var skipped = await ReadOutputAsync(buffer, 0, Math.Min(buffer.Length, length), StreamMode.Partial);
                if (skipped == 0)
                    throw new InvalidOperationException(ErrorStrings.SkipBeyondEndOfStream);

                length -= skipped;
            }
        }

        private void PushInputFrame(InputFrame frame)
        {
            lock (mSyncObject)
            {
                if (mDisposeTask != null)
                    throw new ObjectDisposedException(null);

                if (mFlushed)
                    throw new InvalidOperationException();

                mInputQueue.Enqueue(frame);
                TryStartDecoding();
            }
        }

        private void PushOutputFrame(OutputFrame frame)
        {
            lock (mSyncObject)
            {
                if (mDisposeTask != null)
                    throw new ObjectDisposedException(null);

                mTotalOutputCapacity = checked(mTotalOutputCapacity + (frame.mEnding - frame.mOffset));
                mOutputQueue.Enqueue(frame);
                TryStartDecoding();
            }
        }

        private void TryStartDecoding()
        {
            System.Diagnostics.Debug.Assert(Monitor.IsEntered(mSyncObject));

            if (!mRunning)
            {
                mRunning = true;
                System.Diagnostics.Debug.Assert(mDecoderTask == null);
                mDecoderTask = Task.Run(mDecodeAction);
            }
        }

        private void Decode()
        {
            do { WriteOutput(); }
            while (DecodeInput());
        }

        private void WriteOutput()
        {
            while (mDecoder.AvailableOutputLength > 0)
            {
                OutputFrame frame;
                lock (mSyncObject)
                {
                    System.Diagnostics.Debug.Assert(mRunning);

                    if (mOutputQueue.Count == 0)
                        break;

                    frame = mOutputQueue.Peek();
                }

                var capacity = frame.mEnding - frame.mOffset;
                var copySize = Math.Min(capacity, mDecoder.AvailableOutputLength);
                mDecoder.ReadOutputData(frame.mBuffer, frame.mOffset, copySize);
                frame.mOffset += copySize;

                if (copySize == capacity || frame.mMode == StreamMode.Partial)
                {
                    frame.mCompletion.SetResult(frame.mOffset - frame.mOrigin);

                    lock (mSyncObject)
                    {
                        System.Diagnostics.Debug.Assert(mRunning);
                        var other = mOutputQueue.Dequeue();
                        System.Diagnostics.Debug.Assert(other == frame);
                    }
                }
            }

            if (mDecoder.IsOutputComplete && !mFlushControl.Task.IsCompleted)
                mFlushControl.SetResult(null);
        }

        private bool DecodeInput()
        {
            int capacity;
            bool eof;
            InputFrame frame;

            lock (mSyncObject)
            {
                System.Diagnostics.Debug.Assert(mRunning);

                if (mInputQueue.Count == 0 || mDisposeTask != null)
                {
                    mRunning = false;
                    return false;
                }

                eof = (mFlushed && mInputQueue.Count == 1);
                capacity = mTotalOutputCapacity;
                frame = mInputQueue.Peek();
            }

            frame.mOffset += mDecoder.Decode(frame.mBuffer, frame.mOffset, frame.mEnding - frame.mOffset, capacity, eof);

            if (frame.mOffset == frame.mEnding)
            {
                frame.mCompletion.SetResult(null);

                lock (mSyncObject)
                {
                    System.Diagnostics.Debug.Assert(mRunning);
                    var other = mInputQueue.Dequeue();
                    System.Diagnostics.Debug.Assert(other == frame);

                    if (mDisposeTask != null)
                    {
                        mRunning = false;
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
