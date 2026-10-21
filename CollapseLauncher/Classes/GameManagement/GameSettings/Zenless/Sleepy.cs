/*
 * Initial Implementation Credit by: @Shatyuka
 */

using CollapseLauncher.Helper.StreamUtility;
using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable RedundantUnsafeContext
// ReSharper disable UnusedMember.Local
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.GameSettings.Zenless;

#nullable enable
internal static unsafe class Sleepy
{
    // https://github.com/dotnet/runtime/blob/a7efcd9ca9255dc9faa8b4a2761cdfdb62619610/src/libraries/System.Runtime.Serialization.Formatters/src/System/Runtime/Serialization/Formatters/Binary/BinaryEnums.cs#L7C1-L32C6
    private enum BinaryHeaderEnum : byte
    {
        SerializedStreamHeader    = 0,
        Object                    = 1,
        ObjectWithMap             = 2,
        ObjectWithMapAssemId      = 3,
        ObjectWithMapTyped        = 4,
        ObjectWithMapTypedAssemId = 5,
        ObjectString              = 6,
        Array                     = 7,
        MemberPrimitiveTyped      = 8,
        MemberReference           = 9,
        ObjectNull                = 10,
        MessageEnd                = 11,
        Assembly                  = 12,
        ObjectNullMultiple256     = 13,
        ObjectNullMultiple        = 14,
        ArraySinglePrimitive      = 15,
        ArraySingleObject         = 16,
        ArraySingleString         = 17,
        CrossAppDomainMap         = 18,
        CrossAppDomainString      = 19,
        CrossAppDomainAssembly    = 20,
        MethodCall                = 21,
        MethodReturn              = 22,
    }

    internal static string ReadString(string filePath, ReadOnlySpan<byte> magic)
    {
        // Get the FileInfo
        FileInfo fileInfo = new FileInfo(filePath).StripAlternateDataStream().EnsureNoReadOnly(out bool isExist);
        if (!isExist)
            throw new FileNotFoundException("[Sleepy::ReadString] File does not exist!");

        // Open the stream and get the thing
        using FileStream stream = fileInfo.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadString(stream, magic);
    }

    [SkipLocalsInit]
    internal static string ReadString(Stream stream, ReadOnlySpan<byte> magic)
    {
        // Stream assertion
        if (!stream.CanRead) throw new ArgumentException("[Sleepy::ReadString] Stream must be readable!", nameof(stream));

        // Assign the reader
        using BinaryReader reader = new(stream, Encoding.UTF8, true);

        // Emulate and Assert the BinaryFormatter header
        reader.EmulateReadAssert();

        // Get the data length
        int length = reader.Read7BitEncodedInt();

        // Alloc temporary buffers
        Span<bool> evil         = stackalloc bool[magic.Length];
        byte[]     evilBuffer   = ArrayPool<byte>.Shared.Rent(length);
        char[]     unevilBuffer = ArrayPool<char>.Shared.Rent(length);

        // Read evil data to evil buffer >:)
        reader.BaseStream.ReadExactly(evilBuffer, 0, length);

        try
        {
            // Do the do
            CreateEvil(magic, evil);

            // Do the do (pt. 2)
            int j = InternalRead(magic,
                                 evil,
                                 evilBuffer.AsSpan(0, length),
                                 unevilBuffer.AsSpan(0, length));

            // Emulate and Assert the BinaryFormatter footer
            reader.EmulateReadAssertMessageEnd();

            // Return
            return new string(unevilBuffer, 0, j);
        }
        finally
        {
            // Return and clear the buffer, to only returns the return string.
            evil.Clear();
            ArrayPool<byte>.Shared.Return(evilBuffer, true);
            ArrayPool<char>.Shared.Return(unevilBuffer, true);
        }
    }

    internal static void WriteString(string filePath, ReadOnlySpan<char> content, ReadOnlySpan<byte> magic)
    {
        // Ensure the folder always exist
        string? fileDir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(fileDir))
        {
            Directory.CreateDirectory(fileDir);
        }

        // Get the FileInfo
        FileInfo fileInfo = new FileInfo(filePath).StripAlternateDataStream().EnsureNoReadOnly();

        // Create the stream and write the thing
        using FileStream stream = fileInfo.Open(FileMode.Create, FileAccess.Write, FileShare.Write);
        WriteString(stream, content, magic);
    }

    [SkipLocalsInit]
    internal static void WriteString(Stream stream, ReadOnlySpan<char> content, ReadOnlySpan<byte> magic)
    {
        // Stream assertion
        if (!stream.CanWrite) throw new ArgumentException("[Sleepy::WriteString] Stream must be writable!", nameof(stream));

        // Magic assertion
        if (magic.Length == 0) throw new ArgumentException("[Sleepy::WriteString] Magic cannot be empty!", nameof(magic));

        // Assign the writer
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);

        // Emulate to write the BinaryFormatter header
        writer.EmulateWrite();

        // Do the do
        int contentLen = content.Length;
        int bufferLen  = Encoding.UTF8.GetMaxByteCount(contentLen);

        // Alloc temporary buffers
        Span<bool> evil         = stackalloc bool[magic.Length];
        byte[]     evilBuffer   = ArrayPool<byte>.Shared.Rent(bufferLen);
        byte[]     unevilBuffer = ArrayPool<byte>.Shared.Rent(bufferLen);

        try
        {
            // Encode content to unevil UTF-8 buffer
            int unevilBufferLen = Encoding.UTF8.GetBytes(content, unevilBuffer);

            // Do the do
            CreateEvil(magic, evil);

            // Do the do (pt. 2)
            int h = InternalWrite(magic,
                                  evil,
                                  evilBuffer,
                                  unevilBuffer.AsSpan(0, unevilBufferLen));

            writer.Write7BitEncodedInt(h);
            writer.BaseStream.Write(evilBuffer, 0, h);
            writer.EmulateWriteMessageEnd();
        }
        finally
        {
            // Return and clear the buffer.
            evil.Clear();
            ArrayPool<byte>.Shared.Return(evilBuffer,   true);
            ArrayPool<byte>.Shared.Return(unevilBuffer, true);
        }
    }

    private static int InternalRead(
        ReadOnlySpan<byte>        magic,
        scoped ReadOnlySpan<bool> evil,
        ReadOnlySpan<byte>        evilBuffer,
        Span<char>                unevilBuffer)
    {
        bool eepy = false;

        int j = 0;
        int i = 0;

    amimir:
        int  n  = i % magic.Length;
        byte c  = evilBuffer[i];
        byte ch = (byte)(c ^ magic[n]);

        if (evil[n])
        {
            eepy = ch != 0;
        }
        else
        {
            if (eepy)
            {
                ch   += 0x40;
                eepy =  false;
            }
            unevilBuffer[j++] = (char)ch;
        }

        if (++i < evilBuffer.Length) goto amimir;
        return j;
    }

    private static int InternalWrite(
        ReadOnlySpan<byte>        magic,
        scoped ReadOnlySpan<bool> evil,
        Span<byte>                evilBuffer,
        ReadOnlySpan<byte>        unevilBuffer)
    {
        int h = 0;
        int i = 0;
        int j = 0;

    amimir:
        int  n  = i % magic.Length;
        byte ch = unevilBuffer[j];
        if (evil[n])
        {
            byte eepy = 0;
            if (unevilBuffer[j] >= 0x40)
            {
                ch   -= 0x40;
                eepy =  1;
            }
            evilBuffer[h++] = (byte)(eepy ^ magic[n]);

            n = ++i % magic.Length;
        }

        evilBuffer[h++] = (byte)(ch ^ magic[n]);
        ++i;
        ++j;
        if (j < unevilBuffer.Length) goto amimir;
        return h;
    }

    private static void CreateEvil(ReadOnlySpan<byte> magic, scoped Span<bool> evilist)
    {
        int magicLength = magic.Length;
        int i           = 0;

    evilist:
        int n = i % magicLength;
        evilist[i] = (magic[n] & 0xC0) == 0xC0;

        if (++i < magicLength) goto evilist;
    }

    extension(BinaryReader reader)
    {
        private void EmulateReadAssert()
        {
            // Check if the record type is a SerializedStreamHeader
            reader.ReadAssert(BinaryHeaderEnum.SerializedStreamHeader);

            // Check if Root object ID == 1
            reader.ReadAssert(1);

            // Check if No header object is required
            reader.ReadAssert(-1);

            // Check if the major version is 1
            reader.ReadAssert(1);

            // Check if the minor version is 0
            reader.ReadAssert(0);

            // Check if the record type is an ObjectString
            reader.ReadAssert(BinaryHeaderEnum.ObjectString);

            // Check if Root object ID == 1
            reader.ReadAssert(1);
        }

        private void EmulateReadAssertMessageEnd() =>
            reader.ReadAssert(BinaryHeaderEnum.MessageEnd);

        [SkipLocalsInit]
        private void ReadAssert<T>(T assertWith)
            where T : unmanaged
        {
            Span<byte> buffer = stackalloc byte[sizeof(T)];
            _ = reader.BaseStream.Read(buffer);

            ref T thisEnum = ref MemoryMarshal.AsRef<T>(buffer);
            if (IsEqual(ref thisEnum, ref assertWith))
                return;

            throw new InvalidDataException($"[Sleepy::LogAssertInfo] BinaryFormatter header is not valid at stream pos: {reader.BaseStream.Position:x8}. Expecting value: {assertWith} but getting: {thisEnum} instead!");
        }
    }


    extension(BinaryWriter writer)
    {
        private void EmulateWrite()
        {
            // Emulate to write Sleepy BinaryFormatter header information
            writer.Write(BinaryHeaderEnum.SerializedStreamHeader);
            writer.Write(1);
            writer.Write(-1);
            writer.Write(1);
            writer.Write(0);
            writer.Write(BinaryHeaderEnum.ObjectString);
            writer.Write(1);
        }

        // Emulate to write Sleepy BinaryFormatter footer EOF
        private void EmulateWriteMessageEnd() =>
            writer.Write(BinaryHeaderEnum.MessageEnd);

        private void Write<T>(T value)
            where T : unmanaged
        {
            ReadOnlySpan<byte> buffer = MemoryMarshal.AsBytes(new ReadOnlySpan<T>(ref value));
            writer.BaseStream.Write(buffer);
        }
    }

    private static bool IsEqual<T>(ref T from, ref T to)
        where T : unmanaged
        => sizeof(T) switch
        {
            1  => Unsafe.As<T, byte>(ref from) == Unsafe.As<T, byte>(ref to),
            2  => Unsafe.As<T, short>(ref from) == Unsafe.As<T, short>(ref to),
            4  => Unsafe.As<T, int>(ref from) == Unsafe.As<T, int>(ref to),
            8  => Unsafe.As<T, long>(ref from) == Unsafe.As<T, long>(ref to),
            16 => Unsafe.As<T, Int128>(ref from) == Unsafe.As<T, Int128>(ref to),
            _  => MemoryMarshal.AsBytes(new Span<T>(ref from)).SequenceEqual(MemoryMarshal.AsBytes(new Span<T>(ref to)))
        };
}