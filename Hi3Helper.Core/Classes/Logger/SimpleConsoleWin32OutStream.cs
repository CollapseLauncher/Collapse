using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable CheckNamespace
#pragma warning disable SYSLIB1054

namespace Hi3Helper;

internal sealed unsafe class SimpleConsoleWin32OutStream : Stream
{
    private readonly void* _consoleHandleUnsafe;
    private readonly bool  _isFreeConsoleHandle;

    internal SimpleConsoleWin32OutStream(nint consoleHandle, bool freeConsoleHandle = false)
    {
        _consoleHandleUnsafe = (void*)consoleHandle;
        _isFreeConsoleHandle = freeConsoleHandle;
    }

    static SimpleConsoleWin32OutStream()
    {
        WriteConsole = &WriteConsoleNop;
        CloseHandle  = &CloseHandleNop;

        if (!NativeLibrary.TryLoad("Kernel32.dll", out nint kernel32P))
        {
            return;
        }

        if (NativeLibrary.TryGetExport(kernel32P,
                                       "WriteConsoleA",
                                       out nint writeConsoleD))
        {
            WriteConsole = (delegate* unmanaged[Stdcall]<void*, void*, int, int*, void*, int>)writeConsoleD;
        }

        if (NativeLibrary.TryGetExport(kernel32P,
                                       "CloseHandle",
                                       out nint closeHandleD))
        {
            CloseHandle = (delegate* unmanaged[Stdcall]<void*, int>)closeHandleD;
        }
    }

    [SkipLocalsInit]
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int WriteConsoleNop(void* hConsoleOutput,
                                       void* lpBuffer,
                                       int   nNumberOfCharsToWrite,
                                       int*  lpNumberOfCharsWritten,
                                       void* lpReserved)
    {
        if (lpNumberOfCharsWritten != null)
            *lpNumberOfCharsWritten = nNumberOfCharsToWrite;

        return 1; // Always returns true
    }

    [SkipLocalsInit]
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int CloseHandleNop(void* handle) => 1; // Always returns true

    private static readonly delegate* unmanaged[Stdcall]<void*, void*, int, int*, void*, int> WriteConsole;
    private static readonly delegate* unmanaged[Stdcall]<void*, int>                          CloseHandle;

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
        WriteCore(_consoleHandleUnsafe, ref bufferRef, buffer.Length);
    }

    [SkipLocalsInit]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteCore(void* handle, ref byte buffer, int len) =>
        WriteConsole(handle, Unsafe.AsPointer(ref buffer), len, null, null);

    protected override void Dispose(bool disposing)
    {
        if (!disposing || !_isFreeConsoleHandle)
        {
            return;
        }

        CloseHandle(_consoleHandleUnsafe);
    }

    public override bool CanRead  => false;
    public override bool CanSeek  => false;
    public override bool CanWrite => true;
    public override long Length   => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
}
