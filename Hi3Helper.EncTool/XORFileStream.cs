using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Hi3Helper.EncTool
{
    internal class XORFileStream : FileStream
    {
        private protected readonly byte _xorKey;
        public XORFileStream(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare = FileShare.Read, FileOptions fileOptions = FileOptions.None, byte XORKey = 0xA5)
            : base(path, fileMode, fileAccess, fileShare, 4 << 10, fileOptions)
        {
            _xorKey = XORKey;
        }

        ~XORFileStream() => Dispose();

        public int ReadNoDecrypt(Span<byte> buffer) => base.Read(buffer);
        public int ReadNoDecrypt(byte[] buffer, int offset, int count) => base.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => ReadBytes(buffer);
        public override int Read(byte[] buffer, int offset, int count) => ReadBytes(buffer, offset, count);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void WriteNoEncrypt(ReadOnlySpan<byte> buffer) => base.Write(buffer);
        public void WriteNoEncrypt(byte[] buffer, int offset, int count) => base.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => base.Write(WriteBytes(buffer));
        public override void Write(byte[] buffer, int offset, int count) { WriteBytes(buffer); base.Write(buffer, offset, count); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NotSupportedException();

        private void WriteBytes(byte[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] ^= _xorKey;
            }
        }

        private ReadOnlySpan<byte> WriteBytes(ReadOnlySpan<byte> buffer)
        {
            byte[] inBuffer = new byte[buffer.Length];
            Array.Copy(buffer.ToArray(), inBuffer, buffer.Length);
            for (int i = 0; i < buffer.Length; i++)
            {
                inBuffer[i] ^= _xorKey;
            }

            return inBuffer;
        }

        private int ReadBytes(Span<byte> buffer)
        {
            int i = 0;
            base.Read(buffer);
            for (; i < buffer.Length; i++)
            {
                buffer[i] ^= _xorKey;
            }
            return i;
        }

        private int ReadBytes(byte[] buffer, int offset, int count)
        {
            int i = 0;
            base.Read(buffer, offset, count);
            for (; i < buffer.Length; i++)
            {
                buffer[i] ^= _xorKey;
            }
            return i;
        }

        public new void Dispose() => base.Dispose(true);
    }
}
