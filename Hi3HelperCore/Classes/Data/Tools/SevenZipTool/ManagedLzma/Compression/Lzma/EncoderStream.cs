using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ManagedLzma.LZMA
{
#if NOT_IMPLEMENTED
    public sealed class EncoderStream : Stream
    {
        public override bool CanSeek => false;
        public override bool CanRead => false;
        public override bool CanWrite => true;

        private Stream mStream;

        public EncoderStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            mStream = stream;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                mStream.Dispose();

            base.Dispose(disposing);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        #region Invalid Operations

        public override long Length
        {
            get { throw new InvalidOperationException(); }
        }

        public override long Position
        {
            get { throw new InvalidOperationException(); }
            set { throw new InvalidOperationException(); }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new InvalidOperationException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException();
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException();
        }

        #endregion
    }
#endif
}
