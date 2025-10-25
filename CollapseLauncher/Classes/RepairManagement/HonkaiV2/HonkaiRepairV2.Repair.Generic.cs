using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    private async ValueTask RepairAssetGenericSophonType(
        FilePropertiesRemote asset,
        CancellationToken    token)
    {
        // Update repair status to the UI
        this.UpdateCurrentRepairStatus(asset);

        string assetPath = Path.Combine(GamePath, asset.N);
        FileInfo assetFileInfo = new FileInfo(assetPath)
                                .StripAlternateDataStream()
                                .EnsureCreationOfDirectory()
                                .EnsureNoReadOnly();

        await using FileStream assetFileStream = assetFileInfo
           .Open(FileMode.Create,
                 FileAccess.Write,
                 FileShare.Write,
                 asset.S.GetFileStreamBufferSize());

        if (asset.AssociatedObject is not SophonAsset sophonAsset)
        {
            throw new
                InvalidOperationException("Invalid operation! This asset shouldn't have been here! It's not a sophon-based asset!");
        }

        // Download as Sophon asset
        await sophonAsset
           .WriteToStreamAsync(HttpClientGeneric,
                               assetFileStream,
                               readBytes => UpdateProgressCounter(readBytes, readBytes),
                               token: token);
    }

    private async ValueTask RepairAssetGenericType(
        HttpClient           downloadHttpClient,
        FilePropertiesRemote asset,
        CancellationToken    token)
    {
        // Update repair status to the UI
        this.UpdateCurrentRepairStatus(asset);

        string assetPath = Path.Combine(GamePath, asset.N);
        FileInfo assetFileInfo = new FileInfo(assetPath)
                                .StripAlternateDataStream()
                                .EnsureNoReadOnly();

        try
        {
            if (asset.FT == FileType.Unused)
            {
                if (assetFileInfo.TryDeleteFile())
                {
                    Logger.LogWriteLine($"[HonkaiRepairV2::RepairAssetGenericType] Unused asset {asset} has been deleted!",
                                        LogType.Default,
                                        true);
                }

                return;
            }

            // Use Hi3Helper.Http module to download the file.
            DownloadClient downloadClient = DownloadClient
               .CreateInstance(downloadHttpClient);

            // Perform download
            await RunDownloadTask(asset.S,
                                  assetFileInfo,
                                  asset.RN,
                                  downloadClient,
                                  ProgressRepairAssetGenericType,
                                  token);

            Logger.LogWriteLine($"[HonkaiRepairV2::RepairAssetGenericType] Asset {asset.N} has been downloaded!",
                                LogType.Default,
                                true);
        }
        finally
        {
            this.PopBrokenAssetFromList(asset);
        }
    }

    // Note for future me @neon-nyan:
    // This is intended that we ignore DownloadProgress for now as the download size for "per-file" progress
    // is now being handled by this own class progress counter.
    private void ProgressRepairAssetGenericType(int read, DownloadProgress progress) => UpdateProgressCounter(read, read);
}
