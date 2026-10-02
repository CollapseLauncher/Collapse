// ReSharper disable IdentifierTypo
using System;
using System.IO;
using System.IO.Compression;
using CollapseLauncher.Helper.StreamUtility;

#if NET11_0_OR_GREATER
using ZstandardNativeStream = System.IO.Compression.ZstandardStream;
#else
using System.Collections.Generic;
using ZstandardNativeStream = ZstdNet.DecompressionStream;
using ZstandardNativeDecompressionParameter = ZstdNet.ZSTD_dParameter;
using ZstandardNativeDecompressionOptions = ZstdNet.DecompressionOptions;
#endif

#if !NET11_0_OR_GREATER
using ZstandardManagedDecompressorParameter = ZstdSharp.Unsafe.ZSTD_dParameter;
using ZstandardManagedStream = ZstdSharp.DecompressionStream;
using ZstdNet;
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

        public static Stream CreateZStandardStream(Stream source, bool leaveOpen)
        {
            int     dictionaryBufferLength = source.Read<int>();
            int     maxWindowLog2          = source.Read<int>();
            byte[]? dictionaryBuffer       = null;

#if NET11_0_OR_GREATER
            int quality = 0;
#endif

            // Read customized dictionary settings.
            if (dictionaryBufferLength > 0)
            {
#if NET11_0_OR_GREATER
                quality = source.Read<int>();
#else
                _ = source.Read<int>();
#endif
                dictionaryBuffer = source.ReadArray<byte>(dictionaryBufferLength);
            }

#if NET11_0_OR_GREATER
            ZstandardDictionary? dictionary = ZstandardDictionary.Create(dictionaryBuffer, quality);

            return dictionary != null
                ? new ZstandardStream(source, CompressionMode.Decompress, dictionary, leaveOpen)
                : new ZstandardStream(source, new ZstandardDecompressionOptions
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
                                                 new ZstandardNativeDecompressionOptions(dictionaryBuffer, parameters),
                                                 leaveOpen: leaveOpen);
            }

            ZstandardManagedStream managedStream = new(source, leaveOpen: leaveOpen);
            managedStream.SetParameter(ZstandardManagedDecompressorParameter.ZSTD_d_windowLogMax, maxWindowLog2);
            if (dictionaryBuffer != null) managedStream.LoadDictionary(dictionaryBuffer);

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
}
