using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA2
{
    public sealed class AsyncEncoder : IDisposable
    {
        // immutable
        private readonly object mSyncObject;

        // multithreaded access (under lock)
        private Task mEncoderTask;
        private Task mDisposeTask;
        private bool mRunning;

        // owned by encoder task
        private LZMA.Master.LZMA.CLzma2Enc mEncoder;

        public AsyncEncoder(EncoderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            mSyncObject = new object();

            mEncoder = new LZMA.Master.LZMA.CLzma2Enc(LZMA.Master.LZMA.ISzAlloc.SmallAlloc, LZMA.Master.LZMA.ISzAlloc.BigAlloc);
            mEncoder.Lzma2Enc_SetProps(settings.GetInternalSettings());
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
                        mDisposeTask = mEncoderTask.ContinueWith(new Action<Task>(delegate {
                            lock (mSyncObject)
                                DisposeInternal();
                        }));

                        Monitor.PulseAll(mSyncObject);
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

            // mDisposeTask may not be set yet if we complete mEncoderTask from another thread.
            // However even if mDisposeTask is not set we can be sure that the encoder is not running.

            mEncoder.Lzma2Enc_Destroy();
        }

        public Task EncodeAsync(IStreamReader input, IStreamWriter output, CancellationToken ct = default(CancellationToken))
        {
            lock (mSyncObject)
            {
                if (mDisposeTask != null)
                    throw new OperationCanceledException();

                // TODO: make this wait async as well
                while (mRunning)
                {
                    Monitor.Wait(mSyncObject);

                    if (mDisposeTask != null)
                        throw new OperationCanceledException();
                }

                mRunning = true;
            }

            var task = Task.Run(async delegate {
                var result = mEncoder.Lzma2Enc_Encode(new AsyncOutputProvider(output), new AsyncInputProvider(input), null);
                if (result != LZMA.Master.LZMA.SZ_OK)
                    throw new InvalidOperationException();

                await output.CompleteAsync().ConfigureAwait(false);
            });

            mEncoderTask = task.ContinueWith(delegate {
                lock (mSyncObject)
                {
                    System.Diagnostics.Debug.Assert(mRunning);
                    mRunning = false;
                    Monitor.PulseAll(mSyncObject);
                }
            }, CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);

            return task;
        }
    }
}
