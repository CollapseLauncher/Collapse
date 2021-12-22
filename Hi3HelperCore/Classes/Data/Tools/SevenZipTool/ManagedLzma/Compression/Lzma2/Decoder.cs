using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLzma.LZMA2
{
    /// <summary>
    /// This class maintains the state of a LZMA decoder and can be used to incrementally decode input data.
    /// This class is not threadsafe and must be accessed singlethreaded or under an external lock.
    /// </summary>
    public sealed class Decoder : IDisposable
    {
        private readonly DecoderSettings mSettings;
        private LZMA.Master.LZMA.CLzma2Dec mDecoder;
        private LZMA.Master.LZMA.ELzmaStatus mStatus;
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

            mDecoder = new LZMA.Master.LZMA.CLzma2Dec();
            mDecoder.Lzma2Dec_Construct();
            if (mDecoder.Lzma2Dec_Allocate(settings.GetInternalData(), LZMA.Master.LZMA.ISzAlloc.SmallAlloc) != LZMA.Master.LZMA.SZ_OK)
                throw new InvalidOperationException();
            mDecoder.Lzma2Dec_Init();
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
                mDecoder.Lzma2Dec_Free(LZMA.Master.LZMA.ISzAlloc.SmallAlloc);
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

                if (mDecoderPosition == mDecoder.mDecoder.mDicBufSize)
                    return (int)mDecoder.mDecoder.mDicBufSize;

                return checked((int)(mDecoder.mDecoder.mDicBufSize - mDecoder.mDecoder.mDicPos));
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

                return checked((int)(mDecoder.mDecoder.mDicPos - mDecoderPosition));
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

            Buffer.BlockCopy(mDecoder.mDecoder.mDic.mBuffer, mDecoder.mDecoder.mDic.mOffset + mDecoderPosition, buffer, offset, length);
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

            var mode = eof ? LZMA.Master.LZMA.ELzmaFinishMode.LZMA_FINISH_END : LZMA.Master.LZMA.ELzmaFinishMode.LZMA_FINISH_ANY;

            if (mDecoderPosition == mDecoder.mDecoder.mDicBufSize)
            {
                mDecoder.mDecoder.mDicPos = 0;
                mDecoderPosition = 0;
            }

            long outputLimit = mDecoder.mDecoder.mDicBufSize;
            if (limit.HasValue)
                outputLimit = Math.Min(outputLimit, mDecoderPosition + limit.Value);

            long inputField = length;
            var res = mDecoder.Lzma2Dec_DecodeToDic(outputLimit, LZMA.P.From(buffer, offset), ref inputField, mode, out mStatus);
            if (res != LZMA.Master.LZMA.SZ_OK)
            {
                System.Diagnostics.Debug.Assert(res == LZMA.Master.LZMA.SZ_ERROR_DATA);
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
}
