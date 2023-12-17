using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetIndex;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class StarRailRepair
    {
        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressTotalIndetermined = true;
            UpdateStatus();

            try
            {
                // Get the primary manifest
                using Http httpClient = new Http();
                Dictionary<string, int> hashtable = new Dictionary<string, int>();
                await GetPrimaryManifest(httpClient, token, assetIndex, hashtable);

                // If the this._isOnlyRecoverMain && base._isVersionOverride is true, copy the asset index into the _originAssetIndex
                if (this._isOnlyRecoverMain && base._isVersionOverride)
                {
                    _originAssetIndex = new List<FilePropertiesRemote>();
                    foreach (FilePropertiesRemote asset in assetIndex)
                    {
                        _originAssetIndex.Add(asset.Copy());
                    }
                }

                // Subscribe the fetching progress and subscribe StarRailMetadataTool progress to adapter
                _innerGameVersionManager.StarRailMetadataTool.HttpEvent += _httpClient_FetchAssetProgress;

                // Initialize the metadata tool (including dispatcher and gateway).
                // Perform this if only base._isVersionOverride is false to indicate that the repair performed is
                // not for delta patch integrity check.
                if (!base._isVersionOverride && !this._isOnlyRecoverMain && await _innerGameVersionManager.StarRailMetadataTool.Initialize(token, GetExistingGameRegionID(), Path.Combine(_gamePath, $"{Path.GetFileNameWithoutExtension(_innerGameVersionManager.GamePreset.GameExecutableName)}_Data\\Persistent")))
                {
                    // Read block metadata and convert to FilePropertiesRemote
                    await _innerGameVersionManager.StarRailMetadataTool.ReadAsbMetadataInformation(token);
                    await _innerGameVersionManager.StarRailMetadataTool.ReadBlockMetadataInformation(token);
                    ConvertSRMetadataToAssetIndex(_innerGameVersionManager.StarRailMetadataTool.MetadataBlock, assetIndex, hashtable);

                    // Read Audio metadata and convert to FilePropertiesRemote
                    await _innerGameVersionManager.StarRailMetadataTool.ReadAudioMetadataInformation(token);
                    ConvertSRMetadataToAssetIndex(_innerGameVersionManager.StarRailMetadataTool.MetadataAudio, assetIndex, hashtable, true);

                    // Read Video metadata and convert to FilePropertiesRemote
                    await _innerGameVersionManager.StarRailMetadataTool.ReadVideoMetadataInformation(token);
                    ConvertSRMetadataToAssetIndex(_innerGameVersionManager.StarRailMetadataTool.MetadataVideo, assetIndex, hashtable);
                }

                // Force-Fetch the Bilibili SDK (if exist :pepehands:)
                await FetchBilibiliSDK(token);

                // Clear the hashtable
                hashtable.Clear();
            }
            finally
            {
                // Unsubscribe the fetching progress and dispose it and unsubscribe cacheUtil progress to adapter
                _innerGameVersionManager.StarRailMetadataTool.HttpEvent -= _httpClient_FetchAssetProgress;
            }
        }

        #region PrimaryManifest
        private async Task GetPrimaryManifest(Http client, CancellationToken token, List<FilePropertiesRemote> assetIndex, Dictionary<string, int> hashtable)
        {
            // Initialize pkgVersion list
            List<PkgVersionProperties> pkgVersion = new List<PkgVersionProperties>();

            // Initialize repo metadata
            bool isSuccess = false;
            try
            {
                // Get the metadata
                Dictionary<string, string> repoMetadata = await FetchMetadata(token);

                // Check for manifest. If it doesn't exist, then throw and warn the user
                if (!(isSuccess = repoMetadata.ContainsKey(_gameVersion.VersionString)))
                {
                    throw new VersionNotFoundException($"Manifest for {_gameVersionManager.GamePreset.ZoneName} (version: {_gameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");
                }

                // Assign the URL based on the version
                _gameRepoURL = repoMetadata[_gameVersion.VersionString];
            }
            // If the base._isVersionOverride is true, then throw. This sanity check is required if the delta patch is being performed.
            catch when (base._isVersionOverride) { throw; }

            // Fetch the asset index from CDN (also check if the status is success)
            if (isSuccess)
            {
                // Set asset index URL
                string urlIndex = string.Format(LauncherConfig.AppGameRepairIndexURLPrefix, _gameVersionManager.GamePreset.ProfileName, _gameVersion.VersionString) + ".bin";

                // Start downloading asset index using FallbackCDNUtil and return its stream
                await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlIndex, token);
                if (stream != null)
                {
                    // Deserialize asset index and set it to list
                    AssetIndexV2 parserTool = new AssetIndexV2();
                    pkgVersion = new List<PkgVersionProperties>(parserTool.Deserialize(stream, out DateTime timestamp));
                    LogWriteLine($"Asset index timestamp: {timestamp}", LogType.Default, true);
                }
            }
            else
            {
                // If the base._isVersionOverride is true (for delta patch), then return
                if (base._isVersionOverride) return;
                LogWriteLine($"Falling back to the miHoYo provided pkg_version as the asset index!", LogType.Warning, true);

                // Get the latest game property from the API
                GameInstallStateEnum gameState = _gameVersionManager.GetGameState();
                RegionResourceVersion gameVersion = _gameVersionManager.GetGameLatestZip(gameState).FirstOrDefault();

                // If the gameVersion is null, then return
                if (gameVersion == null) return;

                // Try get the uncompressed url and the pkg_version
                string baseZipURL = gameVersion.path;
                int lastIndexOfURL = baseZipURL.LastIndexOf('/');
                string baseUncompressedURL = baseZipURL.Substring(0, lastIndexOfURL);
                baseUncompressedURL = CombineURLFromString(baseUncompressedURL, "unzip");
                string basePkgVersionUrl = CombineURLFromString(baseUncompressedURL, "pkg_version");

                try
                {
                    // Set the _gameRepoURL
                    _gameRepoURL = baseUncompressedURL;

                    // Try get the data
                    using MemoryStream ms = new MemoryStream();
                    using StreamReader sr = new StreamReader(ms);
                    client.DownloadProgress += _httpClient_FetchAssetProgress;
                    await client.Download(basePkgVersionUrl, ms, null, null, token);

                    // Read the stream and deserialize the JSON
                    pkgVersion.Clear();
                    while (!sr.EndOfStream)
                    {
                        // Deserialize and add the line to pkgVersion list
                        string jsonLine = sr.ReadLine();
                        PkgVersionProperties entry = jsonLine.Deserialize<PkgVersionProperties>(CoreLibraryJSONContext.Default);
                        pkgVersion.Add(entry);
                    }
                }
                catch { throw; }
                finally
                {
                    // Unsubscribe the event
                    client.DownloadProgress -= _httpClient_FetchAssetProgress;
                }
            }

            // Convert the pkg version list to asset index
            ConvertPkgVersionToAssetIndex(pkgVersion, assetIndex, hashtable);

            // Clear the pkg version list
            pkgVersion.Clear();
        }

        private async Task<Dictionary<string, string>> FetchMetadata(CancellationToken token)
        {
            // Set metadata URL
            string urlMetadata = string.Format(LauncherConfig.AppGameRepoIndexURLPrefix, _gameVersionManager.GamePreset.ProfileName);

            // Start downloading metadata using FallbackCDNUtil
            await using BridgedNetworkStream stream = await FallbackCDNUtil.TryGetCDNFallbackStream(urlMetadata, token);
            return await stream.DeserializeAsync<Dictionary<string, string>>(CoreLibraryJSONContext.Default, token);
        }

        private void ConvertPkgVersionToAssetIndex(List<PkgVersionProperties> pkgVersion, List<FilePropertiesRemote> assetIndex, Dictionary<string, int> hashtable)
        {
            int count = 0;
            foreach (PkgVersionProperties entry in pkgVersion)
            {
                // Get the lookup string for the hashtable
                string lookupString = $"{Path.GetFileName(entry.remoteName)}|{entry.fileSize}|{entry.md5.ToLower()}";
                // If the hashtable has the lookup string already, then skip it.
                if (!hashtable.ContainsKey(lookupString))
                {
                    // Otherwise, add the lookup string to the hashtable
                    hashtable.Add(lookupString, count);
                }
                count++;

                // Add the pkgVersion entry to asset index
                assetIndex.Add(new FilePropertiesRemote
                {
                    FT = FileType.Generic,
                    CRC = entry.md5,
                    N = entry.remoteName,
                    RN = CombineURLFromString(_gameRepoURL, entry.remoteName),
                    S = entry.fileSize
                });
            }
        }
        #endregion

        #region Utilities
        private unsafe string GetExistingGameRegionID()
        {
#nullable enable
            // Try get the value as nullable object
            object? value = RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            // Check if the value is null, then return the default name
            if (value == null)
            {
                // Return the dispatch default name. If none, then throw
                return _innerGameVersionManager.GamePreset.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");
            }
#nullable disable

            // Cast the value as byte span
            ReadOnlySpan<byte> span = ((byte[])value).AsSpan();
            // Get the name from the span and trim the \0 character at the end
            string name = Encoding.UTF8.GetString(span.Slice(0, span.Length - 1));
            return name;
        }

        private void ConvertSRMetadataToAssetIndex(SRMetadataBase metadata, List<FilePropertiesRemote> assetIndex, Dictionary<string, int> hashtable, bool writeAudioLangRedord = false)
        {
            // Get the voice Lang ID
            int voLangID = _innerGameVersionManager.GamePreset.GetVoiceLanguageID();
            // Get the voice Lang name by ID
            string voLangName = _innerGameVersionManager.GamePreset.GetStarRailVoiceLanguageFullNameByID(voLangID);

            // If prompt to write Redord file
            if (writeAudioLangRedord)
            {
                // Get game executable name, directory and file path
                string execName = Path.GetFileNameWithoutExtension(_innerGameVersionManager.GamePreset.GameExecutableName);
                string audioRedordDir = Path.Combine(_gamePath, @$"{execName}_Data\Persistent\Audio\AudioPackage\Windows");
                string audioRedordPath = Path.Combine(audioRedordDir, "AudioLangRedord.txt");

                // Create the directory if not exist
                if (!Directory.Exists(audioRedordDir)) Directory.CreateDirectory(audioRedordDir);
                // Then write the Redord file content
                File.WriteAllText(audioRedordPath, "{\"AudioLang\":\"" + voLangName + "\"}");
            }

            int count = 0;
            long countSize = 0;

            // Enumerate the Asset List
            foreach (SRAsset asset in metadata.EnumerateAssets())
            {
                // Get the hash by bytes
                string hash = HexTool.BytesToHexUnsafe(asset.Hash);

                // Filter only current audio language file and other assets
                if (FilterCurrentAudioLangFile(asset, voLangName, out bool IsHasHashMark))
                {
                    // Get the lookup string and check whether the hashtable contains it.
                    // If yes, then override the one from assetIndex list.
                    string lookupString = $"{Path.GetFileName(asset.LocalName)}|{asset.Size}|{hash.ToLower()}";
                    if (hashtable.ContainsKey(lookupString))
                    {
                        // Get the index of the asset index
                        int indexOf = hashtable[lookupString];
                        assetIndex[indexOf] = new FilePropertiesRemote
                        {
                            N = asset.LocalName,
                            RN = asset.RemoteURL,
                            CRC = hash,
                            FT = ConvertFileTypeEnum(asset.AssetType),
                            S = asset.Size,
                            IsPatchApplicable = asset.IsPatch,
                            IsHasHashMark = IsHasHashMark
                        };
                    }
                    else
                    {
                        // Convert and add the asset as FilePropertiesRemote to assetIndex
                        assetIndex.Add(new FilePropertiesRemote
                        {
                            N = asset.LocalName,
                            RN = asset.RemoteURL,
                            CRC = hash,
                            FT = ConvertFileTypeEnum(asset.AssetType),
                            S = asset.Size,
                            IsPatchApplicable = asset.IsPatch,
                            IsHasHashMark = IsHasHashMark
                        });
                        count++;
                        countSize += asset.Size;

#if DEBUG
                        LogWriteLine($"Adding {asset.LocalName} [T: {asset.AssetType}] [S: {SummarizeSizeSimple(asset.Size)} / {asset.Size} bytes]", LogType.Default, true);
#endif
                    }
                }
            }

            LogWriteLine($"Added additional {count} assets with {SummarizeSizeSimple(countSize)}/{countSize} bytes in size", LogType.Default, true);
        }

        private bool FilterCurrentAudioLangFile(SRAsset asset, string langName, out bool isHasHashMark)
        {
            // Set output value as false
            isHasHashMark = false;
            switch (asset.AssetType)
            {
                // In case if the type is SRAssetType.Audio, then do filtering
                case SRAssetType.Audio:
                    // Set isHasHashMark to true
                    isHasHashMark = true;
                    // Split the name definition from LocalName
                    string[] nameDef = asset.LocalName.Split('/');
                    // If the name definition array length > 1, then start do filtering
                    if (nameDef.Length > 1)
                    {
                        // Compare if the first name definition is equal to target langName.
                        // Also return if the file is an audio language file if it is a SFX file or not.
                        return nameDef[0] == langName || nameDef[0] == "SFX";
                    }
                    // If it's not in criteria of name definition, then return true as "normal asset"
                    return true;
                default:
                    // return true as "normal asset"
                    return true;
            }
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum the assetIndex size and assign to _progressTotalSize
            _progressTotalSize = assetIndex.Sum(x => x.S);

            // Assign the assetIndex count to _progressTotalCount
            _progressTotalCount = assetIndex.Count;
        }

        private FileType ConvertFileTypeEnum(SRAssetType assetType) => assetType switch
        {
            SRAssetType.Asb => FileType.Blocks,
            SRAssetType.Block => FileType.Blocks,
            SRAssetType.Audio => FileType.Audio,
            SRAssetType.Video => FileType.Video,
            _ => FileType.Generic
        };

        private RepairAssetType ConvertRepairAssetTypeEnum(FileType assetType) => assetType switch
        {
            FileType.Blocks => RepairAssetType.Block,
            FileType.Audio => RepairAssetType.Audio,
            FileType.Video => RepairAssetType.Video,
            _ => RepairAssetType.General
        };
        #endregion
    }
}
