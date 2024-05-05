﻿using Hi3Helper;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class StarRailCache
    {
        private async Task<List<SRAsset>> Fetch(CancellationToken token)
        {
            // Initialize asset index for the return
            List<SRAsset> returnAsset = new List<SRAsset>();

            try
            {
                // Subscribe the event listener
                _innerGameVersionManager!.StarRailMetadataTool!.HttpEvent += _httpClient_FetchAssetProgress;

                // Initialize metadata
                // Set total activity string as "Fetching Caches Type: Dispatcher"
                _status!.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusFetchingType!, CacheAssetType.Dispatcher);
                _status!.IsProgressTotalIndetermined = true;
                _status!.IsIncludePerFileIndicator = false;
                UpdateStatus();

                if (!await _innerGameVersionManager!.StarRailMetadataTool.Initialize(token, GetExistingGameRegionID(), Path.Combine(_gamePath!, $"{Path.GetFileNameWithoutExtension(_gameVersionManager!.GamePreset!.GameExecutableName)}_Data\\Persistent")))
                    throw new InvalidDataException("The dispatcher response is invalid! Please open an issue to our GitHub page to report this issue.");

                // Iterate type and do fetch
                foreach (SRAssetType type in Enum.GetValues<SRAssetType>())
                {
                    // Skip for unused type
                    switch (type)
                    {
                        case SRAssetType.Audio:
                        case SRAssetType.Video:
                        case SRAssetType.Block:
                        case SRAssetType.Asb:
                            continue;
                    }

                    // uint = Count of the assets available
                    // long = Total size of the assets available
                    (int, long) count = await FetchByType(type, returnAsset, token);

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
                _innerGameVersionManager!.StarRailMetadataTool!.HttpEvent -= _httpClient_FetchAssetProgress;
            }

            // Return asset index
            return returnAsset;
        }

        private async Task<(int, long)> FetchByType(SRAssetType type, List<SRAsset> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Fetching Caches Type: <type>"
            _status!.ActivityStatus = string.Format(Lang!._CachesPage!.CachesStatusFetchingType!, type);
            _status!.IsProgressTotalIndetermined = true;
            _status!.IsIncludePerFileIndicator = false;
            UpdateStatus();

            try
            {
                // Start reading the metadata and build the asset index of each type
                SRAssetProperty assetProperty;
                switch (type)
                {
                    case SRAssetType.IFix:
                        await _innerGameVersionManager!.StarRailMetadataTool!.ReadIFixMetadataInformation(token);
                        assetProperty = _innerGameVersionManager!.StarRailMetadataTool!.MetadataIFix!.GetAssets();
                        assetIndex!.AddRange(assetProperty!.AssetList!);
                        return (assetProperty.AssetList.Count, assetProperty.AssetTotalSize);
                    case SRAssetType.DesignData:
                        await _innerGameVersionManager!.StarRailMetadataTool!.ReadDesignMetadataInformation(token);
                        assetProperty = _innerGameVersionManager.StarRailMetadataTool.MetadataDesign!.GetAssets();
                        assetIndex!.AddRange(assetProperty!.AssetList!);
                        return (assetProperty.AssetList.Count, assetProperty.AssetTotalSize);
                    case SRAssetType.Lua:
                        await _innerGameVersionManager!.StarRailMetadataTool!.ReadLuaMetadataInformation(token);
                        assetProperty = _innerGameVersionManager.StarRailMetadataTool.MetadataLua!.GetAssets();
                        assetIndex!.AddRange(assetProperty!.AssetList!);
                        return (assetProperty.AssetList.Count, assetProperty.AssetTotalSize);
                }

                return (0, 0);
            }
            catch { throw; }
        }

        #region Utilities
        private unsafe string GetExistingGameRegionID()
        {
#nullable enable
            object? value = RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            if (value == null)
            {
                return _gameVersionManager!.GamePreset!.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");
            }
#nullable disable

            ReadOnlySpan<byte> span = (value as byte[]).AsSpan();
            fixed (byte* valueSpan = span)
            {
                string name = Encoding.UTF8.GetString(valueSpan, span.Length - 1);
                return name;
            }
        }

        private CacheAssetType ConvertCacheAssetTypeEnum(SRAssetType assetType) => assetType switch
        {
            SRAssetType.IFix => CacheAssetType.IFix,
            SRAssetType.DesignData => CacheAssetType.DesignData,
            SRAssetType.Lua => CacheAssetType.Lua,
            _ => CacheAssetType.General
        };
        #endregion
    }
}
