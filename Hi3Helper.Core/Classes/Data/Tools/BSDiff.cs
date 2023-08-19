// Original Source:
// https://raw.githubusercontent.com/LogosBible/bsdiff.net/master/src/bsdiff/BinaryPatchUtility.cs

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics;
using System.Threading;
using SharpCompress.Compressors.BZip2;

namespace Hi3Helper.Data
{
    /*
	The original bsdiff.c source code (http://www.daemonology.net/bsdiff/) is
	distributed under the following license:

	Copyright 2003-2005 Colin Percival
	All rights reserved

	Redistribution and use in source and binary forms, with or without
	modification, are permitted providing that the following conditions 
	are met:
	1. Redistributions of source code must retain the above copyright
		notice, this list of conditions and the following disclaimer.
	2. Redistributions in binary form must reproduce the above copyright
		notice, this list of conditions and the following disclaimer in the
		documentation and/or other materials provided with the distribution.

	THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
	IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
	ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
	DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
	DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
	OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
	HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
	STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
	IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
	POSSIBILITY OF SUCH DAMAGE.
	*/
    public sealed class BinaryPatchUtility
    {
        private const int c_bufferSize = 16 << 10;
        private Stream _inputStream { get; set; }
        private Stream _outputStream { get; set; }
        private Func<Stream> _patchStream { get; set; }
        private Stopwatch _progressStopwatch { get; set; }
        private BinaryPatchProgress _progress { get; set; }

        private bool _leaveOpenStream { get; set; }
        private bool _canContinueApply { get; set; }

        private long _controlLength { get; set; }
        private long _diffLength { get; set; }
        private long _newSize { get; set; }

        public long GetNewSize() => _newSize;
        public event EventHandler<BinaryPatchProgress> ProgressChanged;
        private void UpdateProgress(long SizePatched, long SizeToBePatched, long Read)
        {
            _progress.UpdatePatchEvent(SizePatched, SizeToBePatched, Read, _progressStopwatch.Elapsed.TotalSeconds);
            ProgressChanged?.Invoke(this, _progress);
        }

        /// <summary>
        /// Initializing Input, Patch and Output stream before applying binary patch/
        /// </summary>
        /// <param name="inputStream">A <see cref="Stream"/> containing the input data.</param>
        /// <param name="patchStream">A func that can open a <see cref="Stream"/> positioned at the start of the patch data.
        /// This stream must support reading and seeking, and <paramref name="patchStream"/> must allow multiple streams on
        /// the patch to be opened concurrently.</param>
        /// <param name="outputStream">A <see cref="Stream"/> to which the patched data is written.</param>
        public void Initialize(Stream inputStream, Func<Stream> patchStream, Stream outputStream, bool leaveOpen = true)
        {
            _inputStream = inputStream;
            _outputStream = outputStream;
            _patchStream = patchStream;
            _leaveOpenStream = leaveOpen;
            _progressStopwatch = Stopwatch.StartNew();

            ReadHeader();
        }

        public void Initialize(string inputPath, string patchPath, string outputPath, bool leaveOpen = false)
        {
            _inputStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _outputStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            _patchStream = () => { return new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read); };
            _leaveOpenStream = leaveOpen;
            _progressStopwatch = Stopwatch.StartNew();

            ReadHeader();
        }

        private void ReadHeader()
        {
            // check arguments
            if (_inputStream == null)
            {
                throw new ArgumentNullException("Input Stream cannot be null!");
            }
            if (_patchStream == null)
            {
                throw new ArgumentNullException("Patch cannot be null!");
            }
            if (_outputStream == null)
            {
                throw new ArgumentNullException("Output Stream cannot be null!");
            }

            /*
			File format:
				0	8	"BSDIFF40"
				8	8	X
				16	8	Y
				24	8	sizeof(newfile)
				32	X	bzip2(control block)
				32+X	Y	bzip2(diff block)
				32+X+Y	???	bzip2(extra block)
			with control block a set of triples (x,y,z) meaning "add x bytes
			from oldfile to x bytes from the diff block; copy y bytes from the
			extra block; seek forwards in oldfile by z bytes".
			*/
            // read header
            using (Stream patchStream = _patchStream())
            {
                // check patch stream capabilities
                if (!patchStream.CanRead)
                {
                    throw new ArgumentException("Patch stream must be readable.", "_patchStream");
                }
                if (!patchStream.CanSeek)
                {
                    throw new ArgumentException("Patch stream must be seekable.", "_patchStream");
                }

                Span<byte> header = stackalloc byte[c_headerSize];
                patchStream.ReadExactly(header);

                // check for appropriate magic
                long signature = ReadInt64Sanity(header);
                if (signature != c_fileSignature)
                {
                    throw new InvalidOperationException("The patch file is not in a valid format!");
                }

                // read lengths from header
                _controlLength = ReadInt64Sanity(header.Slice(8));
                _diffLength = ReadInt64Sanity(header.Slice(16));
                _newSize = ReadInt64Sanity(header.Slice(24));
                if (_controlLength < 0 || _diffLength < 0 || _newSize < 0)
                {
                    throw new InvalidOperationException($"The patch file may be corrupted! control: {_controlLength}, diff: {_diffLength}, newSize: {_newSize}");
                }

                _canContinueApply = true;
            }
        }

        private Stream TryGetCompressionStream(Stream source, long startPosition)
        {
            // Check if the stream is seekable and readable
            if (!source.CanRead) throw new InvalidOperationException("Stream is not readable!");
            if (!source.CanSeek) throw new InvalidOperationException("Stream is not seekable!");

            // Initialize the delegate function to return compressed stream
            Func<Stream>[] streams = new Func<Stream>[]
            {
                () => new CBZip2InputStream(source, true, true), // Commonly used compression in BZip2
                () => new GZipStream(source, CompressionMode.Decompress, true) // GZip compressed BSDiff used in miHoYo Games (like Honkai Impact 3rd)
            };
            Stream tempStream = null;

            // Try finding the compressed stream
            for (int i = 0; i < streams.Length; i++)
            {
                try
                {
                    source.Position = startPosition;
                    tempStream = streams[i]();
                    tempStream.ReadByte();
                    source.Position = startPosition;
                    return streams[i]();
                }
                catch { }
                finally
                {
                    tempStream?.Dispose();
                    tempStream = null;
                }
            }

            throw new FormatException("The diff file has unsupported compression format or the file is damaged/invalid!");
        }

        private long[] ReadControlNumbers(Stream source, long newPosition, long newSize)
        {
            Span<byte> buffer = stackalloc byte[8];
            long[] controls = new long[3];

            source.ReadExactly(buffer);
            controls[0] = ReadInt64Sanity(buffer); // Copy data control number

            source.ReadExactly(buffer);
            controls[1] = ReadInt64Sanity(buffer); // Additional/new data control number

            source.ReadExactly(buffer);
            controls[2] = ReadInt64Sanity(buffer); // Offset data control number

            // SANITY CHECK: If the new position + Copy data control number > new size, then throw.
            if (newPosition + controls[0] > _newSize)
                throw new InvalidDataException($"The patch file is corrupted! newPosition + control[0]/Copy control number ({newPosition} + {controls[0]}) > newSize ({_newSize})");

            // SANITY CHECK: If the copy control or new data control returns negative number, then throw.
            if (controls[0] < 0 || controls[1] < 0)
                throw new InvalidDataException($"The patch file is corrupted! control[0] ({controls[0]}) or control[1] ({controls[1]}) cannot be < 0!");

            return controls;
        }

        /// <summary>
        /// Applies a binary patch (in <a href="http://www.daemonology.net/bsdiff/">bsdiff</a> format) to the data in
        /// input stream and writes the results of patching to output stream defined after initialization.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidDataException"></exception>
        public unsafe void Apply(CancellationToken token = default)
        {
            // check if the apply can be proceed
            if (!_canContinueApply)
            {
                throw new InvalidOperationException($"You must initialize the patch before applying!");
            }

            // restart progress stopwatch
            _progressStopwatch.Restart();
            _progress = new BinaryPatchProgress();

            if (_inputStream.CanSeek) _inputStream.Position = 0;
            if (_outputStream.CanSeek) _outputStream.Position = 0;

            // preallocate buffers for reading and writing
            Span<byte> newData = stackalloc byte[c_bufferSize];
            Span<byte> oldData = stackalloc byte[c_bufferSize];

            // decompress each part (to read it)
            using (Stream controlCompStream = _patchStream())
            using (Stream diffCompStream = _patchStream())
            using (Stream extraCompStream = _patchStream())
            using (Stream controlStream = TryGetCompressionStream(controlCompStream, c_headerSize))
            using (Stream diffStream = TryGetCompressionStream(diffCompStream, c_headerSize + _controlLength))
            using (Stream extraStream = TryGetCompressionStream(extraCompStream, c_headerSize + _controlLength + _diffLength))
            {
                Span<long> control = stackalloc long[3];
                Span<byte> buffer = stackalloc byte[8];

                long oldPosition = 0;
                long newPosition = 0;
                while (newPosition < _newSize)
                {
                    // Get the control array
                    control = ReadControlNumbers(controlStream, newPosition, _newSize);

                    // Get the size to copy
                    long bytesToCopy = control[0];

                    // Start the copy process
                    while (bytesToCopy > 0)
                    {
                        // Throw if cancelation is called
                        token.ThrowIfCancellationRequested();

                        // Get minimum size to copy
                        int actualBytesToCopy = (int)Math.Min(bytesToCopy, c_bufferSize);
                        // Get the minimum size from old data to copy
                        int availableInputBytes = (int)Math.Min(actualBytesToCopy, _inputStream.Length - _inputStream.Position);

                        // Read diff and old data
                        diffStream.ReadExactly(newData.Slice(0, actualBytesToCopy));
                        _inputStream.ReadExactly(oldData.Slice(0, availableInputBytes));

                        // Add the old with new data in vectors
                        fixed (byte* newDataPtr = newData)
                        fixed (byte* oldDataPtr = oldData)
                        {
                            // Get the offset and remained offset
                            int offset;
                            long offsetRemained = c_bufferSize % Vector128<byte>.Count;
                            for (offset = 0; offset < c_bufferSize - offsetRemained; offset += Vector128<byte>.Count)
                            {
                                Vector128<byte> newVector = Sse2.LoadVector128(newDataPtr + offset);
                                Vector128<byte> oldVector = Sse2.LoadVector128(oldDataPtr + offset);
                                Vector128<byte> resultVector = Sse2.Add(newVector, oldVector);

                                Sse2.Store(newDataPtr + offset, resultVector);
                            }

                            // Process the remained data by the last offset
                            while (offset < c_bufferSize) *(newDataPtr + offset) += *(oldDataPtr + offset++);

                            // Write the data into the output
                            _outputStream.Write(newData.Slice(0, actualBytesToCopy));

                            // Adjust counters
                            newPosition += actualBytesToCopy;
                            oldPosition += actualBytesToCopy;
                            bytesToCopy -= actualBytesToCopy;

                            // Update progress
                            UpdateProgress(_outputStream.Length, _newSize, actualBytesToCopy);
                        }
                    }

                    // SANITY CHECK: Check if the new position + Additional/new data has more size than _newSize.
                    //               If yes, then throw.
                    if (newPosition + control[1] > _newSize)
                    {
                        throw new InvalidDataException($"The patch file is corrupted! newPosition + control[1] ({newPosition} + {control[1]}) > newSize ({_newSize})");
                    }

                    // Get the bytes to copy for the additional data (new data)
                    bytesToCopy = (int)control[1];
                    while (bytesToCopy > 0)
                    {
                        // Throw if cancelation is called
                        token.ThrowIfCancellationRequested();

                        // Get the size of the additional data to copy
                        int actualBytesToCopy = (int)Math.Min(bytesToCopy, c_bufferSize);

                        // Read the new data from extra stream and write it to output
                        extraStream.ReadExactly(newData.Slice(0, actualBytesToCopy));
                        _outputStream.Write(newData.Slice(0, actualBytesToCopy));

                        newPosition += actualBytesToCopy;
                        bytesToCopy -= actualBytesToCopy;

                        // Update progress
                        UpdateProgress(_outputStream.Length, _newSize, actualBytesToCopy);
                    }

                    // Adjust the position (either move it towards or behind the current position)
                    oldPosition = oldPosition + control[2];
                }
            }

            if (!_leaveOpenStream)
            {
                _inputStream.Dispose();
                _outputStream.Dispose();
            }

            _canContinueApply = false;
        }

        private unsafe long ReadInt64Sanity(ReadOnlySpan<byte> buf)
        {
            // Reference the buffer into byte pointer
            fixed (byte* ar = buf)
            {
                // Assign sanity number
                byte sanity = ar[7];

                // AND the last number with 0x7F (127)
                ar[7] &= 0x7F;

                // Points the byte pointer to cast as long
                long value = *(long*)ar;

                // If the sanity results non-zero number from AND 0x80 (128), then set it as negative value
                if ((sanity & 0x80) != 0)
                {
                    value *= -1;
                }

                // Return the value
                return value;
            }
        }

        const long c_fileSignature = 0x3034464649445342L;
        const int c_headerSize = 32;
    }

    public class BinaryPatchProgress
    {
        public BinaryPatchProgress()
        {
            this.Speed = 0;
            this.SizePatched = 0;
            this.SizeToBePatched = 0;
            this.Read = 0;
        }

        public void UpdatePatchEvent(long SizePatched, long SizeToBePatched, long Read, double TotalSecond)
        {
            this.Speed = (long)(SizePatched / TotalSecond);
            this.SizePatched = SizePatched;
            this.SizeToBePatched = SizeToBePatched;
            this.Read = Read;
        }

        public long SizePatched { get; private set; }
        public long SizeToBePatched { get; private set; }
        public double ProgressPercentage => Math.Round((SizePatched / (double)SizeToBePatched) * 100, 2);
        public long Read { get; private set; }
        public long Speed { get; private set; }
        public TimeSpan TimeLeft => checked(TimeSpan.FromSeconds((SizeToBePatched - SizePatched) / UnZeroed(Speed)));
        private long UnZeroed(long Input) => Math.Max(Input, 1);
    }
}