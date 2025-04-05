﻿using System;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
// ReSharper disable UnusedMember.Global

#nullable enable
namespace CollapseLauncher.Helper
{
    public static partial class Hash
    {
        #region Non-crypto hash
        /// <summary>
        /// Computes a non-cryptographic hash for the given read-only character span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only character span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static byte[] GetHashFromString<T>(ReadOnlySpan<char> source)
            where T : NonCryptographicHashAlgorithm
        {
            // Cast as bytes
            ReadOnlySpan<byte> spanAsBytes = MemoryMarshal.AsBytes(source);

            // Borrow GetHashFromBytes<T>(ReadOnlySpan<byte> source) to get the hash
            return GetHashFromBytes<T>(spanAsBytes);
        }

        /// <summary>
        /// Computes a non-cryptographic hash for the given read-only character span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only character span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static string GetHashStringFromString<T>(ReadOnlySpan<char> source)
            where T : NonCryptographicHashAlgorithm
        {
            // Cast as bytes
            ReadOnlySpan<byte> spanAsBytes = MemoryMarshal.AsBytes(source);

            // Borrow GetHashStringFromBytes<T>(ReadOnlySpan<byte> source) to get the hash
            return GetHashStringFromBytes<T>(spanAsBytes);
        }

        /// <summary>
        /// Computes a non-cryptographic hash for the given read-only byte span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only byte span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static byte[] GetHashFromBytes<T>(ReadOnlySpan<byte> source)
            where T : NonCryptographicHashAlgorithm
        {
            // Get the shared hash algorithm and the thread lock instance
            ref (NonCryptographicHashAlgorithm? Hash, Lock Lock) hash = ref GetSharedHash<T>();

            // Allocate the return buffer and calculate the hash from the source span
            int    hashLenInBytes  = hash.Hash!.HashLengthInBytes;
            byte[] hashBytesReturn = new byte[hashLenInBytes];
            if (!TryGetHashFromBytes<T>(ref hash!, source, hashBytesReturn, out _))
            {
                throw new InvalidOperationException("Failed to get the hash.");
            }

            // Return the hash bytes
            return hashBytesReturn;
        }

        /// <summary>
        /// Computes a non-cryptographic hash for the given read-only byte span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only byte span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static string GetHashStringFromBytes<T>(ReadOnlySpan<byte> source)
            where T : NonCryptographicHashAlgorithm
        {
            // Get the shared hash algorithm and the thread lock instance
            ref (NonCryptographicHashAlgorithm? Hash, Lock Lock) hash = ref GetSharedHash<T>();

            // Allocate the hash buffer to be written to
            int        hashLenInBytes = hash.Hash!.HashLengthInBytes;
            Span<byte> hashBuffer     = stackalloc byte[hashLenInBytes];
            Span<char> hashCharBuffer = stackalloc char[hashLenInBytes * 2];

            // Compute the hash and reset
            if (!TryGetHashFromBytes<T>(ref hash!, source, hashBuffer, out _))
            {
                throw new InvalidOperationException("Failed to get the hash.");
            }

            // Convert the hash buffer into the characters of hash string
            if (!Convert.TryToHexStringLower(hashBuffer, hashCharBuffer, out _))
            {
                throw new InvalidOperationException("Failed to convert the hash bytes buffer to string.");
            }

            // Create the string from the hash character buffer
            return new string(hashCharBuffer);
        }

        /// <summary>
        /// Computes a non-cryptographic hash for the given read-only byte span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <param name="hashSource">The hash instance source to be used to compute the hash. Use <see cref="GetSharedHash{T}"/> to get the instance.</param>
        /// <param name="source">The read-only byte span to compute the hash for.</param>
        /// <param name="destination">The hash span to be written to.</param>
        /// <param name="hashBytesWritten">The length of how much bytes is the hash written to the <paramref name="destination"/>.</param>
        /// <returns>True if it's successfully calculate the hash, False as failed.</returns>
        public static bool TryGetHashFromBytes<T>(
            ref (NonCryptographicHashAlgorithm Hash, Lock Lock) hashSource,
            ReadOnlySpan<byte>                                  source,
            Span<byte>                                          destination,
            out int                                             hashBytesWritten)
            where T : NonCryptographicHashAlgorithm
        {
            // Lock the thread and append the span to the hash algorithm
            lock (hashSource.Lock)
            {
                // Append and calculate the hash of the span
                hashSource.Hash.Append(source);

                // Return the bool as success or not, then reset the hash while writing the hash bytes to destination span.
                return hashSource.Hash.TryGetHashAndReset(destination, out hashBytesWritten);
            }
        }
        #endregion

        #region Crypto hash
        /// <summary>
        /// Computes a non-cryptographic hash for the given read-only character span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only character span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static byte[] GetCryptoHashFromString<T>(ReadOnlySpan<char> source)
            where T : HashAlgorithm
        {
            // Cast as bytes
            ReadOnlySpan<byte> spanAsBytes = MemoryMarshal.AsBytes(source);

            // Borrow GetCryptoHashFromBytes<T>(ReadOnlySpan<byte> source) to get the hash
            return GetCryptoHashFromBytes<T>(spanAsBytes);
        }

        /// <summary>
        /// Computes a cryptographic hash for the given read-only character span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only character span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static string GetCryptoHashStringFromString<T>(ReadOnlySpan<char> source)
            where T : HashAlgorithm
        {
            // Cast as bytes
            ReadOnlySpan<byte> spanAsBytes = MemoryMarshal.AsBytes(source);

            // Borrow GetCryptoHashStringFromBytes<T>(ReadOnlySpan<byte> source) to get the hash
            return GetCryptoHashStringFromBytes<T>(spanAsBytes);
        }

        /// <summary>
        /// Computes a cryptographic hash for the given read-only byte span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only byte span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static byte[] GetCryptoHashFromBytes<T>(ReadOnlySpan<byte> source)
            where T : HashAlgorithm
        {
            // Get the shared hash algorithm and the thread lock instance
            ref (HashAlgorithm? Hash, Lock Lock) hash = ref GetSharedCryptoHash<T>();

            // Allocate the return buffer and calculate the hash from the source span
            int    hashLenInBytes  = hash.Hash!.HashSize;
            byte[] hashBytesReturn = new byte[hashLenInBytes];
            if (!TryGetCryptoHashFromBytes<T>(ref hash!, source, hashBytesReturn, out _))
            {
                throw new InvalidOperationException("Failed to get the hash.");
            }

            // Return the hash bytes
            return hashBytesReturn;
        }

        /// <summary>
        /// Computes a cryptographic hash for the given read-only byte span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the cryptographic hash algorithm to use.</typeparam>
        /// <param name="source">The read-only byte span to compute the hash for.</param>
        /// <returns>A byte array containing the computed hash.</returns>
        public static string GetCryptoHashStringFromBytes<T>(ReadOnlySpan<byte> source)
            where T : HashAlgorithm
        {
            // Get the shared hash algorithm and the thread lock instance
            ref (HashAlgorithm? Hash, Lock Lock) hash = ref GetSharedCryptoHash<T>();

            // Allocate the hash buffer to be written to
            int        hashLenInBytes = hash.Hash!.HashSize;
            Span<byte> hashBuffer     = stackalloc byte[hashLenInBytes];
            Span<char> hashCharBuffer = stackalloc char[hashLenInBytes * 2];

            // Compute the hash and reset
            if (!TryGetCryptoHashFromBytes<T>(ref hash!, source, hashBuffer, out _))
            {
                throw new InvalidOperationException("Failed to get the hash.");
            }

            // Convert the hash buffer into the characters of hash string
            if (!Convert.TryToHexStringLower(hashBuffer, hashCharBuffer, out _))
            {
                throw new InvalidOperationException("Failed to convert the hash bytes buffer to string.");
            }

            // Create the string from the hash character buffer
            return new string(hashCharBuffer);
        }

        /// <summary>
        /// Computes a cryptographic hash for the given read-only byte span using the specified hash algorithm.
        /// </summary>
        /// <typeparam name="T">The type of the cryptographic hash algorithm to use.</typeparam>
        /// <param name="hashSource">The hash instance source to be used to compute the hash. Use <see cref="GetSharedCryptoHash{T}"/> to get the instance.</param>
        /// <param name="source">The read-only byte span to compute the hash for.</param>
        /// <param name="destination">The hash span to be written to.</param>
        /// <param name="hashBytesWritten">The length of how much bytes is the hash written to the <paramref name="destination"/>.</param>
        /// <returns>True if it's successfully calculate the hash, False as failed.</returns>
        public static bool TryGetCryptoHashFromBytes<T>(
            ref (HashAlgorithm Hash, Lock Lock) hashSource,
            ReadOnlySpan<byte>                  source,
            Span<byte>                          destination,
            out int                             hashBytesWritten)
            where T : HashAlgorithm
        {
            // Lock the thread and compute the hash of the span
            lock (hashSource.Lock)
            {
                // Reset the hash instance state.
                hashSource.Hash.Initialize();

                // Compute the source bytes and return the success state
                return hashSource.Hash.TryComputeHash(source, destination, out hashBytesWritten);
            }
        }
        #endregion
    }
}
