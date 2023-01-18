using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    internal partial class HonkaiRepair
    {
        private async Task<List<FilePropertiesRemote>> Fetch()
        {
            // Set total activity string as "Loading Indexes..."
            _status.RepairActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressTotalIndetermined = true;
            UpdateStatus();

            using (Http _httpClient = new Http(true, 5, 1000, _userAgent))
            {
                // Fetch metadata
                Dictionary<string, string> manifestDict = await FetchMetadata(_httpClient);
                // Fetch asset index
                List<FilePropertiesRemote> assetIndex = await FetchAssetIndex(_httpClient);

                // Check for manifest. If it doesn't exist, then throw and warn the user
                if (!manifestDict.ContainsKey(_gameVersion.VersionString))
                {
                    throw new VersionNotFoundException($"Manifest for {_gamePreset.ZoneName} (version: {_gameVersion.VersionString}) doesn't exist! Please contact @neon-nyan or open an issue for this!");
                }

                // Try check XMF file and fetch it if it doesn't exist
                await FetchXMFFile(_httpClient, manifestDict[_gameVersion.VersionString]);

                return assetIndex;
            }
        }

        private async Task<Dictionary<string, string>> FetchMetadata(Http _httpClient)
        {
            // Fetch manifest dictionary
            using (MemoryStream mfs = new MemoryStream())
            {
                // Set manifest URL
                string urlManifest = string.Format(AppGameRepoIndexURLPrefix, _gamePreset.ProfileName);

                // Start downloading manifest
                _httpClient.DownloadProgress += _httpClient_FetchManifestAssetProgress;
                await _httpClient.Download(urlManifest, mfs, null, null, _token.Token);
                _httpClient.DownloadProgress -= _httpClient_FetchManifestAssetProgress;

                // Deserialize manifest
                mfs.Position = 0;
                return (Dictionary<string, string>)JsonSerializer.Deserialize(mfs, typeof(Dictionary<string, string>), D_StringString.Default);
            }
        }

        private async Task<List<FilePropertiesRemote>> FetchAssetIndex(Http _httpClient)
        {
            // Fetch asset index
            using (MemoryStream mfs = new MemoryStream())
            {
                // Set asset index URL
                string urlIndex = string.Format(AppGameRepairIndexURLPrefix, _gamePreset.ProfileName, _gameVersion.VersionString);

                // Start downloading asset index
                _httpClient.DownloadProgress += _httpClient_FetchManifestAssetProgress;
                await _httpClient.Download(urlIndex, mfs, null, null, _token.Token);
                _httpClient.DownloadProgress -= _httpClient_FetchManifestAssetProgress;

                // Deserialize asset index and return
                mfs.Position = 0;
                return (List<FilePropertiesRemote>)JsonSerializer.Deserialize(mfs, typeof(List<FilePropertiesRemote>), L_FilePropertiesRemoteContext.Default);
            }
        }

        private async Task FetchXMFFile(Http _httpClient, string _repoURL)
        {
            // Set XMF Path and check if the XMF state is valid
            string xmfPath = Path.Combine(_gamePath, "BH3_Data\\StreamingAssets\\Asb\\pc\\Blocks.xmf");
            if (XMFUtility.CheckIfXMFVersionMatches(xmfPath, _gameVersion.VersionArrayXMF)) return;

            // Set XMF URL
            string urlXMF = _repoURL + "/BH3_Data/StreamingAssets/Asb/pc/Blocks.xmf";

            // Start downloading XMF
            _httpClient.DownloadProgress += _httpClient_FetchManifestAssetProgress;
            await _httpClient.Download(urlXMF, xmfPath, true, null, null, _token.Token);
            _httpClient.DownloadProgress -= _httpClient_FetchManifestAssetProgress;
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum total size
            long blockSize = assetIndex
                .Where(x => x.BlkC != null)
                .Sum(x => x.BlkC
                    .Sum(y => y.BlockSize));
            _progressTotalSize = assetIndex.Sum(x => x.S) + blockSize;

            // Sum total count by adding AssetIndex.Count + Counts from assets with "Blocks" type.
            _progressTotalCount = assetIndex.Count + assetIndex.Where(x => x.BlkC != null).Sum(y => y.BlkC.Sum(z => z.BlockContent.Count));
        }

        private void _httpClient_FetchManifestAssetProgress(object sender, DownloadEvent e)
        {
            // Update fetch status
            _status.IsProgressPerFileIndetermined = false;
            _status.RepairActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, SummarizeSizeSimple(e.Speed));

            // Update fetch progress
            _progress.ProgressPerFilePercentage = e.ProgressPercentage;

            // Push status and progress update
            UpdateStatus();
            UpdateProgress();
        }
    }
}
