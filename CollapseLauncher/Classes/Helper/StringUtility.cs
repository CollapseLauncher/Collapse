using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
#pragma warning disable IDE0130

// ReSharper disable ConvertIfStatementToSwitchStatement

#nullable enable
namespace CollapseLauncher.Helper;

public static class StringUtility
{
    public static bool TryGetStreamFromUrlDataString(
        ReadOnlySpan<char>                    originalUriString,
        out                     string?       mimeType,
        [NotNullWhen(true)] out MemoryStream? stream)
    {
        const string dataScheme = "data:";

        Unsafe.SkipInit(out mimeType);
        Unsafe.SkipInit(out stream);

        if (originalUriString.IsEmpty)
        {
            return false;
        }

        // -- Check if data is entirely a raw base64 string
        if (TryGetStreamFromPureBase64String(originalUriString, out stream))
        {
            return true;
        }

        // -- Check if data is entirely a hex string
        if (TryGetStreamFromPureHexString(originalUriString, out stream))
        {
            return true;
        }

        // -- Check for data scheme
        int indexOfDataToken = originalUriString.IndexOf(dataScheme);
        if (indexOfDataToken == -1)
        {
            return false;
        }

        // -- Check for data section
        int indexOfDataSection = originalUriString.IndexOf(',');
        if (indexOfDataSection == -1)
        {
            return false;
        }

        // -- Try to get data MIME type
        ReadOnlySpan<char> mimeToken = originalUriString[dataScheme.Length..indexOfDataSection];
        if (!mimeToken.IsEmpty)
        {
            mimeType = new string(mimeToken);
        }

        // -- Try to decode the data from base64 string
        ReadOnlySpan<char> dataSpan = originalUriString[indexOfDataSection..];
        return TryGetStreamFromPureBase64String(dataSpan, out stream) ||
               // -- The data is probably a hex string?
               TryGetStreamFromPureHexString(dataSpan, out stream) ||
               // -- uhm, probably escaped URL-encoded string? if all these fails, return false.
               TryGetStreamFromEscapedUrlString(dataSpan, out stream);
    }

    private static bool TryGetStreamFromPureBase64String(
        ReadOnlySpan<char>                    span,
        [NotNullWhen(true)] out MemoryStream? stream)
    {
        Unsafe.SkipInit(out stream);

        int     bufferLen = Base64Url.GetMaxDecodedLength(span.Length);
        byte[]? buffer;

        // -- Allocate test buffer first before allocating real buffer
        int        testBufferLen = Math.Min(1 << 10, span.Length);
        Span<byte> bufferSpan    = stackalloc byte[testBufferLen];

        // -- Try to decode the first with test buffer first.
        //    If succeeded, then do hot-path copy and allocate stream.
        OperationStatus operation = Base64Url.DecodeFromChars(span, bufferSpan, out int offset, out int written);
        if (operation == OperationStatus.Done)
        {
            buffer = GC.AllocateUninitializedArray<byte>(written);
            bufferSpan[..written].CopyTo(buffer); // Copy stack to heap buffer

            stream = new MemoryStream(buffer, 0, written);
            return true;
        }

        // -- If failure happened, return false
        if (operation is OperationStatus.InvalidData or OperationStatus.NeedMoreData)
        {
            return false;
        }

        // -- If more data need to be processed, continue.
        //    But first, copy the previous first test stack buffer to heap buffer.
        buffer = GC.AllocateUninitializedArray<byte>(bufferLen);
        bufferSpan[..written].CopyTo(buffer);

        // -- Set buffer span and source span, then continue decoding the rest of the data.
        bufferSpan = buffer.AsSpan(written);
        span       = span[offset..];

        operation = Base64Url.DecodeFromChars(span, bufferSpan, out _, out int writtenNext);
        if (operation != OperationStatus.Done)
        {
            // :/ We wasted some memory due to failure on decoding.
            // Though, return false.
            return false;
        }

        // -- Then, get the memory stream instance.
        written += writtenNext; // Advance written bytes.
        stream  =  new MemoryStream(buffer, 0, written);
        return true;
    }

    private static bool TryGetStreamFromEscapedUrlString(
        ReadOnlySpan<char>                    span,
        [NotNullWhen(true)] out MemoryStream? stream)
    {
        Unsafe.SkipInit(out stream);

        char[] bufferRent = ArrayPool<char>.Shared.Rent(span.Length);
        try
        {
            if (!Uri.TryUnescapeDataString(span, bufferRent, out int unescapeWritten))
            {
                return false;
            }

            Span<char> bufferRentSpan = bufferRent.AsSpan(0, unescapeWritten);

            int    utf8BufferLen = Encoding.UTF8.GetByteCount(bufferRentSpan);
            byte[] utf8Buffer    = GC.AllocateUninitializedArray<byte>(utf8BufferLen);

            int utf8Written = Encoding.UTF8.GetBytes(bufferRentSpan, utf8Buffer);
            stream = new MemoryStream(utf8Buffer, 0, utf8Written);
            return true;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(bufferRent);
        }
    }

    private static bool TryGetStreamFromPureHexString(
        ReadOnlySpan<char>                    span,
        [NotNullWhen(true)] out MemoryStream? stream)
    {
        Unsafe.SkipInit(out stream);

        span = span.Trim(); // Trim possible spaces
        if (span.Length % 2 != 0) // Not even? definitely invalid.
        {
            return false;
        }

        int     bufferLen = span.Length / 2;
        byte[]? buffer;

        // -- Allocate test buffer first before allocating real buffer
        int        testBufferLen = Math.Min(1 << 10, bufferLen);
        Span<byte> bufferSpan    = stackalloc byte[testBufferLen];

        // -- Try to decode the first with test buffer first.
        //    If succeeded, then do hot-path copy and allocate stream.
        OperationStatus operation = Convert.FromHexString(span, bufferSpan, out int offset, out int written);
        if (operation == OperationStatus.Done)
        {
            buffer = GC.AllocateUninitializedArray<byte>(written);
            bufferSpan[..written].CopyTo(buffer); // Copy stack to heap buffer

            stream = new MemoryStream(buffer, 0, written);
            return true;
        }

        // -- If failure happened, return false
        if (operation is OperationStatus.InvalidData or OperationStatus.NeedMoreData)
        {
            return false;
        }

        // -- If more data need to be processed, continue.
        //    But first, copy the previous first test stack buffer to heap buffer.
        buffer = GC.AllocateUninitializedArray<byte>(bufferLen);
        bufferSpan[..written].CopyTo(buffer);

        // -- Set buffer span and source span, then continue decoding the rest of the data.
        bufferSpan = buffer.AsSpan(written);
        span       = span[offset..];

        operation = Convert.FromHexString(span, bufferSpan, out _, out int writtenNext);
        if (operation != OperationStatus.Done)
        {
            // :/ We wasted some memory due to failure on decoding.
            // Though, return false.
            return false;
        }

        // -- Then, get the memory stream instance.
        written += writtenNext; // Advance written bytes.
        stream  =  new MemoryStream(buffer, 0, written);
        return true;
    }
}
