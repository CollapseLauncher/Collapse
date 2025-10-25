using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.RepairManagement;
using Hi3Helper;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Http;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher;

internal partial class HonkaiRepairV2
{
    private async ValueTask RepairAssetBlockType(
        FilePropertiesRemote asset,
        CancellationToken    token)
    {
        if (!asset.IsPatchApplicable)
        {
            await RepairAssetGenericType(HttpClientAssetBundle, asset, token);
            return;
        }

        // Update repair status to the UI
        this.UpdateCurrentRepairStatus(asset);
        if (!asset.GetBlockPatchUrlProperty(out BlockOldPatchInfo? patchInfo,
                                            out string? patchUrl))
        {
            throw new InvalidOperationException("Cannot get block patch properties!");
        }

        string patchOutputPath = Path.Combine(GamePath,
                                              AssetBundleExtension.RelativePathBlockPatch,
                                              patchInfo.PatchName);
        FileInfo patchOutputFileInfo = new FileInfo(patchOutputPath)
                                      .EnsureCreationOfDirectory()
                                      .EnsureNoReadOnly()
                                      .StripAlternateDataStream();

        string oldFilePath = Path.Combine(GamePath,
                                          AssetBundleExtension.RelativePathBlock,
                                          patchInfo.OldName);
        FileInfo oldFileInfo = new FileInfo(oldFilePath)
                              .EnsureNoReadOnly()
                              .StripAlternateDataStream();

        string newFilePath = Path.Combine(GamePath,
                                          asset.N);
        FileInfo newFileInfo = new FileInfo(newFilePath)
                              .EnsureNoReadOnly()
                              .StripAlternateDataStream();

        FileInfo newFileInfoTemp = new FileInfo(newFilePath + "_temp")
                                  .EnsureNoReadOnly()
                                  .StripAlternateDataStream();

        int bufferSize = patchInfo.PatchSize.GetFileStreamBufferSize();
        long loaded = 0;

        await using (FileStream patchFileStream =
                     patchOutputFileInfo.Open(FileMode.OpenOrCreate,
                                              FileAccess.ReadWrite,
                                              FileShare.Read,
                                              bufferSize))
        {
            byte[]            hashBuffer = new byte[8];
            byte[]            buffer     = ArrayPool<byte>.Shared.Rent(bufferSize);
            MhyMurmurHash264B hasher     = new(patchInfo.PatchSize);

            try
            {
                int read;
                while ((read = await patchFileStream
                                    .ReadAtLeastAsync(buffer, buffer.Length, false, token)
                                    .ConfigureAwait(false)) > 0)
                {
                    hasher.Append(buffer[..read]);
                    UpdateProgressCounter(0, read);
                    loaded += read;
                }

                hasher.TryGetHashAndReset(hashBuffer, out _);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            if (IsBytesEqualReversible(patchInfo.PatchHash, hashBuffer))
            {
                goto StartPatch;
            }
        }

        UpdateProgressCounter(0, -loaded);
        await RunDownloadTask(patchInfo.PatchSize,
                              patchOutputFileInfo,
                              patchUrl,
                              DownloadClient.CreateInstance(HttpClientAssetBundle),
                              (read, _) => UpdateProgressCounter(0, read),
                              token);

    StartPatch:
        BinaryPatchUtility patchUtil = new();
        patchUtil.ProgressChanged += UpdatePatchProgress;

        try
        {
            int writeBufferSize = asset.S.GetFileStreamBufferSize();
            await using FileStream oldFileStream = oldFileInfo
               .Open(FileMode.Open,
                     FileAccess.Read,
                     FileShare.Read,
                     writeBufferSize);
            await using FileStream newFileStream = newFileInfoTemp
               .Open(FileMode.Create,
                     FileAccess.Write,
                     FileShare.Write,
                     writeBufferSize);

            patchUtil.Initialize(oldFileStream,
                                 () => patchOutputFileInfo
                                    .Open(FileMode.Open,
                                          FileAccess.Read,
                                          FileShare.Read,
                                          bufferSize),
                                 newFileStream,
                                 false);

            await Task.Factory
                      .StartNew(() => patchUtil.Apply(token), token)
                      .ConfigureAwait(false);

            Logger.LogWriteLine($"[HonkaiRepairV2::RepairAssetGenericType] Asset {asset.N} has been patched!",
                                LogType.Default,
                                true);
        }
        finally
        {
            patchUtil.ProgressChanged -= UpdatePatchProgress;
            this.PopBrokenAssetFromList(asset);
        }

        newFileInfo.Refresh();
        newFileInfoTemp.Refresh();
        patchOutputFileInfo.Refresh();

        oldFileInfo.TryDeleteFile();
        newFileInfoTemp.TryMoveTo(newFileInfo);
        patchOutputFileInfo.TryDeleteFile();

        return;

        void UpdatePatchProgress(object? sender, BinaryPatchProgress progress) => UpdateProgressCounter(progress.Read, 0);
    }
}