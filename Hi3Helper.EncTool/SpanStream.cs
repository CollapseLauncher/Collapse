using System;
using System.IO;

namespace Hi3Helper.EncTool
{
    public class SpanStream : Stream
    {
        private readonly Memory<byte> Base;
        private long _position = 0;
        
        public SpanStream(Memory<byte> buffer)
        {
            Base = buffer;
        }

        ~SpanStream() => Dispose();

        public override int Read(Span<byte> buffer)
        {
            buffer = Base.Slice((int)_position, buffer.Length).Span;
            _position += buffer.Length;
            return buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            _position += offset;
            int remain = Base.Length - (int)_position;
            int toRead = remain < count ? remain : count;

            buffer = Base.Slice((int)_position, toRead).ToArray();

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            throw new NotSupportedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            Base.Span.Clear();
        }

        public override long Length
        {
            get { return Base.Length; }
        }

        public override long Position
        {
            get
            { 
                return _position;
            }
            set
            {
                _position = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    _position = offset;
                    break;
                case SeekOrigin.Current:
                    _position += offset;
                    break;
                case SeekOrigin.End:
                    _position = Base.Length - offset;
                    break;
            }
            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Base.Span.Clear();
        }
    }
}
