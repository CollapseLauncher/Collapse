using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.UABT;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Preset.ConfigV2Store;

namespace CollapseLauncher
{
    internal partial class HonkaiCache
    {
        private async Task<List<CacheAsset>> Fetch()
        {
            // Initialize asset index for the return
            List<CacheAsset> returnAsset = new List<CacheAsset>();

            // Use HttpClient instance on fetching
            using (Http _httpClient = new Http(true, 5, 1000, _userAgent))
            {
                // Iterate type and do fetch
                foreach (CacheAssetType type in Enum.GetValues(typeof(CacheAssetType)))
                {
                    // Skip for unused type
                    if (type == CacheAssetType.Unused) continue;

                    // uint = Count of the assets available
                    // long = Total size of the assets available
                    (int, long) count = await FetchByType(type, _httpClient, returnAsset);

                    // Write a log about the metadata
                    LogWriteLine("Cache Metadata:"
                        + $"\r\n\t\tCache Count = {count.Item1}"
                        + $"\r\n\t\tCache Size = {SummarizeSizeSimple(count.Item2)}", LogType.NoTag);

                    // Increment the Total Size and Count
                    _progressTotalCount += count.Item1;
                    _progressTotalSize += count.Item2;
                }
            }

            // Return asset index
            return returnAsset;
        }

        private async Task<(int, long)> FetchByType(CacheAssetType type, Http _httpClient, List<CacheAsset> assetIndex)
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
                _httpClient.DownloadProgress += _httpClient_FetchAssetIndexProgress;
                await _httpClient.Download(assetIndexURL, stream, null, null, _token.Token);

                // Build the asset index and return the count and size of each type
                return BuildAssetIndex(type, baseURL, xorStream, assetIndex);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                // Unsubscribe progress and dispose streams
                _httpClient.DownloadProgress -= _httpClient_FetchAssetIndexProgress;
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

            // Set the base URL
            baseURL = $"{baseURL}/";

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

        private void _httpClient_FetchAssetIndexProgress(object sender, DownloadEvent e)
        {
            // Update fetch status
            _status.IsProgressTotalIndetermined = false;
            _status.ActivityTotal = string.Format(Lang._Misc.Speed, SummarizeSizeSimple(e.Speed));

            // Update fetch progress
            _progress.ProgressTotalPercentage = e.ProgressPercentage;

            // Push status and progress update
            UpdateAll();
        }
    }
}
