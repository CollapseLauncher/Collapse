using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool;
using Hi3Helper.EncTool.Parser;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.EncTool.Parser.KianaDispatch;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.HonkaiRepairV2;
// ReSharper disable CheckNamespace
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.RepairManagement;

[JsonSerializable(typeof(List<BlockPatchInfo>))]
public partial class BlockPatchManifestContext : JsonSerializerContext;

internal static partial class AssetBundleExtension
{
    internal const string RelativePathBlock      = @"BH3_Data\StreamingAssets\Asb\pc\";
    internal const string RelativePathBlockPatch = RelativePathBlock + @"Patch\";

    internal static async Task<List<FilePropertiesRemote>>
        GetBlockAssetListAsync<T>(
            this HttpClient    assetBundleHttpClient,
            PresetConfig       presetConfig,
            GameVersion        gameVersion,
            string             gameDir,
            KianaDispatch      gameServerInfo,
            SenadinaFileResult senadinaResults,
            ProgressBase<T>?   progressibleInstance = null,
            CancellationToken  token                = default)
        where T : IAssetIndexSummary
    {
        // Update Progress
        if (progressibleInstance != null)
        {
            progressibleInstance.Status.ActivityStatus =
                string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Block Files");
            progressibleInstance.Status.IsProgressAllIndetermined = true;
            progressibleInstance.Status.IsIncludePerFileIndicator = false;
            progressibleInstance.UpdateStatus();
        }
        bool isUseHttpRepairOverride = LauncherConfig.GetAppConfigValue("EnableHTTPRepairOverride");

        await using Stream xmfMetaCurrentFileStream  = senadinaResults.XmfMeta?.fileStream ?? throw new NullReferenceException("Senadina BlockMeta Identifier Stream cannot be null!");
        await using Stream xmfPatchCurrentFileStream = senadinaResults.XmfPatch?.fileStream ?? throw new NullReferenceException("Senadina BlockPatch Identifier Stream cannot be null!");

        XMFParser          xmfMetaParser  = new(string.Empty, xmfMetaCurrentFileStream, true);
        BlockPatchManifest xmfPatchParser = new(xmfPatchCurrentFileStream);

        List<FilePropertiesRemote> assetList            = [];
        string                     baseUrlVersionPrefix = string.Join('_', gameVersion.VersionArray);

        // Try to get PatchInfo based on last verified version.
        string                             gameAsbBaseDir = Path.Combine(gameDir, RelativePathBlock);
        Dictionary<string, BlockPatchInfo> patchInfos     = new(StringComparer.OrdinalIgnoreCase);
        foreach (BlockPatchInfo patchInfo in xmfPatchParser.PatchAsset)
        {
            // Skip if the last patch file has already existed.
            string newFilePath = Path.Combine(gameAsbBaseDir, patchInfo.NewName);
            if (File.Exists(newFilePath))
            {
                continue;
            }

            foreach (BlockOldPatchInfo? patchPair in patchInfo.PatchPairs)
            {
                if (patchPair == null)
                {
                    continue;
                }

                string oldFilePath = Path.Combine(gameAsbBaseDir, patchPair.OldName);
                if (!File.Exists(oldFilePath))
                {
                    continue;
                }

                List<BlockOldPatchInfo> newPair = [patchPair];
                patchInfo.PatchPairs = newPair;
                patchInfos.Add(patchInfo.NewName, patchInfo);
                break;
            }
        }

        foreach (XMFBlock xmfBlock in xmfMetaParser.BlockEntry)
        {
            ref BlockPatchInfo patchInfoRef =
                ref CollectionsMarshal.GetValueRefOrNullRef(patchInfos, xmfBlock.BlockName);

            FilePropertiesRemote asset = new()
            {
                AssociatedObject = xmfBlock,
                CRC = Path.GetFileNameWithoutExtension(xmfBlock.BlockName),
                FT  = FileType.Block,
                RN = GetRandomBaseUrl(gameServerInfo)
                   .CombineURLFromString($"StreamingAsb/{baseUrlVersionPrefix}/pc/HD/asb", xmfBlock.BlockName),
                N = xmfBlock.BlockName,
                S = xmfBlock.Size
            };
            assetList.Add(asset);

            if (Unsafe.IsNullRef(ref patchInfoRef))
            {
                continue;
            }

            asset.IsPatchApplicable = true;
            asset.BlockPatchInfo    = patchInfoRef;
        }

        return assetList;

        string GetRandomBaseUrl(KianaDispatch kianaDispatch)
        {
            string selectedUrl = kianaDispatch.ExternalAssetUrls.RandomSelectSingle();
            return isUseHttpRepairOverride ? "http://" : "https://" + selectedUrl;
        }
    }
}
