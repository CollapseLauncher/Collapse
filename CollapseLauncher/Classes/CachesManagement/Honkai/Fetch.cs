using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Shared.Region.Honkai;
using Hi3Helper.UABT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                await BuildGameRepoURL(_httpClient, token);

                // Iterate type and do fetch
                foreach (CacheAssetType type in Enum.GetValues(typeof(CacheAssetType)))
                {
                    // Skip for unused type
                    switch (type)
                    {
                        case CacheAssetType.Unused:
                        case CacheAssetType.Dispatcher:
                        case CacheAssetType.Gateway:
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

        private async Task BuildGameRepoURL(Http _httpClient, CancellationToken token)
        {
            // Fetch dispatcher
            Dispatcher dispatcher = null;
            Exception lastException = null;

            // Try fetch disppatcher by iterating the base URL
            foreach (string baseURL in _gamePreset.GameDispatchArrayURL)
            {
                try
                {
                    // Try assign dispatcher
                    dispatcher = await FetchDispatcher(_httpClient, BuildDispatcherURL(baseURL), token);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                // If no exception being thrown, then break
                if (lastException == null)
                {
                    break;
                }
            }

            // If last exception wasn't null, then throw
            if (lastException != null) throw lastException;

            // Get gatewayURl and fetch the gateway
            string gatewayURL = GetPreferredGatewayURL(dispatcher, _gamePreset.GameGatewayDefault);
            _gameGateway = await FetchGateway(_httpClient, gatewayURL, token);

            // Set the Game Repo URL
            _gameRepoURL = BuildAssetBundleURL(_gameGateway);
        }

        private async Task<Dispatcher> FetchDispatcher(Http _httpClient, string baseURL, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: Dispatcher"
            _status.ActivityStatus = string.Format(Lang._CachesPage.CachesStatusFetchingType, CacheAssetType.Dispatcher);
            _status.IsProgressTotalIndetermined = true;
            _status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            // Get the dispatcher properties
            MemoryStream stream = new MemoryStream();

            try
            {
                // Start downloading the dispatcher
                await _httpClient.Download(baseURL, stream, null, null, token);
                stream.Position = 0;

                LogWriteLine($"Cache Update connected to dispatcher endpoint: {baseURL}", LogType.Default, true);

                // Try deserialize dispatcher
                return (Dispatcher)JsonSerializer.Deserialize(stream, typeof(Dispatcher), DispatcherContext.Default);
            }
            finally
            {
                // Dispose the stream
                stream.Dispose();
            }
        }

        private async Task<Gateway> FetchGateway(Http _httpClient, string baseURL, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: Gateway"
            _status.ActivityStatus = string.Format(Lang._CachesPage.CachesStatusFetchingType, CacheAssetType.Gateway);
            _status.IsProgressTotalIndetermined = true;
            _status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            // Get the gateway properties
            MemoryStream stream = new MemoryStream();

            try
            {
                // Start downloading the gateway
                await _httpClient.Download(baseURL, stream, null, null, token);
                stream.Position = 0;

                LogWriteLine($"Cache Update connected to gateway endpoint: {baseURL}", LogType.Default, true);

                // Try deserialize gateway
                return (Gateway)JsonSerializer.Deserialize(stream, typeof(Gateway), GatewayContext.Default);
            }
            finally
            {
                // Dispose the stream
                stream.Dispose();
            }
        }

        private string GetPreferredGatewayURL(Dispatcher dispatcher, string preferredGateway)
        {
            // If preferredGateway == null, then return the first dispatch_url
            if (preferredGateway == null)
            {
                return BuildGatewayURL(dispatcher.region_list[0].dispatch_url);
            }

            // Find the preferred region_list and return the dispatcher_url if found or null if doesn't
            return BuildGatewayURL(dispatcher.region_list.Where(x => x.name == preferredGateway).FirstOrDefault().dispatch_url);
        }

        private string BuildAssetBundleURL(Gateway gateway) => gateway.asset_bundle_url_list[0] + "/{0}/editor_compressed/";

        private string BuildDispatcherURL(string baseDispatcherURL)
        {
            // Format the Dispatcher URL based on template
            long curTime = (int)Math.Truncate(DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
            return string.Format($"{baseDispatcherURL}{_gamePreset.GameDispatchURLTemplate}", _gameVersion.VersionString, _gamePreset.GameDispatchChannelName, curTime);
        }

        private string BuildGatewayURL(string baseGatewayURL)
        {
            // Format the Gateway URL based on template
            long curTime = (int)Math.Truncate(DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds);
            return string.Format($"{baseGatewayURL}{_gamePreset.GameGatewayURLTemplate}", _gameVersion.VersionString, _gamePreset.GameDispatchChannelName, curTime);
        }

        private async Task<(int, long)> FetchByType(CacheAssetType type, Http _httpClient, List<CacheAsset> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: <type>"
            _status.ActivityStatus = string.Format(Lang._CachesPage.CachesStatusFetchingType, type);
            _status.IsProgressTotalIndetermined = true;
            _status.IsIncludePerFileIndicator = false;
            UpdateStatus();

            // Get the asset index properties
            string baseURL = string.Format(_gameRepoURL, type.ToString().ToLowerInvariant());
            string assetIndexURL = string.Format(baseURL + "{0}Version.unity3d", type == CacheAssetType.Data ? "Data" : "Resource");
            MemoryStream stream = new MemoryStream();
            XORStream xorStream = new XORStream(stream);

            try
            {
                // Start downloading the asset index
                await _httpClient.Download(assetIndexURL, stream, null, null, token);

                // Build the asset index and return the count and size of each type
                return BuildAssetIndex(type, baseURL, xorStream, assetIndex);
            }
            finally
            {
                // Dispose the streams
                stream.Dispose();
                xorStream.Dispose();
            }
        }

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
            await BuildGameRepoURL(_httpClient, token);

            // Fetch the progress
            _ = await FetchByType(type, _httpClient, returnAsset, token);

            // Return the list and base asset bundle repo URL
            return (returnAsset, _gameGateway.ex_resource_url_list.FirstOrDefault(), BuildAssetBundleURL(_gameGateway));
        }
    }
}
