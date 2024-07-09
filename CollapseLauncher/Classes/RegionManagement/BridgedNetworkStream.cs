using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    class BridgedNetworkStream : Stream
    {
        private protected readonly HttpResponseMessage _networkResponse;
        private protected readonly Stream _networkStream;
        private protected long _networkLength;
        private protected long _currentPosition = 0;

        internal static async ValueTask<BridgedNetworkStream> CreateStream(HttpResponseMessage networkResponse, CancellationToken token)
        {
            Stream networkStream = await networkResponse?.Content?.ReadAsStreamAsync(token);
            return new BridgedNetworkStream(networkResponse, networkStream);
        }

        public BridgedNetworkStream(HttpResponseMessage networkResponse, Stream networkStream)
        {
            _networkResponse = networkResponse;
            _networkLength = networkResponse?.Content?.Headers?.ContentLength ?? 0;
            _networkStream = networkStream;
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
        public new async ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await ReadAsync(buffer.Slice(totalRead), cancellationToken);
                if (read == 0) return;

                totalRead += read;
            }
        }

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
                _networkResponse?.Dispose();
                _networkStream?.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            _networkResponse?.Dispose();
            if (_networkStream != null)
                await _networkStream.DisposeAsync();

            await base.DisposeAsync();
            return;
        }
    }
}
