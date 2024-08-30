using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Fetch(CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = [];

            // Initialize new proxy-aware HttpClient
            using HttpClient httpClientNew = new HttpClientBuilder()
                .UseLauncherConfig(_downloadThreadCount + _downloadThreadCountReserved)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use a new DownloadClient for fetching
            DownloadClient downloadClient = DownloadClient.CreateInstance(httpClientNew);

            // Build _gameRepoURL from loading Dispatcher and Gateway
            await BuildGameRepoURL(downloadClient, token);

            // Iterate type and do fetch
            foreach (CacheAssetType type in Enum.GetValues<CacheAssetType>())
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
                (int, long) count = await FetchByType(type, downloadClient, returnAsset, token);

                // Write a log about the metadata
                LogWriteLine($"Cache Metadata [T: {type}]:", LogType.Default, true);
                LogWriteLine($"    Cache Count = {count.Item1}", LogType.NoTag, true);
                LogWriteLine($"    Cache Size = {SummarizeSizeSimple(count.Item2)}", LogType.NoTag, true);

                // Increment the Total Size and Count
                _progressAllCountTotal += count.Item1;
                _progressAllSizeTotal += count.Item2;
            }

            // Return asset index
            return returnAsset;
        }

        private async Task BuildGameRepoURL(DownloadClient downloadClient, CancellationToken token)
        {
            KianaDispatch dispatch = null;
            Exception lastException = null;

            foreach (string baseURL in _gameVersionManager!.GamePreset!.GameDispatchArrayURL!)
            {
                try
                {
                    // Init the key and decrypt it if existed.
                    if (string.IsNullOrEmpty(_gameVersionManager.GamePreset.DispatcherKey))
                    {
                        throw new NullReferenceException("Dispatcher key is null or empty!");
                    }

                    string key = _gameVersionManager.GamePreset.DispatcherKey;

                    // Try assign dispatcher
                    dispatch = await KianaDispatch.GetDispatch(downloadClient, baseURL,
                                                               _gameVersionManager.GamePreset.GameDispatchURLTemplate,
                                                               _gameVersionManager.GamePreset.GameDispatchChannelName,
                                                               key, _gameVersion.VersionArray, token);
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
            _gameGateway =
                await KianaDispatch.GetGameserver(downloadClient, dispatch!, _gameVersionManager.GamePreset.GameGatewayDefault!, token);
            _gameRepoURL = BuildAssetBundleURL(_gameGateway);
        }

        private static string BuildAssetBundleURL(KianaDispatch gateway) => CombineURLFromString(gateway!.AssetBundleUrls![0], "/{0}/editor_compressed/");

        private async Task<(int, long)> FetchByType(CacheAssetType type, DownloadClient downloadClient, List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: <type>"
            _status!.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusFetchingType!, type);
            _status.IsProgressAllIndetermined = true;
            _status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            // Get the asset index properties
            string baseURL = string.Format(_gameRepoURL!, type.ToString().ToLowerInvariant());
            string assetIndexURL = string.Format(CombineURLFromString(baseURL, "{0}Version.unity3d")!,
                                                 type == CacheAssetType.Data ? "Data" : "Resource");

#if DEBUG
            LogWriteLine($"Fetching data cache on: {assetIndexURL}", LogType.Debug, true);
#endif

            // Get a direct HTTP Stream
            await using HttpResponseInputStream remoteStream = await HttpResponseInputStream.CreateStreamAsync(
                downloadClient.GetHttpClient(), assetIndexURL, null, null, null, null, null, token);

            await using XORStream stream = new XORStream(remoteStream);

            // Build the asset index and return the count and size of each type
            (int, long) returnValue = await BuildAssetIndex(type, baseURL, stream, assetIndex, token);

            return returnValue;
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

        private IEnumerable<CacheAsset> EnumerateCacheTextAsset(CacheAssetType type, IEnumerable<string> enumerator, string baseUrl)
        {
            // Set isFirst flag as true if type is Data and
            // also convert type as lowered string.
            bool isFirst = type == CacheAssetType.Data;
            bool isNeedReadLuckyNumber = type == CacheAssetType.Data;

            foreach (string line in enumerator)
            {
                // If the line is empty, then next to other line.
                if (line.Length < 1) continue;

                // If isFirst flag set to true, then get the _gameSalt.
                if (isFirst)
                {
                    _gameSalt = GetAssetIndexSalt(line);
                    isFirst = false;
                    continue;
                }

                // Get the lucky number if it does so 👀
                if (isNeedReadLuckyNumber && int.TryParse(line, null, out int luckyNumber))
                {
                    _luckyNumber = luckyNumber;
                    isNeedReadLuckyNumber = false;
                    continue;
                }

                // If the line is not started with '{' and ended with '}' (JSON),
                // then skip it.
                if (line[0] != '{' && line[^1] != '}')
                {
                    LogWriteLine($"Skipping non-JSON line in [T: {type}]:\r\n{line}", LogType.Warning, true);
                    continue;
                }

                CacheAsset content;
                try
                {
                    // Deserialize the line and set the type
                    content = line.Deserialize<CacheAsset>(InternalAppJSONContext.Default);
                }
                catch (Exception ex)
                {
                    // If failed while parsing the file, then skip it.
                    LogWriteLine($"Failed while parsing a line in [T: {type}]:\r\n{line}\r\nReason: {ex}",
                                 LogType.Warning, true);
                    continue;
                }

                // If the content is null, then continue
                if (content == null) continue;

                // Check if the asset is regional and contains only selected language.
                if (IsValidRegionFile(content.N, _gameLang))
                {
                    // Set base URL and Path and add it to asset index
                    content.BaseURL = baseUrl;
                    content.DataType = type;
                    content.BasePath = GetAssetBasePathByType(type);

                    yield return content;
                }
            }
        }

        private async ValueTask<(int, long)> BuildAssetIndex(CacheAssetType type, string baseURL, Stream stream,
                                            List<CacheAsset> assetIndex, CancellationToken token)
        {
            int count = 0;
            long size = 0;

            // Set isFirst flag as true if type is Data and
            // also convert type as lowered string.
            
            // Unused as of Aug 4th 2024, bonk @bagusnl if not true
            // bool isFirst = type == CacheAssetType.Data;
            // bool isNeedReadLuckyNumber = type == CacheAssetType.Data;

            // Parse asset index file from UABT
            BundleFile bundleFile = new BundleFile(stream);
            SerializedFile serializeFile = new SerializedFile(bundleFile.fileList!.FirstOrDefault()!.stream);

            // Try to get the asset index file as byte[] and load it as TextAsset
            byte[] dataRaw = serializeFile.GetDataFirstOrDefaultByName("packageversion.txt");
            TextAsset dataTextAsset = new TextAsset(dataRaw);

            // Initialize local HTTP client
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig(_downloadThreadCount + _downloadThreadCountReserved)
                .SetUserAgent(_userAgent)
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Iterate lines of the TextAsset in parallel
            await Parallel.ForEachAsync(EnumerateCacheTextAsset(type, dataTextAsset.GetStringList(), baseURL),
                new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = _threadCount
                },
                async (content, cancellationToken) =>
                {
                    // Perform URL check if DLM is dynamic type (DLM == 2)
                    if (content.DLM == 2)
                    {
                        // Update the status
                        _status!.ActivityStatus = string.Format(Lang._CachesPage.Status2, type, content.N);
                        _status!.IsProgressAllIndetermined = true;
                        _status!.IsProgressPerFileIndetermined = true;
                        UpdateStatus();

                        // Check for the URL availability and is not available, then skip.
                        var urlStatus = await client.GetURLStatusCode(content.ConcatURL, cancellationToken);
                        LogWriteLine($"The Cache {type} asset: {content.N} " + (urlStatus.IsSuccessStatusCode ? "is" : "is not") + $" available (Status code: {urlStatus.StatusCode})", LogType.Default, true);

                        if (!urlStatus.IsSuccessStatusCode) return;
                    }

                    assetIndex.Add(content);
                });

            // Return the count and the size
            return (count, size);
        }

        private byte[] GetAssetIndexSalt(string data)
        {
            // Get the salt from the string and return as byte[]
            byte[] key;
            if (DataCooker.IsServeV3Data(LauncherMetadataHelper.CurrentMasterKey?.Key))
            {
                DataCooker.GetServeV3DataSize(LauncherMetadataHelper.CurrentMasterKey?.Key, out long keyCompSize,
                                              out long keyDecompSize);
                key = new byte[keyCompSize];
                DataCooker.ServeV3Data(LauncherMetadataHelper.CurrentMasterKey?.Key, key, (int)keyCompSize,
                                       (int)keyDecompSize, out _);
            }
            else
            {
                key = LauncherMetadataHelper.CurrentMasterKey?.Key;
            }

            mhyEncTool saltTool = new mhyEncTool(data, key);
            return saltTool.GetSalt();
        }

        private string GetAssetBasePathByType(CacheAssetType type) => Path.Combine(_gamePath!, type == CacheAssetType.Data ? "Data" : "Resources");

        private bool IsValidRegionFile(string input, string lang)
        {
            // If the path contains regional string, then move to the next check
            if (input!.Contains(_cacheRegionalCheckName!))
            {
                // Check if the regional string has specified language string
                return input.Contains($"{_cacheRegionalCheckName}_{lang}");
            }

            // If none, then pass it as true (non-regional string)
            return true;
        }

        public KianaDispatch GetCurrentGateway() => _gameGateway;

        public async Task<(List<CacheAsset>, string, string, int)> GetCacheAssetList(
            DownloadClient downloadClient, CacheAssetType type, CancellationToken token)
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = new();

            // Build _gameRepoURL from loading Dispatcher and Gateway
            await BuildGameRepoURL(downloadClient, token);

            // Fetch the progress
            _ = await FetchByType(type, downloadClient, returnAsset, token);

            // Return the list and base asset bundle repo URL
            return (returnAsset, _gameGateway!.ExternalAssetUrls!.FirstOrDefault(), BuildAssetBundleURL(_gameGateway),
                    _luckyNumber);
        }
    }
}