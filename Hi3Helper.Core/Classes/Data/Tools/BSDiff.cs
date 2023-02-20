// Original Source:
// https://raw.githubusercontent.com/LogosBible/bsdiff.net/master/src/bsdiff/BinaryPatchUtility.cs

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

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
        private const int c_bufferSize = 0x1000;
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

        /// <summary>
        /// Applies a binary patch (in <a href="http://www.daemonology.net/bsdiff/">bsdiff</a> format) to the data in
        /// input stream and writes the results of patching to output stream defined after initialization.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidDataException"></exception>
        public void Apply()
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

            // prepare to read three parts of the patch in parallel
            using (Stream compressedControlStream = _patchStream())
            using (Stream compressedDiffStream = _patchStream())
            using (Stream compressedExtraStream = _patchStream())
            {
                // seek to the start of each part
                compressedControlStream.Position += c_headerSize;
                compressedDiffStream.Position += c_headerSize + _controlLength;
                compressedExtraStream.Position += c_headerSize + _controlLength + _diffLength;

                // decompress each part (to read it)
                using (GZipStream controlStream = new GZipStream(compressedControlStream, CompressionMode.Decompress))
                using (GZipStream diffStream = new GZipStream(compressedDiffStream, CompressionMode.Decompress))
                using (GZipStream extraStream = new GZipStream(compressedExtraStream, CompressionMode.Decompress))
                {
                    Span<long> control = stackalloc long[3];
                    Span<byte> buffer = stackalloc byte[8];

                    int oldPosition = 0;
                    int newPosition = 0;
                    while (newPosition < _newSize)
                    {
                        // read control data
                        for (int i = 0; i < 3; i++)
                        {
                            controlStream.ReadExactly(buffer);
                            control[i] = ReadInt64Sanity(buffer);
                        }

                        // sanity-check
                        if (newPosition + control[0] > _newSize)
                        {
                            throw new InvalidDataException($"The patch file is corrupted! newPosition + control[0] ({newPosition} + {control[0]}) > newSize ({_newSize})");
                        }

                        // seek old file to the position that the new data is diffed against
                        _inputStream.Position = oldPosition;

                        int bytesToCopy = (int)control[0];
                        while (bytesToCopy > 0)
                        {
                            int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                            // read diff string
                            diffStream.ReadExactly(newData.Slice(0, actualBytesToCopy));

                            // add old data to diff string
                            int availableInputBytes = Math.Min(actualBytesToCopy, (int)(_inputStream.Length - _inputStream.Position));
                            _inputStream.ReadExactly(oldData.Slice(0, availableInputBytes));

                            for (int index = 0; index < availableInputBytes; index++)
                                newData[index] += oldData[index];

                            _outputStream.Write(newData.Slice(0, actualBytesToCopy));

                            // adjust counters
                            newPosition += actualBytesToCopy;
                            oldPosition += actualBytesToCopy;
                            bytesToCopy -= actualBytesToCopy;

                            // Update progress
                            UpdateProgress(_outputStream.Length, _newSize, actualBytesToCopy);
                        }

                        // sanity-check
                        if (newPosition + control[1] > _newSize)
                        {
                            throw new InvalidDataException($"The patch file is corrupted! newPosition + control[1] ({newPosition} + {control[1]}) > newSize ({_newSize})");
                        }

                        // read extra string
                        bytesToCopy = (int)control[1];
                        while (bytesToCopy > 0)
                        {
                            int actualBytesToCopy = Math.Min(bytesToCopy, c_bufferSize);

                            extraStream.ReadExactly(newData.Slice(0, actualBytesToCopy));
                            _outputStream.Write(newData.Slice(0, actualBytesToCopy));

                            newPosition += actualBytesToCopy;
                            bytesToCopy -= actualBytesToCopy;

                            // Update progress
                            UpdateProgress(_outputStream.Length, _newSize, actualBytesToCopy);
                        }

                        // adjust position
                        oldPosition = (int)(oldPosition + control[2]);
                    }
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