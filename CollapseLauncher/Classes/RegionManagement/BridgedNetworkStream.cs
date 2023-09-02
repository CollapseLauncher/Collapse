﻿using System;
using System.IO;

namespace CollapseLauncher
{
    class BridgedNetworkStream : Stream
    {
        private protected readonly Stream _networkStream;
        private protected long _networkLength;
        private protected long _currentPosition = 0;

        public BridgedNetworkStream(Stream networkStream, long networkLength)
        {
            _networkStream = networkStream;
            _networkLength = networkLength;
        }

        ~BridgedNetworkStream() => Dispose();

        private int ReadBytes(Span<byte> buffer)
        {
            int read = _networkStream.Read(buffer);
            _currentPosition += read;
            return read;
        }

        private int ReadBytes(byte[] buffer, int offset, int count)
        {
            int read = _networkStream.Read(buffer, offset, count);
            _currentPosition += read;
            return read;
        }

        public override int Read(Span<byte> buffer) => ReadBytes(buffer);
        public override int Read(byte[] buffer, int offset, int count) => ReadBytes(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            _networkStream.Flush();
        }

        public override long Length
        {
            get { return _networkLength; }
        }

        public override long Position
        {
            get { return _currentPosition; }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _networkStream.Dispose();
            }
        }
    }
}
