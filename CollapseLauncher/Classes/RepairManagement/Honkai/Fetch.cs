using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.Cache;
using Hi3Helper.EncTool.Parser.Senadina;
using Hi3Helper.Http;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.Win32;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    internal struct HonkaiRepairAssetIgnore
    {
        internal static HonkaiRepairAssetIgnore CreateEmpty() => new HonkaiRepairAssetIgnore()
        {
            IgnoredAudioPCKType = Array.Empty<AudioPCKType>(),
            IgnoredVideoCGSubCategory = Array.Empty<int>()
        };

        internal AudioPCKType[] IgnoredAudioPCKType;
        internal int[] IgnoredVideoCGSubCategory;
    }

    internal partial class HonkaiRepair
    {
        private string? _mainMetaRepoUrl;
        private readonly byte[] _collapseHeader = new byte[8] { 0x43, 0x6F, 0x6C, 0x6C, 0x61, 0x70, 0x73, 0x65 };

        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressTotalIndetermined = true;
            UpdateStatus();

            // Initialize the Senadina File Identifier
            Dictionary<string, SenadinaFileIdentifier>? senadinaFileIdentifier = null;

            // Use HttpClient instance on fetching
            Http _httpClient = new Http(true, 5, 1000, _userAgent);
            try
            {
                // Subscribe the fetching progress and subscribe cacheUtil progress to adapter
                _httpClient.DownloadProgress += _httpClient_FetchAssetProgress;
                _cacheUtil.ProgressChanged += _innerObject_ProgressAdapter;
                _cacheUtil.StatusChanged += _innerObject_StatusAdapter;

                // Update thee progress bar state
                _status.IsProgressPerFileIndetermined = true;
                UpdateStatus();

                // Region: XMFAndAssetIndex
                // Fetch metadata
                Dictionary<string, string> manifestDict = await FetchMetadata(_httpClient, token);

                // Check for manifest. If it doesn't exist, then throw and warn the user
                if (!manifestDict.ContainsKey(_gameVersion.VersionString))
                {
                    throw new VersionNotFoundException($"Manifest for {_gameVersionManager.GamePreset.ZoneName} (version: {_gameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");
                }

                // Initialize local audio manifest, blocks and patchConfig stream.
                SenadinaFileIdentifier? audioManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? blocksBaseManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? blocksCurrentManifestSenadinaFileIdentifier = null;
                SenadinaFileIdentifier? patchConfigManifestSenadinaFileIdentifier = null;
                _mainMetaRepoUrl = null;

                // Get the instance of the inner HttpClient from Hi3Helper.Http
                HttpClient httpClient = _httpClient.GetHttpClient();

                // Get the status if the current game is Senadina version.
                GameTypeHonkaiVersion gameVersionKind = _gameVersionManager.CastAs<GameTypeHonkaiVersion>();
                int[] versionArray = gameVersionKind.GetGameVersionAPI().VersionArray;
                bool IsSenadinaVersion = gameVersionKind.IsCurrentSenadinaVersion;

                // TODO: Use FallbackCDNUtil to fetch the stream.
                if (IsSenadinaVersion && !_isOnlyRecoverMain)
                {
                    _mainMetaRepoUrl = $"https://r2.bagelnl.my.id/cl-meta/pustaka/{_gameVersionManager.GamePreset.ProfileName}/{string.Join('.', versionArray)}";

                    // Get the Senadina File Identifier Dictionary and its file references
                    senadinaFileIdentifier = await GetSenadinaIdentifierDictionary(httpClient, _mainMetaRepoUrl, token);
                    audioManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(httpClient, senadinaFileIdentifier, SenadinaKind.chiptunesCurrent, versionArray, _mainMetaRepoUrl, token);
                    blocksBaseManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(httpClient, senadinaFileIdentifier, SenadinaKind.bricksBase, versionArray, _mainMetaRepoUrl, token);
                    blocksCurrentManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(httpClient, senadinaFileIdentifier, SenadinaKind.bricksCurrent, versionArray, _mainMetaRepoUrl, token);
                    patchConfigManifestSenadinaFileIdentifier = await GetSenadinaIdentifierKind(httpClient, senadinaFileIdentifier, SenadinaKind.wandCurrent, versionArray, _mainMetaRepoUrl, token);
                }

                if (!_isOnlyRecoverMain)
                {
                    // Get the list of ignored assets
                    HonkaiRepairAssetIgnore IgnoredAssetIDs = GetIgnoredAssetsProperty();

                    // Region: VideoIndex via External -> _cacheUtil: Data Fetch
                    // Fetch video index and also fetch the gateway URL
                    (string, string) gatewayURL;
                    gatewayURL = await FetchVideoAndGateway(_httpClient, assetIndex, IgnoredAssetIDs, token);
                    _assetBaseURL = "http://" + gatewayURL.Item1 + '/';
                    _gameServer = _cacheUtil?.GetCurrentGateway();

                    // Region: AudioIndex
                    // Try check audio manifest.m file and fetch it if it doesn't exist
                    await FetchAudioIndex(httpClient, assetIndex, IgnoredAssetIDs, audioManifestSenadinaFileIdentifier, token);
                }

                // Assign the URL based on the version
                _gameRepoURL = manifestDict[_gameVersion.VersionString];

                // Region: XMFAndAssetIndex
                // Fetch asset index
                await FetchAssetIndex(assetIndex, token);

                if (!_isOnlyRecoverMain)
                {
                    // Region: XMFAndAssetIndex
                    // Try check XMF file and fetch it if it doesn't exist
                    await FetchXMFFile(httpClient, assetIndex,
                        blocksBaseManifestSenadinaFileIdentifier, blocksCurrentManifestSenadinaFileIdentifier,
                        patchConfigManifestSenadinaFileIdentifier, manifestDict[_gameVersion.VersionString], token);

                    // Remove plugin from assetIndex
                    // Skip the removal for Delta-Patch
                    EliminatePluginAssetIndex(assetIndex);
                }
            }
            finally
            {
                // Unsubscribe the fetching progress and dispose it and unsubscribe cacheUtil progress to adapter
                _httpClient.DownloadProgress -= _httpClient_FetchAssetProgress;
                _cacheUtil.ProgressChanged -= _innerObject_ProgressAdapter;
                _cacheUtil.StatusChanged -= _innerObject_StatusAdapter;
                _httpClient.Dispose();
                senadinaFileIdentifier?.Clear();
            }
        }

        private async Task<Dictionary<string, SenadinaFileIdentifier>?> GetSenadinaIdentifierDictionary(HttpClient client, string mainUrl, CancellationToken token)
        {
            string identifierUrl = CombineURLFromString(mainUrl, $"daftar-pustaka");
            using Stream fileIdentifierStream = await HttpResponseInputStream.CreateStreamAsync(client, identifierUrl, null, null, token);
            using Stream fileIdentifierStreamDecoder = new BrotliStream(fileIdentifierStream, CompressionMode.Decompress, true);

            await ThrowIfFileIsNotSenadina(fileIdentifierStream, token);
#if DEBUG
            using StreamReader rd = new StreamReader(fileIdentifierStreamDecoder);
            string response = await rd.ReadToEndAsync();
            LogWriteLine($"[HonkaiRepair::GetSenadinaIdentifierDictionary() Dictionary Response:\r\n{response}", LogType.Debug, true);
            return response.Deserialize<Dictionary<string, SenadinaFileIdentifier>>(SenadinaJSONContext.Default);
#else
            return await fileIdentifierStreamDecoder.DeserializeAsync<Dictionary<string, SenadinaFileIdentifier>>(SenadinaJSONContext.Default, token);
#endif
        }

        private async Task<SenadinaFileIdentifier?> GetSenadinaIdentifierKind(HttpClient client, Dictionary<string, SenadinaFileIdentifier>? dict, SenadinaKind kind, int[] gameVersion, string mainUrl, CancellationToken token)
        {
            string origFileRelativePath = $"{gameVersion[0]}_{gameVersion[1]}_{kind.ToString().ToLower()}";
            string hashedRelativePath = SenadinaFileIdentifier.GetHashedString(origFileRelativePath);

            string fileUrl = CombineURLFromString(mainUrl, hashedRelativePath);
            if (!dict.ContainsKey(origFileRelativePath))
                throw new KeyNotFoundException($"Key reference to the pustaka file: {hashedRelativePath} is not found for game version: {string.Join('.', gameVersion)}. Please contact us on our Discord Server to report this issue.");

            SenadinaFileIdentifier? identifier = dict[origFileRelativePath];
            Stream networkStream = await HttpResponseInputStream.CreateStreamAsync(client, fileUrl, 0, null, token);

            await ThrowIfFileIsNotSenadina(networkStream, token);
            identifier.fileStream = SenadinaFileIdentifier.CreateKangBakso(networkStream, identifier.lastIdentifier, origFileRelativePath, (int)identifier.fileTime);
            identifier.relativePath = origFileRelativePath;

            return identifier;
        }

        private async ValueTask ThrowIfFileIsNotSenadina(Stream stream, CancellationToken token)
        {
            Memory<byte> header = new byte[_collapseHeader.Length];
            await stream.ReadAsync(header, token);
            if (!header.Span.SequenceEqual(_collapseHeader))
                throw new InvalidDataException($"Daftar pustaka file is corrupted! Expecting header: 0x{BinaryPrimitives.ReadInt64LittleEndian(_collapseHeader):x8} but got: 0x{BinaryPrimitives.ReadInt64LittleEndian(header.Span):x8} instead!");
        }

        private void EliminatePluginAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            _gameVersionManager.GameAPIProp.data.plugins?.ForEach(plugin =>
            {
                assetIndex.RemoveAll(asset =>
                {
                    return plugin.package.validate?.Exists(validate => validate.path == asset.N) ?? false;
                });
            });
        }

        #region Registry Utils
#nullable enable
        private HonkaiRepairAssetIgnore GetIgnoredAssetsProperty()
        {
            // Try get the parent registry key
            RegistryKey? keys = Registry.CurrentUser.OpenSubKey(_gameVersionManager.GamePreset.ConfigRegistryLocation);
            if (keys == null) return HonkaiRepairAssetIgnore.CreateEmpty(); // Return an empty property if the parent key doesn't exist

            // Initialize the property
            AudioPCKType[] IgnoredAudioPCKTypes = Array.Empty<AudioPCKType>();
            int[] IgnoredVideoCGSubCategory = Array.Empty<int>();

            // Try get the values of the registry key of the Audio ignored list
            object? objIgnoredAudioPCKTypes = keys?.GetValue("GENERAL_DATA_V2_DeletedAudioTypes_h214176984");
            if (objIgnoredAudioPCKTypes != null)
            {
                ReadOnlySpan<byte> bytesIgnoredAudioPckTypes = (byte[])objIgnoredAudioPCKTypes;
                IgnoredAudioPCKTypes = bytesIgnoredAudioPckTypes.Deserialize<AudioPCKType[]>(InternalAppJSONContext.Default) ?? IgnoredAudioPCKTypes;
            }

            // Try get the values of the registry key of the Video CG ignored list
            object? objIgnoredVideoCGSubCategory = keys?.GetValue("GENERAL_DATA_V2_DeletedCGPackages_h2282700200");
            if (objIgnoredVideoCGSubCategory != null)
            {
                ReadOnlySpan<byte> bytesIgnoredVideoCGSubCategory = (byte[])objIgnoredVideoCGSubCategory;
                IgnoredVideoCGSubCategory = bytesIgnoredVideoCGSubCategory.Deserialize<int[]>(InternalAppJSONContext.Default) ?? IgnoredVideoCGSubCategory;
            }

            // Return the property value
            return new HonkaiRepairAssetIgnore { IgnoredAudioPCKType = IgnoredAudioPCKTypes, IgnoredVideoCGSubCategory = IgnoredVideoCGSubCategory };
        }
#nullable disable
        #endregion

        #region VideoIndex via External -> _cacheUtil: Data Fetch
        private async Task<(string, string)> FetchVideoAndGateway(Http _httpClient, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, CancellationToken token)
        {
            // Fetch data cache file only and get the gateway
            (List<CacheAsset>, string, string, int) cacheProperty = await _cacheUtil.GetCacheAssetList(_httpClient, CacheAssetType.Data, token);

            if (!_isOnlyRecoverMain)
            {
                // Find the cache asset. If null, then return
                CacheAsset cacheAsset = cacheProperty.Item1.Where(x => x.N.EndsWith($"{HashID.CGMetadata}")).FirstOrDefault();

                // Deserialize and build video index into asset index
                await BuildVideoIndex(_httpClient, cacheAsset, cacheProperty.Item2, assetIndex, ignoredAssetIDs, cacheProperty.Item4, token);
            }

            // Return the gateway URL including asset bundle and asset cache
            return (cacheProperty.Item2, cacheProperty.Item3);
        }

        private async Task BuildVideoIndex(Http _httpClient, CacheAsset cacheAsset, string assetBundleURL, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, int luckyNumber, CancellationToken token)
        {
            // Get the remote stream and use CacheStream
            using (Stream memoryStream = new MemoryStream())
            {
                // Download the cache and store it to MemoryStream
                await _httpClient.Download(cacheAsset.ConcatURL, memoryStream, null, null, token);
                memoryStream.Position = 0;

                // Use CacheStream to decrypt and read it as Stream
                await using CacheStream cacheStream = new CacheStream(memoryStream, true, luckyNumber);
                // Enumerate and iterate the metadata to asset index
                await BuildAndEnumerateVideoVersioningFile(token, CGMetadata.Enumerate(cacheStream, Encoding.UTF8), assetIndex, ignoredAssetIDs, assetBundleURL);
            }
        }

        private async Task BuildAndEnumerateVideoVersioningFile(CancellationToken token, IEnumerable<CGMetadata> enumEntry, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, string assetBundleURL)
        {
            // Get the base URL
            string baseURL = CombineURLFromString("http://" + assetBundleURL, "/Video/");

            // Build video versioning file
            using (StreamWriter sw = new StreamWriter(Path.Combine(_gamePath, NormalizePath(_videoBaseLocalPath), "Version.txt"), false))
            {
                // Iterate the metadata to be converted into asset index in parallel
                await Parallel.ForEachAsync(enumEntry, new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = _threadCount
                }, async (metadata, token) =>
                {
                    // Only add remote available videos (not build-in) and check if the CG file is available in the server
                    // Edit: 2023-12-09
                    // Starting from 7.1, the CGs that have included in ignoredAssetIDs (which is marked as deleted) will be ignored.
                    bool isCGAvailable = await IsCGFileAvailable(metadata, baseURL, token);
                    bool isCGIgnored = ignoredAssetIDs.IgnoredVideoCGSubCategory.Contains(metadata.CgSubCategory);
                    if (!metadata.InStreamingAssets)
                    {
                        lock (sw)
                        {
                            // Append the versioning list
                            sw.WriteLine("Video/" + (_audioLanguage == AudioLanguageType.Japanese ? metadata.CgPathHighBitrateJP : metadata.CgPathHighBitrateCN) + ".usm\t1");
                        }
                    }

#if DEBUG
                    if (isCGIgnored)
                        LogWriteLine($"Ignoring CG Category: {metadata.CgSubCategory} {(_audioLanguage == AudioLanguageType.Japanese ? metadata.CgPathHighBitrateJP : metadata.CgPathHighBitrateCN)}", LogType.Debug, true);
#endif

                    if (!metadata.InStreamingAssets && isCGAvailable && !isCGIgnored)
                    {
                        string name = (_audioLanguage == AudioLanguageType.Japanese ? metadata.CgPathHighBitrateJP : metadata.CgPathHighBitrateCN) + ".usm";
                        lock (assetIndex)
                        {
                            assetIndex.Add(new FilePropertiesRemote
                            {
                                N = CombineURLFromString(_videoBaseLocalPath, name),
                                RN = CombineURLFromString(baseURL, name),
                                S = _audioLanguage == AudioLanguageType.Japanese ? metadata.FileSizeHighBitrateJP : metadata.FileSizeHighBitrateCN,
                                FT = FileType.Video
                            });
                        }
                    }
                });
            }
        }

        private async ValueTask<bool> IsCGFileAvailable(CGMetadata cgInfo, string baseURL, CancellationToken token)
        {
            // If the file has no appoinment schedule (like non-birthday CG), then return true
            if (cgInfo.AppointmentDownloadScheduleID == 0) return true;

            // Update the status
            _status.ActivityStatus = string.Format("Trying to determine CG asset availability: {0}", cgInfo.CgExtraKey);
            _status.IsProgressTotalIndetermined = true;
            _status.IsProgressPerFileIndetermined = true;
            UpdateStatus();

            // Set the URL and try get the status
            string cgURL = CombineURLFromString(baseURL, (_audioLanguage == AudioLanguageType.Japanese ? cgInfo.CgPathHighBitrateJP : cgInfo.CgPathHighBitrateCN) + ".usm");
            using HttpResponseMessage urlStatus = await FallbackCDNUtil.GetURLHttpResponse(cgURL, token);

            LogWriteLine($"The CG asset: {(_audioLanguage == AudioLanguageType.Japanese ? cgInfo.CgPathHighBitrateJP : cgInfo.CgPathHighBitrateCN)} " + (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);

            return urlStatus.IsSuccessStatusCode;
        }

        #endregion

        #region AudioIndex
        private async Task FetchAudioIndex(HttpClient _httpClient, List<FilePropertiesRemote> assetIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, SenadinaFileIdentifier? senadinaFileIdentifier, CancellationToken token)
        {
            // If the gameServer is null, then just leave
            if (_gameServer == null)
            {
                LogWriteLine("We found that the Dispatch/GameServer has return a null. Please report this issue to Collapse's Contributor or submit an issue!", LogType.Warning, true);
                return;
            }

            // Set manifest.m local path and remote URL
            string manifestLocalPath = Path.Combine(_gamePath, NormalizePath(_audioBaseLocalPath), "manifest.m");
            string manifestRemotePath = string.Format(CombineURLFromString(_audioBaseRemotePath, _gameServer.Manifest.ManifestAudio.ManifestAudioPlatform.ManifestWindows), $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision);

            try
            {
                // Try to get the audio manifest and deserialize it
                KianaAudioManifest manifest = await TryGetAudioManifest(_httpClient, senadinaFileIdentifier, manifestLocalPath, manifestRemotePath, token);

                // Deserialize manifest and build Audio Index
                await BuildAudioIndex(manifest, assetIndex, ignoredAssetIDs, token);

                // Build audio version file
                BuildAudioVersioningFile(manifest, ignoredAssetIDs);
            }
            // If a throw was thrown, then try to redownload the manifest.m file and try deserialize it again
            catch (Exception ex)
            {
                LogWriteLine($"Exception was thrown while reading audio manifest file!\r\n{ex}", LogType.Warning, true);
                return;
            }
        }

        private void BuildAudioVersioningFile(KianaAudioManifest audioManifest, HonkaiRepairAssetIgnore ignoredAssetIDs)
        {
            // Build audio versioning file
            using (StreamWriter sw = new StreamWriter(Path.Combine(_gamePath, NormalizePath(_audioBaseLocalPath), "Version.txt"), false))
            {
                // Edit: 2023-12-09
                // Starting from 7.1, the Audio Packages that have included in ignoredAssetIDs (which is marked as deleted) will be ignored.
                foreach (ManifestAssetInfo audioAsset in audioManifest
                    .AudioAssets
                    .Where(audioInfo => (audioInfo.Language == AudioLanguageType.Common
                                      || audioInfo.Language == _audioLanguage)
                                      && !ignoredAssetIDs.IgnoredAudioPCKType.Contains(audioInfo.PckType)))
                {
                    // Only add common and language specific audio file
                    sw.WriteLine($"{audioAsset.Name}.pck\t{audioAsset.HashString}");
                }
            }
        }

        private async Task BuildAudioIndex(KianaAudioManifest audioManifest, List<FilePropertiesRemote> audioIndex, HonkaiRepairAssetIgnore ignoredAssetIDs, CancellationToken token)
        {
            // Iterate the audioAsset to be added in audioIndex in parallel
            // Edit: 2023-12-09
            // Starting from 7.1, the Audio Packages that have included in ignoredAssetIDs (which is marked as deleted) will be ignored.
            await Parallel.ForEachAsync(audioManifest
                .AudioAssets
                .Where(audioInfo => (audioInfo.Language == AudioLanguageType.Common
                                  || audioInfo.Language == _audioLanguage)
                                  && !ignoredAssetIDs.IgnoredAudioPCKType.Contains(audioInfo.PckType)),
                new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = _threadCount
                }, async (audioInfo, token) =>
                {
                    // Try get the availability of the audio asset
                    if (await IsAudioFileAvailable(audioInfo, token))
                    {
                        // Skip AUDIO_Default since it's already been provided by base index
                        if (audioInfo.Name != "AUDIO_Default")
                        {
                            lock (audioIndex)
                            {
                                // Assign based on each values
                                FilePropertiesRemote audioAsset = new FilePropertiesRemote
                                {
                                    RN = audioInfo.Path,
                                    N = CombineURLFromString(_audioBaseLocalPath, audioInfo.Name + ".pck"),
                                    S = audioInfo.Size,
                                    FT = FileType.Audio,
                                    CRC = audioInfo.HashString,
                                    AudioPatchInfo = audioInfo.IsHasPatch ? audioInfo.PatchInfo : null,
                                };

                                // Add audioAsset to audioIndex
                                audioIndex.Add(audioAsset);
                            }
                        }
                    }
                });
        }

        private async ValueTask<bool> IsAudioFileAvailable(ManifestAssetInfo audioInfo, CancellationToken token)
        {
            // If the file is static (NeedMap == true), then pass
            if (audioInfo.NeedMap) return true;

            // Update the status
            _status.ActivityStatus = string.Format("Trying to determine audio asset availability: {0}", audioInfo.Path);
            _status.IsProgressTotalIndetermined = true;
            _status.IsProgressPerFileIndetermined = true;
            UpdateStatus();

            // Set the URL and try get the status
            string audioURL = CombineURLFromString(string.Format(_audioBaseRemotePath, $"{_gameVersion.Major}_{_gameVersion.Minor}", _gameServer.Manifest.ManifestAudio.ManifestAudioRevision), audioInfo.Path);
            using HttpResponseMessage urlStatus = await FallbackCDNUtil.GetURLHttpResponse(audioURL, token);

            LogWriteLine($"The audio asset: {audioInfo.Path} " + (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);

            return urlStatus.IsSuccessStatusCode;
        }

        private async Task<KianaAudioManifest> TryGetAudioManifest(HttpClient client, SenadinaFileIdentifier? senadinaFileIdentifier, string manifestLocal, string manifestRemote, CancellationToken token)
        {
            // Always check if the folder is exist
            string manifestFolder = Path.GetDirectoryName(manifestLocal);
            if (!Directory.Exists(manifestFolder))
            {
                Directory.CreateDirectory(manifestFolder);
            }

            using Stream originalFile = await senadinaFileIdentifier.GetOriginalFileStream(client, token);
            using FileStream localFile = new FileStream(manifestLocal, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

            // Start downloading manifest.m
            await DoCopyStreamProgress(originalFile, localFile, token);
            Stream networkStream = senadinaFileIdentifier.fileStream;

            // Deserialize the manifest
            return new KianaAudioManifest(networkStream, _gameVersion.VersionArrayManifest, true);
        }
        #endregion

        #region XMFAndAssetIndex
        private async Task<Dictionary<string, string>> FetchMetadata(Http _httpClient, CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(AppGameRepoIndexURLPrefix, _gameVersionManager.GamePreset.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream.DeserializeAsync<Dictionary<string, string>>(CoreLibraryJSONContext.Default, token);
        }

        private async Task FetchAssetIndex(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set asset index URL
            string urlIndex = string.Format(AppGameRepairIndexURLPrefix, _gameVersionManager.GamePreset.ProfileName, _gameVersion.VersionString) + ".binv2";

            // Start downloading asset index using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlIndex, token);

            // Deserialize asset index and return
            DeserializeAssetIndexV2(stream, assetIndex);
        }

        private void DeserializeAssetIndexV2(Stream stream, List<FilePropertiesRemote> assetIndexList)
        {
            AssetIndexV2 assetIndex = new AssetIndexV2();
            PkgVersionProperties[] pkgVersionEntries = assetIndex.Deserialize(stream, out DateTime timestamp);
            LogWriteLine($"[HonkaiRepair::DeserializeAssetIndexV2()] Asset index V2 has been deserialized with: {pkgVersionEntries.Length} assets found. Asset index was generated at: {timestamp} (UTC)", LogType.Default, true);

            bool isOnlyRecoverMain = _isOnlyRecoverMain;

            foreach (PkgVersionProperties pkgVersionEntry in pkgVersionEntries)
            {
                // Skip the .wmv file if main recovery check only is not performed
                if (!isOnlyRecoverMain && Path.GetExtension(pkgVersionEntry.remoteName).ToLower() == ".wmv")
                    continue;

                FilePropertiesRemote assetInfo = new FilePropertiesRemote
                {
                    FT = FileType.Generic,
                    N = pkgVersionEntry.remoteName,
                    S = pkgVersionEntry.fileSize,
                    CRC = pkgVersionEntry.md5,
                    RN = CombineURLFromString(_gameRepoURL, pkgVersionEntry.remoteName)
                };

#if DEBUG
                LogWriteLine($"[HonkaiRepair::DeserializeAssetIndexV2()] {assetInfo.PrintSummary()} found in manifest", LogType.Default, true);
#endif
                assetIndexList.Add(assetInfo);
            }
        }

        private async Task FetchXMFFile(HttpClient _httpClient, List<FilePropertiesRemote> assetIndex, SenadinaFileIdentifier? xmfBaseIdentifier, SenadinaFileIdentifier? xmfCurrentIdentifier, SenadinaFileIdentifier? patchConfigIdentifier, string _repoURL, CancellationToken token)
        {
            // Set Primary XMF Path
            string xmfPriPath = Path.Combine(_gamePath, "BH3_Data\\StreamingAssets\\Asb\\pc\\Blocks.xmf");
            // Set Secondary XMF Path
            string xmfSecPath = Path.Combine(_gamePath, $"BH3_Data\\StreamingAssets\\Asb\\pc\\Blocks_{_gameVersion.Major}_{_gameVersion.Minor}.xmf");

#nullable enable
            // Initialize patch config info variable
            BlockPatchManifest? patchConfigInfo = null;

            // Initialize temporary XMF stream
            using MemoryStream tempXMFStream = new();
            using Stream secondaryXMFStream = _isOnlyRecoverMain ? await xmfBaseIdentifier!.GetOriginalFileStream(_httpClient, token) : await xmfCurrentIdentifier!.GetOriginalFileStream(_httpClient, token);
            using Stream dataXMFStream = _isOnlyRecoverMain ? xmfBaseIdentifier!.fileStream! : xmfCurrentIdentifier!.fileStream!;

            // Fetch only RecoverMain is disabled
            using (FileStream fs1 = new FileStream(EnsureCreationOfDirectory(_isOnlyRecoverMain ? xmfPriPath : xmfSecPath), FileMode.Create, FileAccess.ReadWrite))
            {
                // Download the secondary XMF into MemoryStream
                await DoCopyStreamProgress(secondaryXMFStream, fs1, token);

                // Copy the secondary XMF into primary XMF if _isOnlyRecoverMain == false
                if (!_isOnlyRecoverMain)
                {
                    using (FileStream fs2 = new FileStream(EnsureCreationOfDirectory(xmfPriPath), FileMode.Create, FileAccess.Write))
                    {
                        fs1.Position = 0;
                        fs1.CopyTo(fs2);
                    }
                }
            }

            // Get the estimated size of the local xmf size
            FileInfo xmfFileInfoLocal = new FileInfo(_isOnlyRecoverMain ? xmfPriPath : xmfSecPath);
            long? estimatedXmfSize = !xmfFileInfoLocal.Exists ? null : xmfFileInfoLocal.Length;

            // Copy the source stream into temporal stream
            await DoCopyStreamProgress(dataXMFStream, tempXMFStream, token, estimatedXmfSize);
            tempXMFStream.Position = 0;

            // Fetch for PatchConfig.xmf file (Block patch metadata)
            if (!_isOnlyRecoverMain)
            {
                patchConfigInfo = await FetchPatchConfigXMFFile(tempXMFStream, patchConfigIdentifier, _httpClient, token);
            }

            // Reset the temporal stream pos.
            tempXMFStream.Position = 0;

            // After all completed, then Deserialize the XMF to build the asset index
            BuildBlockIndex(assetIndex, patchConfigInfo, _isOnlyRecoverMain ? xmfPriPath : xmfSecPath, tempXMFStream);
#nullable disable
        }

        private async Task<BlockPatchManifest> FetchPatchConfigXMFFile(Stream xmfStream, SenadinaFileIdentifier patchConfigFileIdentifier, HttpClient _httpClient, CancellationToken token)
        {
            // Start downloading XMF and load it to MemoryStream first
            using (MemoryStream mfs = new MemoryStream())
            {
                // Copy the remote stream of Patch Config to temporal mfs
                await patchConfigFileIdentifier.fileStream.CopyToAsync(mfs, token);
                // Reset the MemoryStream position
                mfs.Position = 0;

#nullable enable
                // Get the version provided by the XMF
                int[]? gameVersion = XMFUtility.GetXMFVersion(xmfStream);
                if (gameVersion == null) return null;
#nullable disable

                // Initialize and parse the manifest, then return the Patch Asset
                return new BlockPatchManifest(mfs, gameVersion);
            }
        }

#nullable enable
        private void BuildBlockIndex(List<FilePropertiesRemote> assetIndex, BlockPatchManifest? patchInfo, string xmfPath, Stream xmfStream)
        {
            // Initialize and parse the XMF file
            XMFParser xmfParser = new XMFParser(xmfPath, xmfStream);

            // Do loop and assign the block asset to asset index
            for (int i = 0; i < xmfParser.BlockCount; i++)
            {
                // Check if the patch info exist for current block, then assign blockPatchInfo
                BlockPatchInfo? blockPatchInfo = null;

                if (patchInfo != null && patchInfo.NewBlockCatalog.ContainsKey(xmfParser.BlockEntry[i].HashString))
                {
                    int blockPatchInfoIndex = patchInfo.NewBlockCatalog[xmfParser.BlockEntry[i].HashString];
                    blockPatchInfo = patchInfo.PatchAsset[blockPatchInfoIndex];
                }

                // Assign as FilePropertiesRemote
                FilePropertiesRemote assetInfo = new FilePropertiesRemote
                {
                    N = CombineURLFromString(_blockBasePath, xmfParser.BlockEntry[i].HashString + ".wmv"),
                    RN = CombineURLFromString(_blockAsbBaseURL, xmfParser.BlockEntry[i].HashString + ".wmv"),
                    S = xmfParser.BlockEntry[i].Size,
                    CRC = xmfParser.BlockEntry[i].HashString,
                    FT = FileType.Blocks,
                    BlockPatchInfo = blockPatchInfo
                };

                // Add the asset info
                assetIndex.Add(assetInfo);
            }

            // Write the blockVerifiedVersion based on secondary XMF
            File.WriteAllText(Path.Combine(_gamePath, NormalizePath(_blockBasePath), "blockVerifiedVersion.txt"), string.Join('_', xmfParser.Version));
        }
#nullable disable

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Filter out video assets
            List<FilePropertiesRemote> assetIndexFiltered = assetIndex.Where(x => x.FT != FileType.Video).ToList();

            // Sum the assetIndex size and assign to _progressTotalSize
            _progressTotalSize = assetIndexFiltered.Sum(x => x.S);

            // Assign the assetIndex count to _progressTotalCount
            _progressTotalCount = assetIndexFiltered.Count;
        }
        #endregion
    }
}
