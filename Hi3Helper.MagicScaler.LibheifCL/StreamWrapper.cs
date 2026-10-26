using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// ReSharper disable IdentifierTypo
namespace Hi3Helper.MagicScaler.LibheifCL;

internal unsafe struct StreamWrapper : IDisposable
{
    public const  int MaxEncodedBytes = 128 * 1024 * 1024;
    private const int InitialCapacity = 64 * 1024;

    private nint _buffer;

    private StreamWrapper(nint buffer, uint length)
    {
        _buffer = buffer;
        Length = length;
    }

    public uint Length { get; }

    public nint Buffer => _buffer;

    public ReadOnlySpan<byte> Span
    {
        get
        {
            return _buffer == nint.Zero
                ? throw new ObjectDisposedException(nameof(StreamWrapper))
                : new ReadOnlySpan<byte>((void*)_buffer, checked((int)Length));
        }
    }

    public static StreamWrapper Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        // Preserve the previous behavior for seekable streams. Non-seekable
        // streams are read from their current position.
        if (stream.CanSeek)
            stream.Seek(0, SeekOrigin.Begin);

        nuint capacity = InitialCapacity;
        byte* buffer = (byte*)NativeMemory.Alloc(capacity);

        if (buffer is null)
            throw new OutOfMemoryException();

        int        length   = 0;
        Span<byte> overflow = stackalloc byte[1];

        try
        {
            while (true)
            {
                if ((nuint)length == capacity)
                {
                    if (capacity >= MaxEncodedBytes)
                    {
                        int overflowRead = stream.Read(overflow);

                        if (overflowRead == 0)
                            break;

                        if (overflowRead != 1)
                        {
                            throw new COMException("Invalid stream read count.");
                        }

                        throw new COMException("Encoded image exceeds the sample limit.");
                    }

                    nuint newCapacity = Math.Min(capacity * 2, MaxEncodedBytes);
                    byte* resized = (byte*)NativeMemory.Realloc(buffer, newCapacity);
                    if (resized is null) throw new OutOfMemoryException();

                    buffer = resized;
                    capacity = newCapacity;
                }

                int available = checked((int)(capacity - (nuint)length));

                Span<byte> destination = new(buffer + length, available);
                int read = stream.Read(destination);

                // Protect against incorrectly implemented custom streams.
                if ((uint)read > (uint)available)
                {
                    throw new COMException("Invalid stream read count.");
                }

                if (read == 0)
                    break;

                length = checked(length + read);
            }

            return new StreamWrapper((nint)buffer, checked((uint)length));
        }
        catch
        {
            NativeMemory.Free(buffer);
            throw;
        }
    }

    public void Dispose()
    {
        nint buffer = Interlocked.Exchange(ref _buffer, nint.Zero);
        if (buffer != nint.Zero) NativeMemory.Free((void*)buffer);
    }
}