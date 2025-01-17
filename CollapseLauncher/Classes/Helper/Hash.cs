using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Hashing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper
{
    public static class Hash
    {
        private const int BufferLength = 512 << 10;
        private static readonly Dictionary<Type, Func<HashAlgorithm>> CryptoHashDict = new()
        {
            { typeof(MD5), MD5.Create },
            { typeof(SHA1), SHA1.Create },
            { typeof(SHA256), SHA256.Create },
            { typeof(SHA384), SHA384.Create },
            { typeof(SHA512), SHA512.Create },
            { typeof(SHA3_256), SHA3_256.Create },
            { typeof(SHA3_384), SHA3_384.Create },
            { typeof(SHA3_512), SHA3_512.Create }
        };
        private static readonly Dictionary<Type, Func<byte[], HashAlgorithm>> CryptoHmacHashDict = new()
        {
            { typeof(HMACSHA1), key => new HMACSHA1(key) },
            { typeof(HMACSHA256), key => new HMACSHA256(key) },
            { typeof(HMACSHA384), key => new HMACSHA384(key) },
            { typeof(HMACSHA512), key => new HMACSHA512(key) },
            { typeof(HMACSHA3_256), key => new HMACSHA3_256(key) },
            { typeof(HMACSHA3_384), key => new HMACSHA3_384(key) },
            { typeof(HMACSHA3_512), key => new HMACSHA3_512(key) }
        };

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
                .GetValueRefOrNullRef(CryptoHashDict, typeof(T));

            // If the delegate is null, then throw an exception
            if (createHashDelegate == null)
            {
                throw new NotSupportedException($"Cannot create hash algorithm instance from {typeof(T)}.");
            }

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
                .GetValueRefOrNullRef(CryptoHmacHashDict, typeof(T));

            // If the delegate is null, then throw an exception
            if (createHashDelegate == null)
            {
                throw new NotSupportedException($"Cannot create HMAC-based hash algorithm instance from {typeof(T)}.");
            }

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
        /// Asynchronously computes the cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> CheckCryptoHashAsync<T>(
            string            filePath,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return CheckCryptoHashAsync<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> CheckCryptoHashAsync<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return CheckCryptoHashAsync<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> CheckCryptoHashAsync<T>(
            Stream            stream,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(() => CheckCryptoHash<T>(stream, hmacKey, readProgress, token),
                                         token,
                                         TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously computes the cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] CheckCryptoHash<T>(
            string            filePath,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return CheckCryptoHash<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] CheckCryptoHash<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return CheckCryptoHash<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] CheckCryptoHash<T>(
            Stream            stream,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            // Create and use hasher. If hmacKey is not null and not empty, then create HMAC hasher
            using HashAlgorithm hashCryptoTransform = hmacKey != null && hmacKey.Length != 0 ?
                CreateHmacCryptoHash<T>(hmacKey) :
                CreateCryptoHash<T>();

            // Get length based on stream length or at least if bigger, use the default one
            int bufferLen = BufferLength > stream.Length ? (int)stream.Length : BufferLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.Read(buffer, 0, bufferLen)) > 0)
                {
                    // Throw Cancellation exception if detected
                    token.ThrowIfCancellationRequested();

                    // Append buffer into hash block
                    hashCryptoTransform.TransformBlock(buffer, 0, read, buffer, 0);

                    // Update the read progress if the action is not null
                    readProgress?.Invoke(read);
                }

                // Finalize the hash calculation
                hashCryptoTransform.TransformFinalBlock(buffer, 0, read);

                // Return computed hash byte
                return hashCryptoTransform.Hash ?? [];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> CheckHashAsync<T>(
            string            filePath,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return CheckHashAsync<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> CheckHashAsync<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return CheckHashAsync<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> CheckHashAsync<T>(
            Stream            stream,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(() => CheckHash<T>(stream, readProgress, token),
                                         token,
                                         TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] CheckHash<T>(
            string            filePath,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return CheckHash<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] CheckHash<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return CheckHash<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] CheckHash<T>(
            Stream            stream,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create hasher instance
            NonCryptographicHashAlgorithm hashProvider = CreateHash<T>();

            // Get length based on stream length or at least if bigger, use the default one
            int bufferLen = BufferLength > stream.Length ? (int)stream.Length : BufferLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.Read(buffer, 0, bufferLen)) > 0)
                {
                    // Throw Cancellation exception if detected
                    token.ThrowIfCancellationRequested();

                    // Append buffer into hash block
                    hashProvider.Append(buffer.AsSpan(0, read));

                    // Update the read progress if the action is not null
                    readProgress?.Invoke(read);
                }

                // Return computed hash byte
                return hashProvider.GetHashAndReset();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
