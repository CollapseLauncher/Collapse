using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable CheckNamespace
#pragma warning disable SYSLIB1054

namespace Hi3Helper;

internal sealed class SimpleConsoleWin32OutStream : Stream
{
    private readonly nint _consoleHandleUnsafe;
    private readonly bool _isFreeConsoleHandle;

    internal SimpleConsoleWin32OutStream(nint consoleHandle, bool freeConsoleHandle = false)
    {
        _consoleHandleUnsafe = consoleHandle;
        _isFreeConsoleHandle = freeConsoleHandle;
    }

    [DllImport("Kernel32.dll", EntryPoint = "WriteConsoleA")]
    private static extern int WriteConsole(nint     hConsoleOutput,
                                           ref byte lpBuffer,
                                           int      nNumberOfCharsToWrite,
                                           ref int  lpNumberOfCharsWritten,
                                           nint     lpReserved);

    [DllImport("Kernel32.dll", EntryPoint = "CloseHandle")]
    private static extern int CloseHandle(nint handle);

    [SkipLocalsInit]
    public override void Flush()
    {
        // NOP: Intended. Reason: Console doesn't have flush function.
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(byte[] buffer, int offset, int count)
        => Write(buffer.AsSpan(offset, count));

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        ref byte bufferRef = ref MemoryMarshal.GetReference(buffer);
        WriteCore(ref bufferRef, buffer.Length);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteCore(ref byte buffer, int len)
    {
        int written = 0;

    Write:
        int result = WriteConsole(_consoleHandleUnsafe, ref buffer, len, ref written, nint.Zero);
        Marshal.ThrowExceptionForHR(result);
        len -= written;
        if (len > 0)
        {
            buffer = ref Unsafe.Add(ref buffer, written);
            goto Write;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing || !_isFreeConsoleHandle)
        {
            return;
        }

        int hResult = CloseHandle(_consoleHandleUnsafe);
        Marshal.ThrowExceptionForHR(hResult);
    }

    public override bool CanRead  => false;
    public override bool CanSeek  => false;
    public override bool CanWrite => true;
    public override long Length   => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
}
