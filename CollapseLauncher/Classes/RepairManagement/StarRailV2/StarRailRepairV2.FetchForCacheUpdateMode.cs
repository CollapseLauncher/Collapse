using CollapseLauncher.Helper.Metadata;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher;

internal partial class StarRailRepairV2
{

    #region CacheUpdateManifest

    private async Task FetchForCacheUpdateMode(HttpClient                 client,
                                               string                     regionId,
                                               List<FilePropertiesRemote> assetIndex,
                                               CancellationToken          token)
    {
        // -- Fetch game dispatcher/gateway
        PresetConfig gamePreset = GameVersionManager.GamePreset;
        SRDispatcherInfo dispatcherInfo = new(gamePreset.GameDispatchArrayURL,
                                              gamePreset.ProtoDispatchKey,
                                              gamePreset.GameDispatchURLTemplate,
                                              gamePreset.GameGatewayURLTemplate,
                                              gamePreset.GameDispatchChannelName,
                                              GameVersionManager.GetGameVersionApi().ToString());
        await dispatcherInfo.Initialize(client, regionId, token);

        StarRailPersistentRefResult persistentRefResult = await StarRailPersistentRefResult
           .GetCacheReferenceAsync(this,
                                    dispatcherInfo,
                                    client,
                                    GamePath,
                                    GameDataPersistentPathRelative,
                                    token);

        List<FilePropertiesRemote> sophonAssets = [];
        await GetPrimaryManifest(sophonAssets, [], token); // Just to get the sophon asset for stock LuaV

        await persistentRefResult.FinalizeCacheFetchAsync(this,
                                                          client,
                                                          sophonAssets,
                                                          GamePath,
                                                          persistentRefResult.BaseDirs.CacheLua!,
                                                          token);

        // HACK: Duplicate List from Sophon so we know which one is being added
        List<FilePropertiesRemote> sophonAssetsDup = new(sophonAssets);
        assetIndex.AddRange(persistentRefResult.GetPersistentFiles(sophonAssetsDup, GamePath, []));
        assetIndex.AddRange(sophonAssetsDup[sophonAssets.Count..]);
    }

    #endregion
}
