using System;
using System.IO;

namespace Hi3Helper.EncTool
{
    public class XORStream : Stream
    {
        private protected readonly Stream _stream;
        private protected readonly byte _xorKey;
        public XORStream(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare = FileShare.Read, FileOptions fileOptions = FileOptions.None, byte XORKey = 0xA5)
            : base()
        {
            _xorKey = XORKey;
            _stream = new FileStream(path, fileMode, fileAccess, fileShare, 4 << 10, fileOptions);
        }

        public XORStream(Stream stream, byte XORKey = 0xA5)
            : base()
        {
            _xorKey = XORKey;
            _stream = stream;
        }

        public XORStream(byte XORKey = 0xA5)
            : base()
        {
            _xorKey = XORKey;
            _stream = new MemoryStream();
        }

        ~XORStream() => Dispose();

        private void WriteBytes(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] ^= _xorKey;
            }

            _stream.Write(buffer, offset, count);
        }

        private void WriteBytes(ReadOnlySpan<byte> buffer)
        {
            byte[] inBuffer = new byte[buffer.Length];
            Array.Copy(buffer.ToArray(), inBuffer, buffer.Length);
            for (int i = 0; i < buffer.Length; i++)
            {
                inBuffer[i] ^= _xorKey;
            }

            _stream.Write(inBuffer);
        }

        private int ReadBytes(Span<byte> buffer)
        {
            int i = _stream.Read(buffer);
            ReadXOR(buffer, i);
            return i;
        }

        private int ReadBytes(byte[] buffer, int offset, int count)
        {
            int i = _stream.Read(buffer, offset, count);
            ReadXOR(buffer, i);
            return i;
        }

        public override int Read(Span<byte> buffer) => ReadBytes(buffer);
        public override int Read(byte[] buffer, int offset, int count) => ReadBytes(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => WriteBytes(buffer);
        public override void Write(byte[] buffer, int offset, int count) => WriteBytes(buffer, offset, count);

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
            _stream.Flush();
        }

        public override long Length
        {
            get { return _stream.Length; }
        }

        public override long Position
        {
            get { return _stream.Position; }
            set { _stream.Position = value; }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _stream.SetLength(value);
        }

        private void ReadXOR(Span<byte> buffer, int i)
        {
            for (int j = 0; j < i; j++)
            {
                buffer[j] ^= _xorKey;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _stream.Dispose();
        }
    }
}
