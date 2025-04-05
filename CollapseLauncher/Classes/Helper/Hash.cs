﻿using Hi3Helper;
using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

#nullable enable
namespace CollapseLauncher.Helper
{
    public static partial class Hash
    {
        private const int BufferLength = 512 << 10;
        private static readonly Dictionary<int, Func<HashAlgorithm>> CryptoHashDict = new()
        {
            { typeof(MD5).GetHashCode(), MD5.Create },
            { typeof(SHA1).GetHashCode(), SHA1.Create },
            { typeof(SHA256).GetHashCode(), SHA256.Create },
            { typeof(SHA384).GetHashCode(), SHA384.Create },
            { typeof(SHA512).GetHashCode(), SHA512.Create },
            { typeof(SHA3_256).GetHashCode(), SHA3_256.Create },
            { typeof(SHA3_384).GetHashCode(), SHA3_384.Create },
            { typeof(SHA3_512).GetHashCode(), SHA3_512.Create }
        };
        private static readonly Dictionary<int, Func<byte[], HashAlgorithm>> CryptoHmacHashDict = new()
        {
            { typeof(HMACSHA1).GetHashCode(), key => new HMACSHA1(key) },
            { typeof(HMACSHA256).GetHashCode(), key => new HMACSHA256(key) },
            { typeof(HMACSHA384).GetHashCode(), key => new HMACSHA384(key) },
            { typeof(HMACSHA512).GetHashCode(), key => new HMACSHA512(key) },
            { typeof(HMACSHA3_256).GetHashCode(), key => new HMACSHA3_256(key) },
            { typeof(HMACSHA3_384).GetHashCode(), key => new HMACSHA3_384(key) },
            { typeof(HMACSHA3_512).GetHashCode(), key => new HMACSHA3_512(key) }
        };

        private static readonly Dictionary<int, (HashAlgorithm?, Lock)> CryptoHashDictShared = new()
        {
            { typeof(MD5).GetHashCode(), (CreateHashAndNullIfUnsupported(MD5.Create), new Lock()) },
            { typeof(SHA1).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA1.Create), new Lock()) },
            { typeof(SHA256).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA256.Create), new Lock()) },
            { typeof(SHA384).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA384.Create), new Lock()) },
            { typeof(SHA512).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA512.Create), new Lock()) },
            { typeof(SHA3_256).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA3_256.Create), new Lock()) },
            { typeof(SHA3_384).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA3_384.Create), new Lock()) },
            { typeof(SHA3_512).GetHashCode(), (CreateHashAndNullIfUnsupported(SHA3_512.Create), new Lock()) }
        };

        private static readonly Dictionary<int, (NonCryptographicHashAlgorithm?, Lock)> HashDictShared = new()
        {
            { typeof(Crc32).GetHashCode(), (CreateHashAndNullIfUnsupported(() => new Crc32()), new Lock()) },
            { typeof(Crc64).GetHashCode(), (CreateHashAndNullIfUnsupported(() => new Crc64()), new Lock()) },
            { typeof(XxHash3).GetHashCode(), (CreateHashAndNullIfUnsupported(() => new XxHash3()), new Lock()) },
            { typeof(XxHash32).GetHashCode(), (CreateHashAndNullIfUnsupported(() => new XxHash32()), new Lock()) },
            { typeof(XxHash64).GetHashCode(), (CreateHashAndNullIfUnsupported(() => new XxHash64()), new Lock()) },
            { typeof(XxHash128).GetHashCode(), (CreateHashAndNullIfUnsupported(() => new XxHash128()), new Lock()) }
        };

        private static T? CreateHashAndNullIfUnsupported<T>(Func<T> delegateCreate)
        {
            try
            {
                return delegateCreate();
            }
            catch (PlatformNotSupportedException)
            {
                Logger.LogWriteLine($"Cannot initialize hash type: {typeof(T).Name} as it is not supported on your platform!", LogType.Warning, true);
                return default;
            }
        }

        /// <summary>
        /// Creates an instance of the specified cryptographic hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to create. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <returns>An instance of the specified hash algorithm.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified hash algorithm type is not supported.</exception>
        public static HashAlgorithm CreateCryptoHash<T>()
            where T : HashAlgorithm
        {
            // Try to get the hash create method from the dictionary
            ref Func<HashAlgorithm> createHashDelegate = ref CollectionsMarshal
                .GetValueRefOrNullRef(CryptoHashDict, typeof(T).GetHashCode());

            // Create the hash algorithm instance
            return createHashDelegate();
        }

        /// <summary>
        /// Creates an instance of the specified HMAC-based cryptographic hash algorithm using the provided key.
        /// </summary>
        /// <typeparam name="T">The type of the HMAC-based hash algorithm to create. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="key">The key to use for the HMAC-based hash algorithm.</param>
        /// <returns>An instance of the specified HMAC-based hash algorithm.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified HMAC-based hash algorithm type is not supported.</exception>
        public static HashAlgorithm CreateHmacCryptoHash<T>(byte[] key)
            where T : HashAlgorithm
        {
            // Try to get the hash create method from the dictionary
            ref Func<byte[], HashAlgorithm> createHashDelegate = ref CollectionsMarshal
                .GetValueRefOrNullRef(CryptoHmacHashDict, typeof(T).GetHashCode());

            // Create the hash algorithm instance
            return createHashDelegate(key);
        }

        /// <summary>
        /// Creates an instance of the specified non-cryptographic hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to create. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <returns>An instance of the specified non-cryptographic hash algorithm.</returns>
        public static NonCryptographicHashAlgorithm CreateHash<T>()
            where T : NonCryptographicHashAlgorithm, new() => new T();

        /// <summary>
        /// Gets the shared hash algorithm instance of the specified non-cryptographic hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <returns>A tuple of the non-cryptographic hash algorithm and the thread <see cref="Lock"/> instance.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified non-cryptographic hash algorithm type is not supported.</exception>
        public static ref (NonCryptographicHashAlgorithm? Hash, Lock Lock) GetSharedHash<T>()
            where T : NonCryptographicHashAlgorithm
        {
            // Get reference from the dictionary
            ref (NonCryptographicHashAlgorithm? Hash, Lock Lock) hash = ref CollectionsMarshal
               .GetValueRefOrNullRef(HashDictShared, typeof(T).GetHashCode());

            // Return the tuple reference
            return ref hash;
        }

        /// <summary>
        /// Gets the shared hash algorithm instance of the specified cryptographic hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the cryptographic hash algorithm to use.</typeparam>
        /// <returns>A tuple of the cryptographic hash algorithm and the thread <see cref="Lock"/> instance.</returns>
        /// <exception cref="NotSupportedException">Thrown when the specified cryptographic hash algorithm type is not supported.</exception>
        public static ref (HashAlgorithm? Hash, Lock Lock) GetSharedCryptoHash<T>()
            where T : HashAlgorithm
        {
            // Get reference from the dictionary
            ref (HashAlgorithm? Hash, Lock Lock) hash = ref CollectionsMarshal
               .GetValueRefOrNullRef(CryptoHashDictShared, typeof(T).GetHashCode());

            // Return the tuple reference
            return ref hash;
        }
    }
}
