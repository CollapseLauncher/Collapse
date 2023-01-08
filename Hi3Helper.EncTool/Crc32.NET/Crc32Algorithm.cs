using System;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Force.Crc32
{
    /// <summary>
    /// Implementation of CRC-32.
    /// This class supports several convenient static methods returning the CRC as UInt32.
    /// </summary>
    public class Crc32Algorithm : HashAlgorithm
    {
        private uint _currentCrc;

        private readonly bool _isBigEndian = true;

        private readonly SafeProxy _proxy = new SafeProxy();

        /// <summary>
        /// Initializes a new instance of the <see cref="Crc32Algorithm"/> class.
        /// </summary>
        public Crc32Algorithm()
        {
            HashSizeValue = 32;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Crc32Algorithm"/> class.
        /// </summary>
        /// <param name="isBigEndian">Should return bytes result as big endian or little endian</param>
        // Crc32 by dariogriffo uses big endian, so, we need to be compatible and return big endian as default
        public Crc32Algorithm(bool isBigEndian = true)
            : this()
        {
            _isBigEndian = isBigEndian;
        }

        /// <summary>
        /// Computes CRC-32 from multiple buffers.
        /// Call this method multiple times to chain multiple buffers.
        /// </summary>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        public void Append(ReadOnlySpan<byte> input)
        {
            _currentCrc = AppendInternal(_currentCrc, input);
        }

        /// <summary>
        /// Computes CRC-32 from multiple buffers.
        /// Call this method multiple times to chain multiple buffers.
        /// </summary>
        /// <param name="initial">
        /// Initial CRC value for the algorithm. It is zero for the first buffer.
        /// Subsequent buffers should have their initial value set to CRC value returned by previous call to this method.
        /// </param>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        public void Append(byte[] input)
        {
            _currentCrc = AppendInternal(_currentCrc, input);
        }

        /// <summary>
        /// Computes CRC-32 from multiple buffers.
        /// Call this method multiple times to chain multiple buffers.
        /// </summary>
        /// <param name="initial">
        /// Initial CRC value for the algorithm. It is zero for the first buffer.
        /// Subsequent buffers should have their initial value set to CRC value returned by previous call to this method.
        /// </param>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <param name="offset">Offset of the input data within the buffer.</param>
        /// <param name="length">Length of the input data in the buffer.</param>
        /// <returns>Accumulated CRC-32 of all buffers processed so far.</returns>
        public void Append(byte[] input, int offset, int length)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            if (offset < 0 || length < 0 || offset + length > input.Length)
                throw new ArgumentOutOfRangeException("length");
            _currentCrc = AppendInternal(_currentCrc, input, offset, length);
        }

        /// <summary>
        /// Computes CRC-32 from multiple buffers.
        /// Call this method multiple times to chain multiple buffers.
        /// </summary>
        /// <param name="initial">
        /// Initial CRC value for the algorithm. It is zero for the first buffer.
        /// Subsequent buffers should have their initial value set to CRC value returned by previous call to this method.
        /// </param>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <param name="offset">Offset of the input data within the buffer.</param>
        /// <param name="length">Length of the input data in the buffer.</param>
        /// <returns>Accumulated CRC-32 of all buffers processed so far.</returns>
        public uint Append(uint initial, byte[] input, int offset, int length)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            if (offset < 0 || length < 0 || offset + length > input.Length)
                throw new ArgumentOutOfRangeException("length");
            return AppendInternal(initial, input, offset, length);
        }

        /// <summary>
        /// Computes CRC-32 from multiple buffers.
        /// Call this method multiple times to chain multiple buffers.
        /// </summary>
        /// <param name="initial">
        /// Initial CRC value for the algorithm. It is zero for the first buffer.
        /// Subsequent buffers should have their initial value set to CRC value returned by previous call to this method.
        /// </param>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        /// <returns>Accumulated CRC-32 of all buffers processed so far.</returns>
        public uint Append(uint initial, byte[] input)
        {
            if (input == null)
                throw new ArgumentNullException();
            return AppendInternal(initial, input, 0, input.Length);
        }

        /// <summary>
        /// Computes CRC-32 from multiple buffers.
        /// Call this method multiple times to chain multiple buffers.
        /// </summary>
        /// <param name="initial">
        /// Initial CRC value for the algorithm. It is zero for the first buffer.
        /// Subsequent buffers should have their initial value set to CRC value returned by previous call to this method.
        /// </param>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        /// <returns>Accumulated CRC-32 of all buffers processed so far.</returns>
        public uint Append(uint initial, ReadOnlySpan<byte> input)
        {
            return AppendInternal(initial, input);
        }

        /// <summary>
        /// Computes CRC-32 from input buffer.
        /// </summary>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        /// <returns>CRC-32 buffer of the input buffer.</returns>
        public byte[] ComputeHashByte(byte[] input)
        {
            _currentCrc = AppendInternal(0, input);
            return HashFinal();
        }

        /// <summary>
        /// Computes CRC-32 from input buffer.
        /// </summary>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        /// <returns>CRC-32 buffer of the input buffer.</returns>
        public byte[] ComputeHashByte(ReadOnlySpan<byte> input)
        {
            _currentCrc = AppendInternal(0, input);
            return HashFinal();
        }

        /// <summary>
        /// Computes CRC-32 from input buffer.
        /// </summary>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <param name="offset">Offset of the input data within the buffer.</param>
        /// <param name="length">Length of the input data in the buffer.</param>
        /// <returns>CRC-32 of the data in the buffer.</returns>
        public uint Compute(byte[] input, int offset, int length)
        {
            return Append(0, input, offset, length);
        }

        /// <summary>
        /// Computes CRC-32 from input buffer.
        /// </summary>
        /// <param name="input">Input buffer containing data to be checksummed.</param>
        /// <returns>CRC-32 of the data in the buffer.</returns>
        public uint Compute(byte[] input)
        {
            return Append(0, input);
        }

        /// <summary>
        /// Computes CRC-32 from input buffer.
        /// </summary>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <returns>CRC-32 of the data in the buffer.</returns>
        public uint Compute(ReadOnlySpan<byte> input)
        {
            return Append(0, input);
        }

        /// <summary>
        /// Computes CRC-32 from input buffer and writes it after end of data (buffer should have 4 bytes reserved space for it). Can be used in conjunction with <see cref="IsValidWithCrcAtEnd(byte[],int,int)"/>
        /// </summary>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <param name="offset">Offset of the input data within the buffer.</param>
        /// <param name="length">Length of the input data in the buffer.</param>
        /// <returns>CRC-32 of the data in the buffer.</returns>
        public uint ComputeAndWriteToEnd(byte[] input, int offset, int length)
        {
            if (length + 4 > input.Length)
                throw new ArgumentOutOfRangeException("length", "Length of data should be less than array length - 4 bytes of CRC data");
            var crc = Append(0, input, offset, length);
            var r = offset + length;
            input[r] = (byte)crc;
            input[r + 1] = (byte)(crc >> 8);
            input[r + 2] = (byte)(crc >> 16);
            input[r + 3] = (byte)(crc >> 24);
            return crc;
        }

        /// <summary>
        /// Computes CRC-32 from input buffer - 4 bytes and writes it as last 4 bytes of buffer. Can be used in conjunction with <see cref="IsValidWithCrcAtEnd(byte[])"/>
        /// </summary>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <returns>CRC-32 of the data in the buffer.</returns>
        public uint ComputeAndWriteToEnd(byte[] input)
        {
            if (input.Length < 4)
                throw new ArgumentOutOfRangeException("input", "Input array should be 4 bytes at least");
            return ComputeAndWriteToEnd(input, 0, input.Length - 4);
        }

        /// <summary>
        /// Validates correctness of CRC-32 data in source buffer with assumption that CRC-32 data located at end of buffer in reverse bytes order. Can be used in conjunction with <see cref="ComputeAndWriteToEnd(byte[],int,int)"/>
        /// </summary>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <param name="offset">Offset of the input data within the buffer.</param>
        /// <param name="lengthWithCrc">Length of the input data in the buffer with CRC-32 bytes.</param>
        /// <returns>Is checksum valid.</returns>
        public bool IsValidWithCrcAtEnd(byte[] input, int offset, int lengthWithCrc)
        {
            return Append(0, input, offset, lengthWithCrc) == 0x2144DF1C;
        }

        /// <summary>
        /// Validates correctness of CRC-32 data in source buffer with assumption that CRC-32 data located at end of buffer in reverse bytes order. Can be used in conjunction with <see cref="ComputeAndWriteToEnd(byte[],int,int)"/>
        /// </summary>
        /// <param name="input">Input buffer with data to be checksummed.</param>
        /// <returns>Is checksum valid.</returns>
        public bool IsValidWithCrcAtEnd(byte[] input)
        {
            if (input.Length < 4)
                throw new ArgumentOutOfRangeException("input", "Input array should be 4 bytes at least");
            return Append(0, input, 0, input.Length) == 0x2144DF1C;
        }

        /// <summary>
        /// Resets internal state of the algorithm. Used internally.
        /// </summary>
        public override void Initialize()
        {
            _currentCrc = 0;
        }

        /// <summary>
        /// Appends CRC-32 from given buffer
        /// </summary>
        protected override void HashCore(byte[] input, int offset, int length)
        {
            _currentCrc = AppendInternal(_currentCrc, input, offset, length);
        }

        /// <summary>
        /// Appends CRC-32 from given buffer
        /// </summary>
        protected override void HashCore(ReadOnlySpan<byte> source)
        {
            _currentCrc = AppendInternal(_currentCrc, source);
        }

        /// <summary>
        /// Computes CRC-32 from <see cref="HashCore(byte[], int, int)"/>
        /// </summary>
        protected override byte[] HashFinal()
        {
            if (_isBigEndian)
                return new[] { (byte)(_currentCrc >> 24), (byte)(_currentCrc >> 16), (byte)(_currentCrc >> 8), (byte)_currentCrc };
            else
                return new[] { (byte)_currentCrc, (byte)(_currentCrc >> 8), (byte)(_currentCrc >> 16), (byte)(_currentCrc >> 24) };
        }

        /// <summary>
        /// Computes CRC-32 from <see cref="HashCore(ReadOnlySpan{byte})"/>
        /// </summary>
        protected override bool TryHashFinal(Span<byte> destination, out int bytesWritten)
        {
            if (destination.Length < 4)
            {
                bytesWritten = 0;
                return false;
            }

            if (_isBigEndian)
            {
                BinaryPrimitives.WriteUInt32BigEndian(destination, _currentCrc);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(destination, _currentCrc);
            }

            bytesWritten = 4;
            return true;
        }

        /// <summary>
        /// Get final hash from processed buffer
        /// </summary>
        public override byte[] Hash
        {
            get
            {
                return HashFinal();
            }
        }

        private uint AppendInternal(uint initial, byte[] input, int offset, int length)
        {
            if (length > 0)
            {
                return _proxy.Append(initial, input, offset, length);
            }
            else
                return initial;
        }

        private uint AppendInternal(uint initial, ReadOnlySpan<byte> input)
        {
            if (input.Length > 0)
            {
                return _proxy.Append(initial, input);
            }
            else
                return initial;
        }
    }
}
