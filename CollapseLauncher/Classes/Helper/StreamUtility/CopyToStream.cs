using Hi3Helper.Http;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.StreamUtility
{
    internal sealed class CopyToStream(
        Stream                    sourceStream,
        Stream                    destinationStream,
        DownloadProgressDelegate? readDelegate    = null,
        bool                      isDisposeStream = false)
        : Stream
    {
        private readonly DownloadProgress _readProperty = new()
        {
            BytesDownloaded = 0,
            BytesTotal      = sourceStream.Length
        };

        public override bool CanRead => sourceStream.CanRead;

        public override bool CanSeek => sourceStream.CanSeek;

        public override bool CanWrite => sourceStream.CanWrite;

        public override long Length => sourceStream.Length;

        public override long Position { get => sourceStream.Position; set => Seek(value, SeekOrigin.Begin); }

        ~CopyToStream() => Dispose(false);

        public override int Read(byte[] buffer, int offset, int count)
            => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int read = sourceStream.Read(buffer);
            if (read <= 0)
            {
                return read;
            }

            destinationStream.Write(buffer[..read]);
            Interlocked.Add(ref _readProperty.BytesDownloaded, read);
            readDelegate?.Invoke(read, _readProperty);
            return read;
        }

        public override int ReadByte()
        {
            int readByte = sourceStream.ReadByte();
            destinationStream.WriteByte((byte)readByte);

            return readByte;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await ReadAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int read = await sourceStream.ReadAsync(buffer, cancellationToken);
            if (read <= 0)
            {
                return read;
            }

            await destinationStream.WriteAsync(buffer[..read], cancellationToken);
            Interlocked.Add(ref _readProperty.BytesDownloaded, read);
            readDelegate?.Invoke(read, _readProperty);
            return read;
        }

        public override void Write(byte[] buffer, int offset, int count)
            => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            sourceStream.Write(buffer);
            destinationStream.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            sourceStream.WriteByte(value);
            destinationStream.WriteByte(value);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await sourceStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await destinationStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long seek = sourceStream.Seek(offset, origin);
            _ = destinationStream.Seek(offset, origin);

            return seek;
        }

        public override void SetLength(long value)
        {
            sourceStream.SetLength(value);
            destinationStream.SetLength(value);
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                int read;
                while ((read = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0,       read), cancellationToken);
                    await destinationStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void CopyTo(Stream destination, int bufferSize)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                int read;
                while ((read = sourceStream.Read(buffer)) > 0)
                {
                    destination.Write(buffer, 0, read);
                    destinationStream.Write(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void Close()
        {
            sourceStream.Close();
            destinationStream.Close();
        }

        public override void Flush()
        {
            sourceStream.Flush();
            destinationStream.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await sourceStream.FlushAsync(cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing || !isDisposeStream)
            {
                return;
            }

            sourceStream.Dispose();
            destinationStream.Dispose();
        }

        public override async ValueTask DisposeAsync()
        {
            await sourceStream.DisposeAsync();
            await destinationStream.DisposeAsync();

            GC.SuppressFinalize(this);
        }
    }
}
