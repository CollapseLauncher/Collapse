using Hi3Helper;
using Hi3Helper.EncTool;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CollapseLauncher.Helper.Metadata
{
    internal enum CompressionType : byte { None, Brotli }
    internal static class DataCooker
    {
        private const long COLLAPSESIG = 7310310183885631299;
        private const int ALLOWEDBUFFERPOOLSIZE = 1 << 20; // 1 MiB

        internal static mhyEncTool EncryptTool = new mhyEncTool();
        internal static RSA RSAInstance = null;

        internal static string ServeV3Data(string data)
        {
            if (!Base64.IsValid(data))
                return data;

            byte[] dataBytes = Convert.FromBase64String(data);
            if (IsServeV3Data(dataBytes))
            {
                GetServeV3DataSize(dataBytes, out long compressedSize, out long decompressedSize);
                byte[] outBuffer = new byte[decompressedSize];
                ServeV3Data(dataBytes, outBuffer, (int)compressedSize, (int)decompressedSize, out int dataWritten);
                return Encoding.UTF8.GetString(outBuffer.AsSpan(0, dataWritten));
            }

            return data;
        }

        internal static bool IsServeV3Data(ReadOnlySpan<byte> data)
        {
            if (data.Length < 8) return false;

            long signature = MemoryMarshal.Read<long>(data);
            if (signature != COLLAPSESIG) return false;

            return true;
        }

        internal static void GetServeV3DataSize(ReadOnlySpan<byte> data, out long compressedSize, out long decompressedSize)
        {
            if (data.Length < 32) throw new FormatException($"The MetadataV3 data format is corrupted!");

            int readOffset = sizeof(long) * 2;
            compressedSize = MemoryMarshal.Read<long>(data.Slice(readOffset));
            decompressedSize = MemoryMarshal.Read<long>(data.Slice(readOffset += sizeof(long)));
        }

        private static void GetServeV3Attribute(ReadOnlySpan<byte> data, out CompressionType compressionType, out bool isUseEncryption)
        {
            int readOffset = sizeof(long);
            long attribNumber = MemoryMarshal.Read<long>(data.Slice(readOffset));

            compressionType = (CompressionType)(byte)attribNumber;
            isUseEncryption = (byte)(attribNumber >> 8) == 1;
        }


        internal static void ServeV3Data(ReadOnlySpan<byte> data, Span<byte> outData, int compressedSize, int decompressedSize, out int dataWritten)
        {
            GetServeV3Attribute(data, out CompressionType compressionType, out bool isUseEncryption);
            int readOffset = sizeof(long) * 4;

            ReadOnlySpan<byte> dataRawBuffer = data.Slice(readOffset, data.Length - readOffset);
            byte[] decryptedDataSpan = null;
            int encBitLength = LauncherMetadataHelper.CurrentMasterKey.BitSize;

            bool isDecryptPoolAllowed = dataRawBuffer.Length <= ALLOWEDBUFFERPOOLSIZE;
            bool isDecryptPoolUsed = false;

            try
            {
                if (isUseEncryption)
                {
                    decryptedDataSpan = isDecryptPoolAllowed ? ArrayPool<byte>.Shared.Rent(dataRawBuffer.Length) : new byte[dataRawBuffer.Length];
                    isDecryptPoolUsed = isDecryptPoolAllowed;

                    if (RSAInstance == null)
                    {
                        RSAInstance = RSA.Create();
                        byte[] key;
                        if (IsServeV3Data(LauncherMetadataHelper.CurrentMasterKey.Key))
                        {
                            GetServeV3DataSize(LauncherMetadataHelper.CurrentMasterKey.Key, out long keyCompSize, out long keyDecompSize);
                            key = new byte[keyCompSize];
                            ServeV3Data(LauncherMetadataHelper.CurrentMasterKey.Key, key, (int)keyCompSize, (int)keyDecompSize, out _);
                        }
                        else
                        {
                            key = LauncherMetadataHelper.CurrentMasterKey.Key;
                        }

                        RSAInstance.ImportRSAPrivateKey(key, out _);
                    }

                    int offset = 0;
                    int offsetOut = 0;
                    while (offset < dataRawBuffer.Length)
                    {
                        int decryptWritten = RSAInstance.Decrypt(dataRawBuffer.Slice(offset, encBitLength), decryptedDataSpan.AsSpan(offsetOut), RSAEncryptionPadding.Pkcs1);
                        offsetOut += decryptWritten;
                        offset += encBitLength;
                    }

                    dataRawBuffer = decryptedDataSpan.AsSpan(0, offsetOut);
                }

                if (dataRawBuffer.Length != compressedSize)
                    throw new DataMisalignedException($"RAW data is misaligned!");

                switch (compressionType)
                {
                    case CompressionType.None:
                        if (!isUseEncryption) data.Slice(readOffset, decompressedSize).CopyTo(outData);
                        dataWritten = decompressedSize;
                        break;
                    case CompressionType.Brotli:
                        {
                            Span<byte> dataDecompressed = outData;
                            BrotliDecoder decoder = new BrotliDecoder();

                            int offset = 0;
                            int decompressedWritten = 0;
                            while (offset < compressedSize)
                            {
                                decoder.Decompress(dataRawBuffer.Slice(offset), dataDecompressed.Slice(decompressedWritten), out int dataConsumedWritten, out int dataDecodedWritten);
                                decompressedWritten += dataDecodedWritten;
                                offset += dataConsumedWritten;
                            }
                            if (decompressedSize != decompressedWritten)
                                throw new DataMisalignedException($"Decompressed data is misaligned!");

                            dataWritten = decompressedWritten;
                        }
                        break;
                    default:
                        throw new FormatException($"Decompression format is not supported! ({compressionType})");
                }

#if DEBUG
                Logger.LogWriteLine($"[DataCooker::ServeV3Data()] Loaded ServeV3 data [IsPooled: {isDecryptPoolUsed}][TCompress: {compressionType} | IsEncrypt: {isUseEncryption}][CompSize: {compressedSize} | UncompSize: {decompressedSize}]", LogType.Debug, true);
#endif
            }
            finally
            {
                if (isDecryptPoolAllowed && isDecryptPoolUsed && decryptedDataSpan != null)
                    ArrayPool<byte>.Shared.Return(decryptedDataSpan, true);
            }
        }
    }
}
