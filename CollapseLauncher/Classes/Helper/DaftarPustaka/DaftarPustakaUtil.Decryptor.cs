using CollapseLauncher.Helper.StreamUtility;
using System;
using System.IO;
using System.Security.Cryptography;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Helper.DaftarPustaka;

internal static partial class DaftarPustakaUtil
{
    public static class Decryptor
    {
        /// <summary>
        /// Creates an AES Decryption Stream.<br/><br/>
        ///
        /// Field Structure:<br/>
        /// <code>
        /// +------------------+-------------------+---------------+--------------+<br/>
        /// | (I4) Cipher Mode | (I4) Padding Mode | (U1) Key Size | (U1) IV Size |<br/>
        /// +------------------+-------------------+---------------+--------------+<br/>
        /// |                         Encrypted Data Blob                         |<br/>
        /// +---------------------------------------------------------------------+
        /// </code>
        /// </summary>
        /// <param name="inputStream">The source <see cref="Stream"/> to decrypt.</param>
        /// <param name="context">The context of the file, providing the information about the file and the salt for decryption.</param>
        /// <param name="leaveOpen">Whether to dispose or keeping it open after disposing the decompression and decryption stream.</param>
        /// <returns>An instance of the AES Decryption <see cref="CryptoStream"/>.</returns>
        public static CryptoStream CreateAesStream(
            Stream             inputStream,
            ServeV3FileContext context,
            bool               leaveOpen = false)
        {
            CipherMode  cipherMode  = inputStream.Read<CipherMode>();
            PaddingMode paddingMode = inputStream.Read<PaddingMode>();
            byte        keySize     = inputStream.Read<byte>();
            byte        ivSize      = inputStream.Read<byte>();

            context.GetKeyAndIvFromLength(keySize,
                                          ivSize,
                                          out byte[] key,
                                          out byte[] iv);

            Aes aesInstance = Aes.Create();
            aesInstance.Mode    = cipherMode;
            aesInstance.Padding = paddingMode;
            aesInstance.Key     = key;

            if (cipherMode != CipherMode.ECB)
                aesInstance.IV = iv;

            return new CryptoStream(inputStream, aesInstance.CreateDecryptor(), CryptoStreamMode.Read, leaveOpen);
        }

        public static CryptoStream CreateRsaStream(
            Stream             inputStream,
            ServeV3FileContext context,
            bool               leaveOpen = false)
        {
            throw new NotImplementedException();
        }

        public static CryptoStream CreateXorStream(
            Stream             inputStream,
            ServeV3FileContext context,
            bool               leaveOpen = false)
        {
            throw new NotImplementedException();
        }
    }
}
