using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ManagedLzma.SevenZip.Metadata;

namespace ManagedLzma.SevenZip.Writer
{
    public sealed class CopyEncoderSettings : EncoderSettings
    {
        public static readonly CopyEncoderSettings Instance = new CopyEncoderSettings();

        internal override CompressionMethod GetDecoderType() => CompressionMethod.Copy;

        private CopyEncoderSettings() { }

        internal override ImmutableArray<byte> SerializeSettings()
        {
            return ImmutableArray<byte>.Empty;
        }

        internal override EncoderNode CreateEncoder()
        {
            return new CopyEncoderNode();
        }
    }

    internal sealed class CopyEncoderNode : EncoderNode, IStreamReader, IStreamWriter
    {
        private byte[] mBuffer;
        private int mOffset;
        private int mLength;
        private bool mComplete;

        public override IStreamWriter GetInputSink(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return this;
        }

        public override void SetInputSource(int index, IStreamReader stream)
        {
            throw new InternalFailureException();
        }

        public override IStreamReader GetOutputSource(int index)
        {
            if (index != 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return this;
        }

        public override void SetOutputSink(int index, IStreamWriter stream)
        {
            throw new InternalFailureException();
        }

        public override void Start()
        {
        }

        public override void Dispose()
        {
        }

        Task<int> IStreamReader.ReadAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            Utilities.DebugCheckStreamArguments(buffer, offset, length, mode);

            lock (this)
            {
                int total = 0;

                while (length > 0)
                {
                    while (mBuffer == null)
                    {
                        if (mComplete)
                            return Task.FromResult(total);

                        Monitor.Wait(this);
                    }

                    int copied = Math.Min(length, mLength);
                    System.Diagnostics.Debug.Assert(copied > 0);
                    Buffer.BlockCopy(mBuffer, mOffset, buffer, offset, copied);
                    mOffset += copied;
                    mLength -= copied;
                    offset += copied;
                    length -= copied;
                    total += copied;

                    if (mLength == 0)
                    {
                        mBuffer = null;
                        Monitor.PulseAll(this);
                    }

                    if (mode == StreamMode.Partial)
                        break;
                }

                return Task.FromResult(total);
            }
        }

        Task<int> IStreamWriter.WriteAsync(byte[] buffer, int offset, int length, StreamMode mode)
        {
            Utilities.DebugCheckStreamArguments(buffer, offset, length, mode);

            lock (this)
            {
                System.Diagnostics.Debug.Assert(!mComplete);

                while (mBuffer != null)
                {
                    Monitor.Wait(this);
                    System.Diagnostics.Debug.Assert(!mComplete);
                }

                mBuffer = buffer;
                mOffset = offset;
                mLength = length;

                Monitor.PulseAll(this);

                while (mBuffer != null)
                {
                    Monitor.Wait(this);
                    System.Diagnostics.Debug.Assert(!mComplete);
                    System.Diagnostics.Debug.Assert(mBuffer == null || mBuffer == buffer);

                    if (mode == StreamMode.Partial && mBuffer != null && mLength < length)
                    {
                        mBuffer = null;
                        Monitor.PulseAll(this);
                        return Task.FromResult(length - mLength);
                    }
                }

                return Task.FromResult(length);
            }
        }

        Task IStreamWriter.CompleteAsync()
        {
            lock (this)
            {
                System.Diagnostics.Debug.Assert(!mComplete);

                mComplete = true;
                Monitor.PulseAll(this);

                while (mBuffer != null)
                    Monitor.Wait(this);
            }

            return Utilities.CompletedTask;
        }
    }
}
