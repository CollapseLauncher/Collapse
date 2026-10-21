using CollapseLauncher.Helper.StreamUtility;
using System;
using System.IO;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.DaftarPustaka;

internal static partial class DaftarPustakaUtil
{
    public static class Reader
    {
        public static Stream CreateCryptoStream(
            Stream              inputStream,
            ServeV3FileContext? context   = null,
            bool                leaveOpen = false)
        {
            ulong  signature = inputStream.Read<ulong>();
            ushort version   = inputStream.Read<ushort>();

            if (signature != DaftarPustakaSettings.CollapseHeader)
                throw new InvalidDataException("Stream does not contain Daftar Pustaka formatted file.");

            if (version != DaftarPustakaSettings.CurrentVersion)
                throw new InvalidDataException($"Daftar Pustaka version: {version} is not supported!");

            DaftarPustakaCompression compressionType = inputStream.Read<DaftarPustakaCompression>();
            DaftarPustakaEncryption  encryptionType  = inputStream.Read<DaftarPustakaEncryption>();

            if (context == null && encryptionType != DaftarPustakaEncryption.Unencrypted)
                throw new ArgumentNullException(nameof(context), "Context cannot be null while encryption is set!");

            inputStream = encryptionType switch
            {
                DaftarPustakaEncryption.Aes         => Decryptor.CreateAesStream(inputStream, context!, leaveOpen),
                DaftarPustakaEncryption.Rsa         => Decryptor.CreateRsaStream(inputStream, context!, leaveOpen),
                DaftarPustakaEncryption.Xor         => Decryptor.CreateXorStream(inputStream, context!, leaveOpen),
                DaftarPustakaEncryption.Unencrypted => inputStream,

                _ => throw new NotSupportedException($"Encryption type: {encryptionType} is not implemented!")
            };

            inputStream = compressionType switch
            {
                DaftarPustakaCompression.Brotli       => Decompressor.CreateBrotliStream(inputStream, leaveOpen),
                DaftarPustakaCompression.Zstandard    => Decompressor.CreateZStandardStream(inputStream, leaveOpen),
                DaftarPustakaCompression.Deflate      => Decompressor.CreateDeflateStream(inputStream, leaveOpen),
                DaftarPustakaCompression.Zlib         => Decompressor.CreateZlibStream(inputStream, leaveOpen),
                DaftarPustakaCompression.Lzma         => Decompressor.CreateLzmaStream(inputStream, leaveOpen),
                DaftarPustakaCompression.Lzma2        => Decompressor.CreateLzmaStream(inputStream, leaveOpen),
                DaftarPustakaCompression.BZip2        => Decompressor.CreateBZip2Stream(inputStream, leaveOpen),
                DaftarPustakaCompression.Uncompressed => inputStream,

                _ => throw new NotSupportedException($"Compression type: {compressionType} is not implemented!")
            };

            return inputStream;
        }
    }
}
