using System;
using System.Buffers;
using System.IO.Hashing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace
// ReSharper disable UnusedMember.Global

#nullable enable
namespace CollapseLauncher.Helper
{
    public static partial class Hash
    {
        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            string            filePath,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm =>
            GetCryptoHashAsync<T>(() => File.OpenRead(filePath), hmacKey, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            string            filePath,
            byte[]?           hmacKey,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : HashAlgorithm =>
            GetCryptoHashAsync<T>(() => File.OpenRead(filePath), hmacKey, readProgress, isLongRunning, token);

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm =>
            GetCryptoHashAsync<T>(fileInfo.OpenRead, hmacKey, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : HashAlgorithm =>
            GetCryptoHashAsync<T>(fileInfo.OpenRead, hmacKey, readProgress, isLongRunning, token);

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Stream            stream,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm =>
            GetCryptoHashAsync<T>(stream, hmacKey, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Stream            stream,
            byte[]?           hmacKey,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : HashAlgorithm
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(Impl,
                                         (stream, hmacKey, readProgress, token),
                                         token,
                                         isLongRunning ? TaskCreationOptions.LongRunning : TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);

            static byte[] Impl(object? state)
            {
                (Stream stream, byte[]? hmacKey, Action<int>? readProgress, CancellationToken token) = ((Stream, byte[]?, Action<int>?, CancellationToken))state!;
                return GetCryptoHash<T>(stream, hmacKey, readProgress, token);
            }
        }

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="streamDelegate">A delegate function which returns the stream to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Func<Stream>      streamDelegate,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm =>
            GetCryptoHashAsync<T>(streamDelegate, hmacKey, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="streamDelegate">A delegate function which returns the stream to compute the hash for.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Func<Stream>      streamDelegate,
            byte[]?           hmacKey,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : HashAlgorithm
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(Impl,
                                         (streamDelegate, hmacKey, readProgress, token),
                                         token,
                                         isLongRunning ? TaskCreationOptions.LongRunning : TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);

            static byte[] Impl(object? state)
            {
                (Func<Stream> streamDelegate, byte[]? hmacKey, Action<int>? readProgress, CancellationToken token) = ((Func<Stream>, byte[]?, Action<int>?, CancellationToken))state!;
                using Stream stream = streamDelegate();
                return GetCryptoHash<T>(stream, hmacKey, readProgress, token);
            }
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
        public static byte[] GetCryptoHash<T>(
            string            filePath,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm =>
            GetCryptoHash<T>(() => File.OpenRead(filePath), hmacKey, readProgress, token);

        /// <summary>
        /// Synchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetCryptoHash<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm =>
            GetCryptoHash<T>(fileInfo.OpenRead, hmacKey, readProgress, token);

        /// <summary>
        /// Synchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="streamDelegate">A delegate function which returns the stream to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetCryptoHash<T>(
            Func<Stream>      streamDelegate,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using Stream stream = streamDelegate();
            return GetCryptoHash<T>(stream, hmacKey, readProgress, token);
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
        public static byte[] GetCryptoHash<T>(
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
            long streamLen = GetStreamLength(stream);
            int  bufferLen = streamLen != -1 && BufferLength > streamLen ? (int)streamLen : BufferLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.ReadAtLeast(buffer, bufferLen, false)) > 0)
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
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            string            filePath,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            GetHashAsync<T>(() => File.OpenRead(filePath), readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            string            filePath,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : NonCryptographicHashAlgorithm, new() =>
            GetHashAsync<T>(() => File.OpenRead(filePath), readProgress, isLongRunning, token);

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            GetHashAsync<T>(fileInfo.OpenRead, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : NonCryptographicHashAlgorithm, new() =>
            GetHashAsync<T>(fileInfo.OpenRead, readProgress, isLongRunning, token);

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            Stream            stream,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            GetHashAsync<T>(stream, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            Stream            stream,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create hasher instance
            NonCryptographicHashAlgorithm hashProvider = CreateHash<T>();

            // Calculate hash from the stream
            return GetHashAsync(stream, hashProvider, readProgress, isLongRunning, token);
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream using a specifically provided <see cref="NonCryptographicHashAlgorithm"/> instance.
        /// </summary>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="hashProvider">A specifically <see cref="NonCryptographicHashAlgorithm"/> instance to use.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync(
            Stream                        stream,
            NonCryptographicHashAlgorithm hashProvider,
            Action<int>?                  readProgress,
            bool                          isLongRunning,
            CancellationToken             token)
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(Impl,
                                         (stream, hashProvider, readProgress, token),
                                         token,
                                         isLongRunning ? TaskCreationOptions.LongRunning : TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);

            static byte[] Impl(object? state)
            {
                (Stream stream, NonCryptographicHashAlgorithm hashProvider, Action<int>? readProgress, CancellationToken token) =
                    ((Stream, NonCryptographicHashAlgorithm, Action<int>?, CancellationToken))state!;
                return GetHash(stream, hashProvider, readProgress, token);
            }
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="streamDelegate">A delegate function which returns the stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            Func<Stream>      streamDelegate,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new() =>
            GetHashAsync<T>(streamDelegate, readProgress, false, token);

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="streamDelegate">A delegate function which returns the stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            Func<Stream>      streamDelegate,
            Action<int>?      readProgress,
            bool              isLongRunning,
            CancellationToken token)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create hasher instance
            NonCryptographicHashAlgorithm hashProvider = CreateHash<T>();

            // Calculate hash from the stream
            return GetHashAsync(streamDelegate, hashProvider, readProgress, isLongRunning, token);
        }


        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream using a specifically provided <see cref="NonCryptographicHashAlgorithm"/> instance.
        /// </summary>
        /// <param name="streamDelegate">A delegate function which returns the stream to compute the hash for.</param>
        /// <param name="hashProvider">A specifically <see cref="NonCryptographicHashAlgorithm"/> instance to use.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="isLongRunning">
        /// Define where the async method should run for hashing big files.<br/>
        /// This to hint the default TaskScheduler to allow more hashing threads to be running at the same time.<br/>
        /// Set to <c>true</c> if the data stream is big, otherwise <c>false</c> for small data stream.
        /// </param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync(
            Func<Stream>                  streamDelegate,
            NonCryptographicHashAlgorithm hashProvider,
            Action<int>?                  readProgress,
            bool                          isLongRunning,
            CancellationToken             token)
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(Impl,
                                         (streamDelegate, hashProvider, readProgress, token),
                                         token,
                                         isLongRunning ? TaskCreationOptions.LongRunning : TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);

            static byte[] Impl(object? state)
            {
                (Func<Stream> streamDelegate, NonCryptographicHashAlgorithm hashProvider, Action<int>? readProgress, CancellationToken token) =
                    ((Func<Stream>, NonCryptographicHashAlgorithm, Action<int>?, CancellationToken))state!;
                using Stream stream = streamDelegate();
                return GetHash(stream, hashProvider, readProgress, token);
            }
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash<T>(
            string            filePath,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return GetHash<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return GetHash<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash<T>(
            Stream            stream,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create hasher instance
            NonCryptographicHashAlgorithm hashProvider = CreateHash<T>();

            // Calculate hash from the stream
            return GetHash(stream, hashProvider, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a stream using a specifically provided <see cref="NonCryptographicHashAlgorithm"/> instance.
        /// </summary>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="hashProvider">A specifically <see cref="NonCryptographicHashAlgorithm"/> instance to use.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash(
            Stream                        stream,
            NonCryptographicHashAlgorithm hashProvider,
            Action<int>?                  readProgress = null,
            CancellationToken             token        = default)
        {
            // Get length based on stream length or at least if bigger, use the default one
            long streamLen = GetStreamLength(stream);
            int bufferLen = streamLen != -1 && BufferLength > streamLen ? (int)streamLen : BufferLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.ReadAtLeast(buffer, bufferLen, false)) > 0)
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

        /// <summary>
        /// Try to get the length of the stream. 
        /// </summary>
        /// <param name="stream">The stream to get the length to.</param>
        /// <returns>If it doesn't have exact length (which will throw), return -1. Otherwise, return the actual length.</returns>
        private static long GetStreamLength(Stream stream)
        {
            try
            {
                return stream.Length;
            }
            catch
            {
                return -1;
            }
        }
    }
}
