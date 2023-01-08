using System;
using System.IO;

namespace Hi3Helper.EncTool
{
    public class ChunkStream : Stream
    {
        private long _start { get; set; }
        private long _end { get; set; }
        private long _size { get => _end - _start; }
        private long _curPos { get; set; }
        private long _remain { get => _size - _curPos; }
        private protected readonly Stream _stream;

        public ChunkStream(Stream stream, long start, long end)
            : base()
        {
            _stream = stream;

            if (_stream.Length == 0)
            {
                throw new Exception("The stream must not have 0 bytes!");
            }

            if (_stream.Length < start || end > _stream.Length)
            {
                throw new ArgumentOutOfRangeException("Offset is out of stream size range!");
            }

            _stream.Position = start;
            _start = start;
            _end = end;
            _curPos = 0;
        }

        ~ChunkStream() => Dispose();

        public override int Read(Span<byte> buffer)
        {
            if (_remain == 0) return 0;

            int toSlice = (int)(buffer.Length > _remain ? _remain : buffer.Length);
            _curPos += toSlice;

            return _stream.Read(buffer.Slice(0, toSlice));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_remain == 0) return 0;

            int toRead = (int)(_remain < count ? _remain : count);
            int toOffset = offset > _remain ? 0 : offset;
            _stream.Position += toOffset;
            _curPos += toOffset + toRead;

            return _stream.Read(buffer, offset, toRead);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_remain == 0) return;

            int toSlice = (int)(buffer.Length > _remain ? _remain : buffer.Length);
            _curPos += toSlice;

            _stream.Write(buffer.Slice(0, toSlice));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int toRead = (int)(_remain < count ? _remain : count);
            int toOffset = offset > _remain ? 0 : offset;
            _stream.Position += toOffset;
            _curPos += toOffset + toRead;

            _stream.Write(buffer, offset, toRead);
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get { return _stream.CanRead; }
        }

        public override bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        public override bool CanWrite
        {
            get { return _stream.CanWrite; }
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override long Length
        {
            get { return _size; }
        }

        public override long Position
        {
            get
            {
                return _stream.Position - _start;
            }
            set
            {
                if (value > _size)
                {
                    throw new IndexOutOfRangeException();
                }

                _stream.Position = value + _start;
                _curPos = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        if (offset > _size)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                        return _stream.Seek(offset + _start, SeekOrigin.Begin) - _start;
                    }
                case SeekOrigin.Current:
                    {
                        long pos = _stream.Position - _start;
                        if (pos + offset > _size)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                        return _stream.Seek(offset, SeekOrigin.Current) - _start;
                    }
                default:
                    {
                        _stream.Position = _end;
                        _stream.Position -= offset;

                        return Position;
                    }
            }
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _stream.Dispose();
        }
    }
}
