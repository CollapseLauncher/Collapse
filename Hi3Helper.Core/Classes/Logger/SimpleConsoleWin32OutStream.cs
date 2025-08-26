using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable CheckNamespace
#pragma warning disable SYSLIB1054

namespace Hi3Helper;

internal sealed class SimpleConsoleWin32OutStream : Stream
{
    private readonly unsafe void* _consoleHandleUnsafe;
    private readonly        bool  _isFreeConsoleHandle;

    internal unsafe SimpleConsoleWin32OutStream(nint consoleHandle, bool freeConsoleHandle = false)
    {
        _consoleHandleUnsafe = (void*)consoleHandle;
        _isFreeConsoleHandle = freeConsoleHandle;
    }

    [DllImport("Kernel32.dll", EntryPoint = "WriteConsoleA")]
    private static extern unsafe int WriteConsole(void* hConsoleOutput,
                                                  void* lpBuffer,
                                                  uint  nNumberOfCharsToWrite,
                                                  uint* lpNumberOfCharsWritten,
                                                  void* lpReserved);

    [DllImport("Kernel32.dll", EntryPoint = "CloseHandle")]
    private static extern unsafe int CloseHandle(void* handle);

    public override void Flush()
    {
        // NOP: Intended. Reason: Console doesn't have flush function.
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override void Write(byte[] buffer, int offset, int count)
        => Write(buffer.AsSpan(offset, count));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override unsafe void Write(ReadOnlySpan<byte> buffer)
    {
        void* ptr = Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer));
        WriteCore(ptr, (uint)buffer.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe void WriteCore(void* buffer, uint len)
    {
        uint written = 0;

        Write:
        int result = WriteConsole(_consoleHandleUnsafe, buffer, len, &written, null);
        Marshal.ThrowExceptionForHR(result);
        len -= written;
        if (len > 0)
        {
            buffer = Unsafe.Add<byte>(buffer, (int)written);
            goto Write;
        }
    }

    protected override unsafe void Dispose(bool disposing)
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
