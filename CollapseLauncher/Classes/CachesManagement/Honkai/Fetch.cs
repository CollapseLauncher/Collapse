using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Http;
using Hi3Helper.UABT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Fetch(CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = new List<CacheAsset>();

            // Use HttpClient instance on fetching
            Http _httpClient = new Http(true, 5, 1000, _userAgent);
            try
            {
                // Subscribe the event listener
                _httpClient.DownloadProgress += _httpClient_FetchAssetProgress;

                // Build _gameRepoURL from loading Dispatcher and Gateway
                await BuildGameRepoURL(token);

                // Iterate type and do fetch
                foreach (CacheAssetType type in Enum.GetValues(typeof(CacheAssetType)))
                {
                    // Skip for unused type
                    switch (type)
                    {
                        case CacheAssetType.Unused:
                        case CacheAssetType.Dispatcher:
                        case CacheAssetType.Gateway:
                        case CacheAssetType.General:
                        case CacheAssetType.IFix:
                        case CacheAssetType.DesignData:
                        case CacheAssetType.Lua:
                            continue;
                    }

                    // uint = Count of the assets available
                    // long = Total size of the assets available
                    (int, long) count = await FetchByType(type, _httpClient, returnAsset, token);

                    // Write a log about the metadata
                    LogWriteLine($"Cache Metadata [T: {type}]:", LogType.Default, true);
                    LogWriteLine($"    Cache Count = {count.Item1}", LogType.NoTag, true);
                    LogWriteLine($"    Cache Size = {SummarizeSizeSimple(count.Item2)}", LogType.NoTag, true);

                    // Increment the Total Size and Count
                    _progressTotalCount += count.Item1;
                    _progressTotalSize += count.Item2;
                }
            }
            finally
            {
                // Unsubscribe the event listener and dispose Http client
                _httpClient.DownloadProgress -= _httpClient_FetchAssetProgress;
                _httpClient.Dispose();
            }

            // Return asset index
            return returnAsset;
        }

        private async Task BuildGameRepoURL(CancellationToken token)
        {
            KianaDispatch dispatch = null;
            Exception lastException = null;

            foreach (string baseURL in _gamePreset.GameDispatchArrayURL)
            {
                try
                {
                    // Init the key and decrypt it if exist.
                    string key = null;
                    if (_gamePreset.DispatcherKey != null)
                    {
                        mhyEncTool Decryptor = new mhyEncTool();
                        Decryptor.InitMasterKey(ConfigV2.MasterKey, ConfigV2.MasterKeyBitLength, RSAEncryptionPadding.Pkcs1);

                        key = _gamePreset.DispatcherKey;
                        Decryptor.DecryptStringWithMasterKey(ref key);
                    }

                    // Try assign dispatcher
                    dispatch = await KianaDispatch.GetDispatch(baseURL, _gamePreset.GameDispatchURLTemplate, _gamePreset.GameDispatchChannelName, key, _gameVersion.VersionArray, token);
                    lastException = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }
            }

            // If last exception wasn't null, then throw
            if (lastException != null) throw lastException;

            // Get gatewayURl and fetch the gateway
            _gameGateway = await KianaDispatch.GetGameserver(dispatch, _gamePreset.GameGatewayDefault, token);
            _gameRepoURL = BuildAssetBundleURL(_gameGateway);
        }

        private string BuildAssetBundleURL(KianaDispatch gateway) => CombineURLFromString(gateway.AssetBundleUrls[0], "/{0}/editor_compressed/");

        private async Task<(int, long)> FetchByType(CacheAssetType type, Http _httpClient, List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: <type>"
            _status.ActivityStatus = string.Format(Lang._CachesPage.CachesStatusFetchingType, type);
            _status.IsProgressTotalIndetermined = true;
            _status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            // Get the asset index properties
            string baseURL = string.Format(_gameRepoURL, type.ToString().ToLowerInvariant());
            string assetIndexURL = string.Format(CombineURLFromString(baseURL, "{0}Version.unity3d"), type == CacheAssetType.Data ? "Data" : "Resource");
            MemoryStream stream = new MemoryStream();
            XORStream xorStream = new XORStream(stream);

            try
            {
                // Start downloading the asset index
                await _httpClient.Download(assetIndexURL, stream, null, null, token);

                // Build the asset index and return the count and size of each type
                (int, long) returnValue = BuildAssetIndex(type, baseURL, xorStream, assetIndex);

                return returnValue;
            }
            catch { throw; }
            finally
            {
                // Dispose the streams
                stream.Dispose();
                xorStream.Dispose();
            }
        }

        /*
        private void BuildDataPatchConfig(MemoryStream stream, List<CacheAsset> assetIndex)
        {
            // Reset position
            stream.Position = 0;

            // Initialize manifest file
            CachePatchManifest manifest = new CachePatchManifest(stream, true);

            for (int i = 0; i < assetIndex.Count; i++)
            {
                if (assetIndex[i].DataType == CacheAssetType.Data)
                {
                    CachePatchInfo info = manifest.PatchAsset.Where(x => IsArrayMatch(x.NewHashSHA1, assetIndex[i].CRCArray)).FirstOrDefault();
                }
            }
        }
        */

        private (int, long) BuildAssetIndex(CacheAssetType type, string baseURL, XORStream stream, List<CacheAsset> assetIndex)
        {
            int count = 0;
            long size = 0;

            // Set isFirst flag as true if type is Data and
            // also convert type as lowered string.
            bool isFirst = type == CacheAssetType.Data;

            // Parse asset index file from UABT
            stream.Position = 0;
            BundleFile bundleFile = new BundleFile(stream);
            SerializedFile serializeFile = new SerializedFile(bundleFile.fileList.FirstOrDefault().stream);

            // Try get the asset index file as byte[] and load it as TextAsset
            byte[] dataRaw = serializeFile.GetDataFirstOrDefaultByName("packageversion.txt");
            TextAsset dataTextAsset = new TextAsset(dataRaw);


            // Iterate lines of the TextAsset
            foreach (ReadOnlySpan<char> line in dataTextAsset.GetStringEnumeration())
            {
                // If the line is empty, then next to other line.
                if (line.Length < 1) continue;

                // If isFirst flag set to true, then get the _gameSalt.
                if (isFirst)
                {
                    _gameSalt = GetAssetIndexSalt(line.ToString());
                    isFirst = false;
                    continue;
                }

                // Deserialize the line and set the type
                CacheAsset content = (CacheAsset)JsonSerializer.Deserialize(line, typeof(CacheAsset), CacheAssetContext.Default);
                content.DataType = type;

                // Check if the asset is regional and contains only selected language.
                if (IsValidRegionFile(content.N, _gameLang))
                {
                    // If valid, then add the asset to assetIndex
                    count++;
                    size += content.CS;

                    // Set base URL and Path and add it to asset index
                    content.BaseURL = baseURL;
                    content.BasePath = GetAssetBasePathByType(type);
                    assetIndex.Add(content);
                }
            }

            // Return the count and the size
            return (count, size);
        }

        private byte[] GetAssetIndexSalt(string data)
        {
            // Get the salt from the string and return as byte[]
            mhyEncTool saltTool = new mhyEncTool(data, ConfigV2.MasterKey);
            return saltTool.GetSalt();
        }

        private string GetAssetBasePathByType(CacheAssetType type) => Path.Combine(_gamePath, type == CacheAssetType.Data ? "Data" : "Resources");

        private bool IsValidRegionFile(string input, string lang)
        {
            // If the path contains regional string, then move to the next check
            if (input.Contains(_cacheRegionalCheckName))
            {
                // Check if the regional string has specified language string
                return input.Contains($"{_cacheRegionalCheckName}_{lang}");
            }

            // If none, then pass it as true (non-regional string)
            return true;
        }

        public async Task<(List<CacheAsset>, string, string)> GetCacheAssetList(Http _httpClient, CacheAssetType type, CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = new List<CacheAsset>();

            // Build _gameRepoURL from loading Dispatcher and Gateway
            await BuildGameRepoURL(token);

            // Fetch the progress
            _ = await FetchByType(type, _httpClient, returnAsset, token);

            // Return the list and base asset bundle repo URL
            return (returnAsset, _gameGateway.ExternalAssetUrls.FirstOrDefault(), BuildAssetBundleURL(_gameGateway));
        }
    }
}
