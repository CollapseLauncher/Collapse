using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
#pragma warning disable IDE0130
namespace CollapseLauncher.RepairManagement;

internal static class RepairSharedUtility
{
    public static async Task FetchAssetsFromSophonAsync(
        this ProgressBase          instance,
        HttpClient                 client,
        List<FilePropertiesRemote> assetIndex,
        Func<string, FileType>     assetTypeDeterminer,
        GameVersion                gameVersion,
        string[]                   excludeMatchingFieldList,
        CancellationToken          token = default)
    {
        // Set total activity string as "Fetching Caches Type: <type>"
        instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, "Sophon");
        instance.Status.IsProgressAllIndetermined = true;
        instance.Status.IsIncludePerFileIndicator = false;
        instance.UpdateStatus();

        PresetConfig gamePreset    = instance.GameVersionManager.GamePreset;
        string?      sophonApiUrl  = gamePreset.LauncherResourceChunksURL?.MainUrl;
        string?      matchingField = gamePreset.LauncherResourceChunksURL?.MainBranchMatchingField;

        if (sophonApiUrl == null)
        {
            throw new NullReferenceException("Sophon API URL inside of the metadata is null. Try to clear your metadata by going to \"App Settings\" > \"Clear Metadata and Restart\"");
        }

        string versionApiToUse = gameVersion.ToString();
        sophonApiUrl += $"&tag={versionApiToUse}";

        SophonChunkManifestInfoPair infoPair = await SophonManifest
           .CreateSophonChunkManifestInfoPair(client,
                                              sophonApiUrl,
                                              matchingField,
                                              false,
                                              token);

        if (!infoPair.IsFound)
        {
            throw new InvalidOperationException($"Sophon cannot find matching field: {matchingField} from API URL: {sophonApiUrl}");
        }

        SearchValues<string> excludedMatchingFields = SearchValues.Create(excludeMatchingFieldList, StringComparison.OrdinalIgnoreCase);
        List<SophonChunkManifestInfoPair> infoPairs = [infoPair];
        infoPairs.AddRange(infoPair
                          .OtherSophonBuildData?
                          .ManifestIdentityList
                          .Where(x => !x.MatchingField
                                        .ContainsAny(excludedMatchingFields) && !x.MatchingField.Equals(matchingField))
                          .Select(x => infoPair.GetOtherManifestInfoPair(x.MatchingField)) ?? []);


        foreach (SophonChunkManifestInfoPair pair in infoPairs)
        {
            instance.Status.ActivityStatus = string.Format(Locale.Lang._CachesPage.CachesStatusFetchingType, $"Sophon ({pair.MatchingField})");
            instance.Status.IsProgressAllIndetermined = true;
            instance.Status.IsIncludePerFileIndicator = false;
            instance.UpdateStatus();

            await foreach (SophonAsset asset in SophonManifest
                              .EnumerateAsync(client,
                                              pair,
                                              token: token))
            {
                assetIndex.Add(new FilePropertiesRemote
                {
                    AssociatedObject = asset,
                    S = asset.AssetSize,
                    N = asset.AssetName.NormalizePath(),
                    CRC = asset.AssetHash,
                    FT = assetTypeDeterminer(asset.AssetName)
                });
            }
        }
    }

}
