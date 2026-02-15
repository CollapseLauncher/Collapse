using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Plugins;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.SentryHelper;
using PhotoSauce.MagicScaler;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Helper.Image.Waifu2X;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable CommentTypo
// ReSharper disable MemberCanBePrivate.Global

namespace CollapseLauncher.Helper.Image
{
    internal static class ImageLoaderHelper
    {
        internal static readonly string SupportedImageFormats =
            string.Join(";", LayeredBackgroundImage.SupportedImageBitmapExtensions.Select(x => $"*{x}")) + ";" +
            string.Join(";", LayeredBackgroundImage.SupportedImageBitmapExternalCodecExtensions.Select(x => $"*{x}")) + ";" +
            string.Join(";", LayeredBackgroundImage.SupportedImageVectorExtensions.Select(x => $"*{x}"));

        internal static readonly string SupportedVideoFormats =
            string.Join(";", LayeredBackgroundImage.SupportedVideoExtensions.Select(x => $"*{x}"));

        internal static readonly Dictionary<string, string> SupportedBackgroundFormats =
            new() {
                { "All supported formats", SupportedImageFormats + ';' + SupportedVideoFormats },
                { "Image formats", SupportedImageFormats },
                { "Video formats", SupportedVideoFormats }
            };

        #region Waifu2X
        public static Waifu2X _waifu2X;
        private static Waifu2XStatus _cachedStatus = Waifu2XStatus.NotInitialized;

        public static Waifu2XStatus Waifu2XStatus => _cachedStatus;

        public static bool IsWaifu2XEnabled
        {
            get => GetAppConfigValue("EnableWaifu2X").ToBool() && IsWaifu2XUsable && _waifu2X != null;
            set
            {
                SetAndSaveConfigValue("EnableWaifu2X", value);
                if (value) InitWaifu2X();
                else DestroyWaifu2X();
            }
        }

        public static bool IsWaifu2XUsable => Waifu2XStatus < Waifu2XStatus.Error;

        public static bool EnsureWaifu2X()
        {
            if (_cachedStatus != Waifu2XStatus.NotInitialized)
                return false;
            _cachedStatus = VulkanTest();
            return true;
        }

        private static Waifu2X CreateWaifu2X()
        {
            var waifu2X = new Waifu2X();
            if (waifu2X.Status >= Waifu2XStatus.Error)
            {
                return waifu2X;
            }

            waifu2X.SetParam(Param.Noise, -1);
            waifu2X.SetParam(Param.Scale, 2);
            waifu2X.Load(Path.Combine(AppExecutableDir, @"Assets\Waifu2X_Models\scale2.0x_model.param.bin"),
                         Path.Combine(AppExecutableDir, @"Assets\Waifu2X_Models\scale2.0x_model.bin"));
            _cachedStatus = waifu2X.Status;
            return waifu2X;
        }

        public static void InitWaifu2X()
        {
            _waifu2X ??= CreateWaifu2X();
        }

        public static void DestroyWaifu2X()
        {
            _waifu2X?.Dispose();
            _waifu2X = null;
        }
        #endregion


#nullable enable
        private static volatile ProcessImageSettings? _pngImageSettings;

        public static Task GetConvertedImageAsPng(Stream input,
                                                  Stream output,
                                                  double dpiX = 96,
                                                  double dpiY = 96)
        {
            TaskCompletionSource tcs = new();
            Task.Factory.StartNew(Impl);

            return tcs.Task;

            void Impl()
            {
                try
                {
                    if (_pngImageSettings == null)
                    {
                        ProcessImageSettings settings = new()
                        {
                            DpiX = dpiX,
                            DpiY = dpiY
                        };
                        settings.TrySetEncoderFormat(ImageMimeTypes.Png);
                        _pngImageSettings = settings;
                    }

                    using ProcessingPipeline pipeline =
                        MagicImageProcessor.BuildPipeline(input, _pngImageSettings);

                    pipeline.WriteOutput(output);
                    tcs.SetResult();
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }
        }
#nullable restore

        /// <summary>
        /// Check if background image is downloaded
        /// </summary>
        /// <param name="fileInfo">FileInfo of the image to store</param>
        /// <param name="checkIsHashable">Is it hashed?</param>
        /// <returns>true if downloaded, false if not</returns>
        public static Task<bool> IsFileCompletelyDownloadedAsync(FileInfo fileInfo, bool checkIsHashable)
        {
            // Check if the file exist
            return Task<bool>.Factory.StartNew(
                () => IsFileCompletelyDownloaded(fileInfo, checkIsHashable),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Check if background image is downloaded
        /// </summary>
        /// <param name="fileInfo">FileInfo of the image to store</param>
        /// <param name="checkIsHashable">Is it hashed?</param>
        /// <returns>true if downloaded, false if not</returns>
        public static bool IsFileCompletelyDownloaded(FileInfo fileInfo, bool checkIsHashable)
        {
            // Get the parent path and file name
            string outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
            string outputFileName = Path.GetFileName(fileInfo.FullName);

#nullable enable
            // Try to get a hash from filename if the checkIsHashable set to true and the file does exist
            if (checkIsHashable && fileInfo.Exists && TryGetMd5HashFromFilename(outputFileName, out byte[]? hashFromFilename))
            {
                // Open the file and check for the hash
                using FileStream fileStream = fileInfo.OpenRead();
                ReadOnlySpan<byte> hashByte = MD5.HashData(fileStream);

                // Check if the hash matches then return the completeness
                bool isMatch = hashByte.SequenceEqual(hashFromFilename);
                return isMatch;
            }
#nullable restore

            // Try to get the prop file which includes the filename + the suggested size provided
            // by the network stream if it has been downloaded before
            if (outputParentPath == null)
            {
                return false;
            }

            string propFilePath = Directory.EnumerateFiles(outputParentPath, $"{outputFileName}#*", SearchOption.TopDirectoryOnly).FirstOrDefault();
            // Check if the file is found (not null), then try parse the information
            if (string.IsNullOrEmpty(propFilePath))
            {
                return false;
            }

            // Try split the filename into a segment by # char
            string[] propSegment = Path.GetFileName(propFilePath).Split('#');
            // Assign the check if the condition met and set the file existence status
            return propSegment.Length >= 2
                   && long.TryParse(propSegment[1], null, out long suggestedSize)
                   && fileInfo.Exists && fileInfo.Length == suggestedSize;

            // If the prop doesn't exist, then return false to assume that the file doesn't exist
        }

#nullable enable
        private static bool TryGetMd5HashFromFilename(string fileName, out byte[]? hash)
        {
            // Set default value for out
            hash = null;

            // If the filename is null, then return false
            if (string.IsNullOrEmpty(fileName))
                return false;

            // Assign range and try to get the split
            Span<Range> range = stackalloc Range[4];
            ReadOnlySpan<char> fileNameSpan = fileName.AsSpan();
            int len = fileNameSpan.Split(range, '_', StringSplitOptions.RemoveEmptyEntries);

            // As per format should be "hash_number.ext", check that the range should have
            // expected to return 2. If not, then return false as non hashable.
            if (len != 2)
                return false;

            // Try to get the span of the hash
            ReadOnlySpan<char> hashSpan = fileNameSpan[range[0]];

            // If the hashSpan is empty or the length is not even, or it's not a MD5 Hex (32 chars), then return false
            if (hashSpan.IsEmpty || hashSpan.Length % 2 != 0 || hashSpan.Length != 32)
                return false;

            // Try to decode hash hex to find out if the string is actually a hex
            Span<byte> dummy = stackalloc byte[16];
            if (!HexTool.TryHexToBytesUnsafe(hashSpan, dummy))
                return false;

            // Copy hash from stackalloc to output array
            hash = new byte[16];
            dummy.CopyTo(hash);

            // Return true as it's a valid MD5 hash
            return true;
        }

        private static readonly HashSet<string> ProcessingUrls = new(StringComparer.OrdinalIgnoreCase);

        public static async Task<bool> TryDownloadToCompletenessAsync(string? url, HttpClient? useHttpClient, FileInfo fileInfo, bool isSkipCheck, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            fileInfo.EnsureCreationOfDirectory().EnsureNoReadOnly();

            if (ProcessingUrls.Contains(url))
            {
                Logger.LogWriteLine("Found duplicate download request, skipping...\r\n\t" +
                                    $"URL : {url}", LogType.Warning, true);
                return false;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                ProcessingUrls.Add(url);
                // Initialize file temporary name
                FileInfo fileInfoTemp = new FileInfo(fileInfo.FullName + "_temp")
                    .EnsureCreationOfDirectory()
                    .EnsureNoReadOnly();

                Logger.LogWriteLine($"Start downloading resource from: {url}", LogType.Default, true);

                if (fileInfo.Exists)
                    fileInfo.Delete();

                int writeAttempt = 5;

                while (writeAttempt > 0)
                {
                    // Try to get the remote stream and download the file
                    long fileLength;
                    await using (Stream netStream = await GetFallbackStreamUrl(useHttpClient, url, token))
                    {
                        await using (FileStream outStream = new FileStream(fileInfoTemp.FullName, FileMode.Create,
                                                                           FileAccess.ReadWrite, FileShare.ReadWrite))
                        {
                            // Get the file length
                            fileLength = netStream.Length;

                            // Create the prop file for download completeness checking
                            string? outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
                            string  outputFilename   = Path.GetFileName(fileInfo.FullName);
                            if (outputParentPath != null)
                            {
                                string propFilePath = Path.Combine(outputParentPath, $"{outputFilename}#{netStream.Length}");
                                await using (FileStream _ = new FileStream(propFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Delete))
                                {
                                    // Just create the file
                                }
                            }

                            // Copy (and download) the remote streams to local
                            int read;
                            while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                                await outStream.WriteAsync(buffer.AsMemory(0, read), token);
                        }
                    }

                    Logger.LogWriteLine($"Resource download from: {url} has been completed and stored locally into:"
                                        + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)", LogType.Default, true);

                    // Move to its original filename
                    fileInfoTemp.Refresh();
                    fileInfoTemp.MoveTo(fileInfo.FullName, true);
                    fileInfo.Refresh();

                    if (isSkipCheck || await IsFileCompletelyDownloadedAsync(fileInfo, true))
                    {
                        // Break from the loop and return true
                        return true;
                    }

                    Logger.LogWriteLine($"Failed to download resource from: {url} while trying to store into:"
                        + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)"
                        + $". Remained attempt: {writeAttempt}", LogType.Warning, true);

                    // Decrement the write attempt
                    writeAttempt--;
                }

                // Throw as timeout
                throw new TimeoutException($"The url: {url} keeps returning invalid data while trying to store into: {fileInfo.FullName}. Failing...");
            }
            // Ignore cancellation exceptions
            catch (TaskCanceledException)
            {
                // Return false as Cancelled
                return false;
            }
            catch (OperationCanceledException)
            {
                // Return false as Cancelled
                return false;
            }
            catch (Exception ex)
            {
                // ErrorSender.SendException(ex, ErrorType.Connection);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Error has occured while downloading in background for: {url}\r\n{ex}", LogType.Error, true);

                // Return false as failed
                return false;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                ProcessingUrls.Remove(url);
            }
        }
        
        private static readonly ConcurrentDictionary<string, int> UrlRetryCount = new();

        private static async Task<Stream> GetFallbackStreamUrl(HttpClient? client, string urlLocal, CancellationToken tokenLocal)
        {
            Stream returnStream;
            
            const int maxRetries   = 4;
            const int baseDelay    = 1000; // 1 second base delay

            var currentRetry = UrlRetryCount.AddOrUpdate(urlLocal, 1, (_, val) => val + 1);
            
            try
            {
                returnStream =  await GetFallbackStreamUrlInner(client, urlLocal, tokenLocal);
            }
            catch (Exception ex)
            {
                if (currentRetry > maxRetries)
                {
                    Logger.LogWriteLine($"Failed to download resource from: {urlLocal} after {maxRetries} attempts.\r\n{ex}", LogType.Error, true);
                    UrlRetryCount.TryRemove(urlLocal, out _);
                    throw;
                }
                
                var delay = baseDelay * currentRetry;
                Logger.LogWriteLine($"Failed to download resource from: {urlLocal} (Attempt {currentRetry}/{maxRetries}). Retrying in {delay}ms...\r\n{ex}", LogType.Warning, true);
                UrlRetryCount[urlLocal] = currentRetry;
                await Task.Delay(delay, tokenLocal);
                returnStream =  await GetFallbackStreamUrl(client, urlLocal, tokenLocal); // Recursive call to retry
            }
            
            UrlRetryCount.TryRemove(urlLocal, out _);
            return returnStream;
        }
        
        private static async Task<Stream> GetFallbackStreamUrlInner(HttpClient? client, string urlLocal, CancellationToken tokenLocal)
        {
            if (client == null)
                return await FallbackCDNUtil.GetHttpStreamFromResponse(urlLocal, tokenLocal);

            return await BridgedNetworkStream.CreateStream(await client.GetAsync(urlLocal, HttpCompletionOption.ResponseHeadersRead, tokenLocal),
                                                           tokenLocal);
        }

        public static async Task<string?> GetCachedSpritesAsync(string? url, bool isSkipHashCheck, CancellationToken token)
            => await GetCachedSpritesAsync(null, url, isSkipHashCheck, token);

        public static async Task<string?> GetCachedSpritesAsync(HttpClient? httpClient, string? url, bool isSkipHashCheck, CancellationToken token)
        {
            if (string.IsNullOrEmpty(url) ||
                ProcessingUrls.Contains(url))
            {
                Logger.LogWriteLine("Found duplicate download request, skipping...\r\n\t" +
                                    $"URL : {url}", LogType.Warning, true);
                return url;
            }

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(url));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (await IsFileCompletelyDownloadedAsync(fInfo, !isSkipHashCheck))
            {
                return cachePath;
            }

            if (!await TryDownloadToCompletenessAsync(url, httpClient, fInfo, isSkipHashCheck, token))
            {
                return url;
            }
            return cachePath;
        }

        private delegate bool WriteEmbeddedBase64DataToBuffer(ReadOnlySpan<char> chars, Span<byte> buffer, out int dataDecoded);

        public static string? CopyToLocalIfBase64(string? url, string? dirPath = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }

            WriteEmbeddedBase64DataToBuffer writeToDelegate;
            if (Base64Url.IsValid(url, out int bufferLen))
            {
                writeToDelegate = WriteBufferFromBase64Url;
            }
            else if (Base64.IsValid(url, out bufferLen))
            {
                writeToDelegate = WriteBufferFromBase64Raw;
            }
            else
            {
                return url;
            }

            ReadOnlySpan<char> urlAsSpan = url;

            dirPath ??= Path.GetTempPath();
            byte[] fileNameHash = HashUtility<XxHash128>.Shared.GetHashFromString(urlAsSpan.Length > 128 ? urlAsSpan[..^128] : urlAsSpan[..Math.Min(urlAsSpan.Length - 1, 128)]);
            string fileNameBase = HexTool.BytesToHexUnsafe(fileNameHash)!;
            string filePath = Path.Combine(dirPath, fileNameBase);

            string? existingFilePath = Directory
                .EnumerateFiles(dirPath, fileNameBase + ".*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(existingFilePath))
            {
                return existingFilePath;
            }

            byte[] decodedBuffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            try
            {
                if (!writeToDelegate(url, decodedBuffer, out int writtenToBuffer))
                {
                    return null;
                }

                string fileExt = PluginLauncherApiWrapper.DecideEmbeddedDataExtension(decodedBuffer);
                filePath += fileExt;

                using UnmanagedMemoryStream bufferStream = ToStream(new Span<byte>(decodedBuffer, 0, writtenToBuffer));
                using FileStream fileStream = File.Create(filePath);
                bufferStream.CopyTo(fileStream);

                return filePath;
            }
            catch
#if DEBUG
            (Exception ex)
#endif
            {
#if DEBUG
                Logger.LogWriteLine($"An error has occurred while writing Base64 URL to local file.\r\n{ex}", LogType.Error, true);
#endif
                return null;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(decodedBuffer);
            }

            static unsafe UnmanagedMemoryStream ToStream(Span<byte> buffer)
            {
                ref byte dataRef = ref MemoryMarshal.AsRef<byte>(buffer);
                return new UnmanagedMemoryStream((byte*)Unsafe.AsPointer(ref dataRef), buffer.Length);
            }

            static bool WriteBufferFromBase64Url(ReadOnlySpan<char> chars, Span<byte> buffer, out int dataDecoded)
            {
                if (Base64Url.TryDecodeFromChars(chars, buffer, out dataDecoded))
                {
                    return true;
                }

                dataDecoded = 0;
                return false;
            }

            bool WriteBufferFromBase64Raw(ReadOnlySpan<char> chars, Span<byte> buffer, out int dataDecoded)
            {
                int tempBufferToUtf8Len = Encoding.UTF8.GetByteCount(chars);
                byte[] tempBufferToUtf8 = ArrayPool<byte>.Shared.Rent(tempBufferToUtf8Len);
                try
                {
                    if (!Encoding.UTF8.TryGetBytes(chars, tempBufferToUtf8, out int utf8StrWritten))
                    {
                        dataDecoded = 0;
                        return false;
                    }

                    OperationStatus decodeStatus = Base64.DecodeFromUtf8(tempBufferToUtf8.AsSpan(0, utf8StrWritten), buffer, out _, out dataDecoded);
                    if (decodeStatus == OperationStatus.Done)
                    {
                        return true;
                    }

                    dataDecoded = 0;
#if DEBUG
                    throw new InvalidOperationException($"Cannot decode data string from Base64 as it returns with status: {decodeStatus}");
#else
                    return false;
#endif
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(tempBufferToUtf8);
                }
            }
        }
    }
}
