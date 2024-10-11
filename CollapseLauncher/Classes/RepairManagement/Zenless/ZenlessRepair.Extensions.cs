using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.Sleepy;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher
{
    [JsonSerializable(typeof(ZenlessResManifestAsset))]
    [JsonSourceGenerationOptions(AllowOutOfOrderMetadataProperties = true, AllowTrailingCommas = true)]
    internal partial class ZenlessManifestContext : JsonSerializerContext { }

    internal class ZenlessManifestInterceptStream : Stream
    {
        private Stream redirectStream;
        private Stream innerStream;
        private bool isFieldStart;
        private bool isFieldEnd;
        private byte[] innerBuffer = new byte[16 << 10];

        internal ZenlessManifestInterceptStream(string? filePath, Stream stream)
        {
            innerStream = stream;
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                string? filePathDir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(filePathDir) && !Directory.Exists(filePathDir))
                {
                    Directory.CreateDirectory(filePathDir);
                }
                redirectStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                return;
            }
            redirectStream = Stream.Null;
        }

        public override bool CanRead => true;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            innerStream.Flush();
            redirectStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => InternalRead(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) => InternalRead(buffer);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await InternalReadAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await InternalReadAsync(buffer, cancellationToken);

        private readonly char[] searchStartValuesUtf16 = "\"files\": [".ToCharArray();
        private readonly byte[] searchStartValuesUtf8 = "\"files\": ["u8.ToArray();
        private readonly char[] searchEndValuesUtf16 = "]\r\n}".ToCharArray();
        private readonly byte[] searchEndValuesUtf8 = "]\r\n}"u8.ToArray();

        private async ValueTask<int> InternalReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (isFieldEnd)
                return 0;

            Start:
            // - 8 is important to ensure that the EOF detection is working properly
            int toRead = Math.Min(innerBuffer.Length - 8, buffer.Length);
            int read = await innerStream.ReadAtLeastAsync(innerBuffer.AsMemory(0, toRead), toRead, false, token);
            if (read == 0)
                return 0;

            await redirectStream.WriteAsync(innerBuffer, 0, read, token);

            int lastIndexOffset;
            if (isFieldStart && ((lastIndexOffset = EnsureIsEnd(innerBuffer, read)) > 0))
            {
                innerBuffer.AsSpan(0, lastIndexOffset).CopyTo(buffer.Span);
                isFieldEnd = true;
                return lastIndexOffset;
            }

            int offset = 0;
            if (!isFieldStart && !(isFieldStart = !((offset = EnsureIsStart(innerBuffer)) < 0)))
                goto Start;

            bool isOneGoBufferLoadEnd = read < toRead && isFieldStart && !isFieldEnd;
            ReadOnlySpan<byte> spanToCopy = isOneGoBufferLoadEnd ? innerBuffer.AsSpan(offset, read - offset).TrimEnd((byte)'}')
                : innerBuffer.AsSpan(offset, read - offset);

            spanToCopy.CopyTo(buffer.Span);
            return isOneGoBufferLoadEnd ? spanToCopy.Length : read - offset;
        }

        private int InternalRead(Span<byte> buffer)
        {
            if (isFieldEnd)
                return 0;

            Start:
            // - 8 is important to ensure that the EOF detection is working properly
            int toRead = Math.Min(innerBuffer.Length - 8, buffer.Length);
            int read = innerStream.ReadAtLeast(innerBuffer.AsSpan(0, toRead), toRead, false);
            if (read == 0)
                return 0;

            redirectStream.Write(innerBuffer, 0, read);

            int lastIndexOffset;
            if (isFieldStart && ((lastIndexOffset = EnsureIsEnd(innerBuffer, read)) > 0))
            {
                innerBuffer.AsSpan(0, lastIndexOffset).CopyTo(buffer);
                isFieldEnd = true;
                return lastIndexOffset;
            }

            int offset = 0;
            if (!isFieldStart && !(isFieldStart = !((offset = EnsureIsStart(innerBuffer)) < 0)))
                goto Start;

            innerBuffer.AsSpan(offset, read - offset).CopyTo(buffer);
            return read - offset;
        }

        private int EnsureIsEnd(Span<byte> buffer, int read)
        {
            ReadOnlySpan<char> bufferAsChars = MemoryMarshal.Cast<byte, char>(buffer);

            int lastIndexOfAnyUtf8 = buffer.LastIndexOf(searchEndValuesUtf8);
            if (lastIndexOfAnyUtf8 < searchEndValuesUtf8.Length)
            {
                int lastIndexOfAnyUtf16 = bufferAsChars.LastIndexOf(searchEndValuesUtf16);
                return lastIndexOfAnyUtf16 > 0 ? lastIndexOfAnyUtf16 + 1 : -1;
            }

            return lastIndexOfAnyUtf8 + 1;
        }

        private int EnsureIsStart(Span<byte> buffer)
        {
            ReadOnlySpan<char> bufferAsChars = MemoryMarshal.Cast<byte, char>(buffer);

            int indexOfAnyUtf8 = buffer.IndexOf(searchStartValuesUtf8);
            if (indexOfAnyUtf8 < searchStartValuesUtf8.Length)
            {
                int indexOfAnyUtf16 = bufferAsChars.IndexOf(searchStartValuesUtf16);
                return indexOfAnyUtf16 > 0 ? indexOfAnyUtf16 + (searchStartValuesUtf16.Length - 1) : -1;
            }

            return indexOfAnyUtf8 + (searchStartValuesUtf8.Length - 1);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask DisposeAsync()
        {
            if (innerStream != null)
                await innerStream.DisposeAsync();

            if (redirectStream != null)
                await redirectStream.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                innerStream?.Dispose();
                redirectStream?.Dispose();
            }
        }
    }

    internal static class ZenlessRepairExtensions
    {
        const string StreamingAssetsPath = "StreamingAssets\\";
        const string AssetTypeAudioPath = StreamingAssetsPath + "Audio\\Windows\\";
        const string AssetTypeBlockPath = StreamingAssetsPath + "Blocks\\";
        const string AssetTypeVideoPath = StreamingAssetsPath + "Video\\HD\\";

        const string PersistentAssetsPath = "Persistent\\";
        const string AssetTypeAudioPersistentPath = PersistentAssetsPath + "Audio\\Windows\\";
        const string AssetTypeBlockPersistentPath = PersistentAssetsPath + "Blocks\\";
        const string AssetTypeVideoPersistentPath = PersistentAssetsPath + "Video\\HD\\";

        internal static async IAsyncEnumerable<T?> MergeAsyncEnumerable<T>(params IAsyncEnumerable<T?>[] sources)
        {
            foreach (IAsyncEnumerable<T?> enumerable in sources)
            {
                await foreach (T? item in enumerable)
                {
                    yield return item;
                }
            }
        }

        internal static async IAsyncEnumerable<PkgVersionProperties> RegisterSleepyFileInfoToManifest(
            this SleepyFileInfoResult fileInfo,
            HttpClient httpClient,
            List<FilePropertiesRemote> assetIndex,
            bool needWriteToLocal,
            string persistentPath,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            string manifestFileUrl = ConverterTool.CombineURLFromString(fileInfo.BaseUrl, fileInfo.ReferenceFileInfo.FileName);
            using HttpResponseMessage responseMessage = await httpClient.GetAsync(manifestFileUrl, HttpCompletionOption.ResponseHeadersRead, token);

            string filePath = Path.Combine(persistentPath, fileInfo.ReferenceFileInfo.FileName + "_persist");

            await using Stream responseStream = await responseMessage.Content.ReadAsStreamAsync(token);
            await using Stream responseInterceptedStream = new ZenlessManifestInterceptStream(needWriteToLocal ? filePath : null, responseStream);

            IAsyncEnumerable<ZenlessResManifestAsset?> enumerable = JsonSerializer
                .DeserializeAsyncEnumerable(
                    responseInterceptedStream,
                    ZenlessManifestContext.Default.ZenlessResManifestAsset,
                    false,
                    token
                );

            await foreach (ZenlessResManifestAsset? manifest in enumerable)
            {
                if (manifest == null)
                {
                    continue;
                }

                yield return new PkgVersionProperties
                {
                    fileSize = manifest.FileSize,
                    isForceStoreInPersistent = manifest.IsPersistentFile,
                    isPatch = manifest.IsPersistentFile,
                    md5 = Convert.ToHexStringLower(manifest.Xxh64Hash),
                    remoteName = manifest.FileRelativePath
                };
            }
        }

        internal static IEnumerable<FilePropertiesRemote?> RegisterMainCategorizedAssetsToHashSet(this IEnumerable<PkgVersionProperties> assetEnumerable, List<FilePropertiesRemote> assetIndex, Dictionary<string, FilePropertiesRemote> hashSet, string baseLocalPath, string baseUrl)
        {
            foreach (PkgVersionProperties asset in assetEnumerable)
            {
                yield return ReturnCategorizedYieldValue(hashSet, assetIndex, asset, baseLocalPath, baseUrl);
            }
        }

        internal static async IAsyncEnumerable<FilePropertiesRemote?> RegisterResCategorizedAssetsToHashSetAsync(this IAsyncEnumerable<PkgVersionProperties> assetEnumerable, List<FilePropertiesRemote> assetIndex, Dictionary<string, FilePropertiesRemote> hashSet, string baseLocalPath, string baseUrl)
        {
            await foreach (PkgVersionProperties asset in assetEnumerable)
            {
                string baseLocalPathMerged = Path.Combine(baseLocalPath, asset.isPatch ? PersistentAssetsPath : StreamingAssetsPath);

                yield return ReturnCategorizedYieldValue(hashSet, assetIndex, asset, baseLocalPathMerged, baseUrl);
            }
        }

        private static FilePropertiesRemote? ReturnCategorizedYieldValue(Dictionary<string, FilePropertiesRemote> hashSet, List<FilePropertiesRemote> assetIndex, PkgVersionProperties asset, string baseLocalPath, string baseUrl)
        {
            FilePropertiesRemote asRemoteProperty = GetNormalizedFilePropertyTypeBased(
                    baseUrl,
                    baseLocalPath,
                    asset.remoteName,
                    asset.fileSize,
                    asset.md5,
                    FileType.Generic,
                    asset.isPatch);

            ReadOnlySpan<char> relTypeRelativePath = asRemoteProperty.GetAssetRelativePath(out RepairAssetType assetType);
            asRemoteProperty.FT = assetType switch
            {
                RepairAssetType.Audio => FileType.Audio,
                RepairAssetType.Block => FileType.Block,
                RepairAssetType.Video => FileType.Video,
                _ => FileType.Generic
            };

            if (!relTypeRelativePath.IsEmpty)
            {
                string relTypePath = assetType switch
                {
                    RepairAssetType.Audio => AssetTypeAudioPath,
                    RepairAssetType.Block => AssetTypeBlockPath,
                    RepairAssetType.Video => AssetTypeVideoPath,
                    _ => throw new NotSupportedException()
                };

                string relTypeRelativePathStr = relTypeRelativePath.ToString();
                if (!hashSet.TryAdd(relTypeRelativePathStr, asRemoteProperty))
                {
                    FilePropertiesRemote existingValue = hashSet[relTypeRelativePathStr];
                    int indexOf = assetIndex.IndexOf(existingValue);
                    if (indexOf < -1)
                        return asRemoteProperty;

                    assetIndex[indexOf] = asRemoteProperty;
                    hashSet[relTypeRelativePathStr] = asRemoteProperty;

                    return null;
                }
            }

            return asRemoteProperty;
        }

        private static FilePropertiesRemote GetNormalizedFilePropertyTypeBased(string remoteParentURL,
                                                                               string baseLocalPath,
                                                                               string remoteRelativePath,
                                                                               long fileSize,
                                                                               string hash,
                                                                               FileType type = FileType.Generic,
                                                                               bool isPatchApplicable = false)
        {
            string remoteAbsolutePath = type switch
            {
                FileType.Generic => ConverterTool.CombineURLFromString(remoteParentURL, remoteRelativePath),
                _ => remoteParentURL
            };
            string localAbsolutePath = Path.Combine(baseLocalPath, ConverterTool.NormalizePath(remoteRelativePath));

            return new FilePropertiesRemote
            {
                FT = type,
                CRC = hash,
                S = fileSize,
                N = localAbsolutePath,
                RN = remoteAbsolutePath,
                IsPatchApplicable = isPatchApplicable
            };
        }

        internal static ReadOnlySpan<char> GetAssetRelativePath(this FilePropertiesRemote asset, out RepairAssetType assetType)
        {
            assetType = RepairAssetType.Generic;

            int indexOfOffset;
            if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeAudioPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Audio;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeBlockPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Block;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeVideoPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Video;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeAudioPersistentPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Audio;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeBlockPersistentPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Block;
            }
            else if ((indexOfOffset = asset.N.LastIndexOf(AssetTypeVideoPersistentPath, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                assetType = RepairAssetType.Video;
            }

            if (indexOfOffset >= 0)
            {
                return asset.N.AsSpan(indexOfOffset);
            }

            return ReadOnlySpan<char>.Empty;
        }
    }
}
