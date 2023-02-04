using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher
{
    internal partial class GenshinRepair
    {
        private async Task<FilePropertiesRemote[]> FetchCacheAssetIndex()
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressTotalIndetermined = true;
            UpdateStatus();

            // Fetch manifest dictionary
            using (Http _httpClient = new Http(true, 5, 1000, _userAgent))
            {
                Dictionary<string, string> manifestDict;

                using (MemoryStream mfs = new MemoryStream())
                {
                    // Set manifest URL
                    string urlManifest = string.Format(AppGameRepoIndexURLPrefix, PageStatics._GameVersion.GamePreset.ProfileName);

                    // Start downloading manifest
                    _httpClient.DownloadProgress += _httpClient_FetchManifestAssetProgress;
                    await _httpClient.Download(urlManifest, mfs, null, null, _token.Token);
                    _httpClient.DownloadProgress -= _httpClient_FetchManifestAssetProgress;

                    // Deserialize manifest
                    mfs.Position = 0;
                    manifestDict = (Dictionary<string, string>)JsonSerializer.Deserialize(mfs, typeof(Dictionary<string, string>), D_StringString.Default);
                }

                using (MemoryStream mfs = new MemoryStream())
                {
                    // Set asset index URL
                    string urlIndex = string.Format(AppGameRepairIndexURLPrefix, PageStatics._GameVersion.GamePreset.ProfileName, _gameVersion.VersionString);

                    // Start downloading asset index
                    _httpClient.DownloadProgress += _httpClient_FetchManifestAssetProgress;
                    await _httpClient.Download(urlIndex, mfs, null, null, _token.Token);
                    _httpClient.DownloadProgress -= _httpClient_FetchManifestAssetProgress;

                    // Deserialize manifest
                    mfs.Position = 0;

                    // Deserialize data and return
                    return (FilePropertiesRemote[])JsonSerializer.Deserialize(mfs, typeof(FilePropertiesRemote[]), Array_FilePropertiesRemoteContext.Default);
                }
            }
        }

        private void _httpClient_FetchManifestAssetProgress(object sender, DownloadEvent e)
        {
            _status.IsProgressPerFileIndetermined = false;
            _status.ActivityPerFile = string.Format(Lang._GameRepairPage.PerProgressSubtitle3, SummarizeSizeSimple(e.Speed));

            _progress.ProgressPerFilePercentage = e.ProgressPercentage;

            UpdateStatus();
            UpdateProgress();
        }
    }
}
