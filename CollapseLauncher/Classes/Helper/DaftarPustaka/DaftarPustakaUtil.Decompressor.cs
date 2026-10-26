// ReSharper disable IdentifierTypo
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if NET11_0_OR_GREATER
using ZstandardNativeStream = System.IO.Compression.ZstandardStream;
using ZstandardNativeDecompressionOptions = System.IO.Compression.ZstandardDecompressionOptions;
#else
using System.Collections.Generic;
using ZstandardNativeStream = ZstdNet.DecompressionStream;
using ZstandardNativeDecompressionParameter = ZstdNet.ZSTD_dParameter;
using ZstandardNativeDecompressionOptions = ZstdNet.DecompressionOptions;
#endif

#if !NET11_0_OR_GREATER
using ZstdNet;
using ZstandardManagedDecompressorParameter = ZstdSharp.Unsafe.ZSTD_dParameter;
using ZstandardManagedStream = ZstdSharp.DecompressionStream;
#endif
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.DaftarPustaka;

internal static partial class DaftarPustakaUtil
{
    public static class Decompressor
    {
        public static BrotliStream CreateBrotliStream(Stream source, bool leaveOpen) =>
            new(source, CompressionMode.Decompress, leaveOpen);

        /// <summary>
        /// Creates a ZStandard Decompression Stream.<br/><br/>
        ///
        /// Field Structure:<br/>
        /// <code>
        /// +----------------------+<br/>
        /// | (I4) Dictionary Size |<br/>
        /// +----------------------+<br/>
        /// |   Dictionary Blob    |<br/>
        /// +----------------------+<br/>
        /// | (I4) Max Window Log2 |<br/>
        /// +----------------------+<br/>
        /// | Compressed Data Blob |<br/>
        /// +----------------------+
        /// </code>
        /// </summary>
        /// <param name="source">The source <see cref="Stream"/> to encrypt.</param>
        /// <param name="leaveOpen">Whether to dispose or keeping it open after disposing the decompression stream.</param>
        /// <returns>An instance of the ZStandard Decompression <see cref="Stream"/>.</returns>
        public static Stream CreateZStandardStream(Stream source, bool leaveOpen)
        {
            byte[] dictionaryBuffer = source.ReadArray<byte>();
            int    maxWindowLog2    = source.Read<int>();

#if NET11_0_OR_GREATER
            const int quality = 0;
            ZstandardDictionary? dictionary = dictionaryBuffer.Length == 0
                ? null
                : ZstandardDictionary.Create(dictionaryBuffer, quality);
            
            return new ZstandardNativeStream(source, new ZstandardNativeDecompressionOptions
            {
                Dictionary    = dictionary,
                MaxWindowLog2 = maxWindowLog2
            }, leaveOpen);
#else
            if (DllUtils.IsLibraryExist(DllUtils.DllName))
            {
                var parameters = new Dictionary<ZstandardNativeDecompressionParameter, int>
                {
                    { ZstandardNativeDecompressionParameter.ZSTD_d_windowLogMax, maxWindowLog2 }
                };
                return new ZstandardNativeStream(source,
                                                 new ZstandardNativeDecompressionOptions(dictionaryBuffer.Length == 0 ? null : dictionaryBuffer, parameters),
                                                 leaveOpen: leaveOpen);
            }

            ZstandardManagedStream managedStream = new(source, leaveOpen: leaveOpen);
            managedStream.SetParameter(ZstandardManagedDecompressorParameter.ZSTD_d_windowLogMax, maxWindowLog2);

            if (dictionaryBuffer.Length > 0)
                managedStream.LoadDictionary(dictionaryBuffer);

            return managedStream;
#endif
        }

        public static DeflateStream CreateDeflateStream(Stream source, bool leaveOpen)
            => new(source, CompressionMode.Decompress, leaveOpen);

        public static ZLibStream CreateZlibStream(Stream source, bool leaveOpen)
            => new(source, CompressionMode.Decompress, leaveOpen);

        public static ZLibStream CreateLzmaStream(Stream source, bool leaveOpen)
            => throw new NotImplementedException();

        public static ZLibStream CreateBZip2Stream(Stream source, bool leaveOpen)
            => throw new NotImplementedException();
    }

    extension(Stream stream)
    {
        [SkipLocalsInit]
        internal unsafe T Read<T>()
            where T : unmanaged
        {
            T          result = default;
            Span<byte> buffer = MemoryMarshal.AsBytes(new Span<T>(ref result));

            int read = stream.Read(buffer);
            return read != sizeof(T)
                ? throw new EndOfStreamException("Stream has been reaching end-of-data")
                : result;
        }

        [SkipLocalsInit]
        internal unsafe T[] ReadArray<T>()
            where T : unmanaged
        {
            int elementCount = stream.Read<int>();
            ArgumentOutOfRangeException.ThrowIfLessThan(elementCount, 0);

            return stream.ReadArray<T>(elementCount);
        }

        internal unsafe T[] ReadArray<T>(int elementCount)
            where T : unmanaged
        {
            if (elementCount == 0)
                return [];

            T[]        array        = GC.AllocateUninitializedArray<T>(elementCount);
            Span<byte> arrayAsBytes = MemoryMarshal.AsBytes(array.AsSpan());

            while (!arrayAsBytes.IsEmpty)
            {
                int read = stream.Read(arrayAsBytes);
                arrayAsBytes = arrayAsBytes[read..];

                if (read == 0)
                    throw new EndOfStreamException($"Stream is reaching end-of-data while reading array for {typeof(T)}. Expecting: {array.Length} bytes, but got: {array.Length - arrayAsBytes.Length} bytes instead");
            }

            return array;
        }
    }
}
