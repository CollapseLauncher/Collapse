using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;

namespace CollapseLauncher
{
    internal partial class StarRailRepair
    {
        private async Task Fetch(List<FilePropertiesRemote> assetIndex, CancellationToken token)
        {
            // Set total activity string as "Loading Indexes..."
            _status.ActivityStatus = Lang._GameRepairPage.Status2;
            _status.IsProgressTotalIndetermined = true;
            UpdateStatus();

            try
            {
                // Subscribe the fetching progress and subscribe StarRailMetadataTool progress to adapter
                _gameVersionManager.StarRailMetadataTool.HttpEvent += _httpClient_FetchAssetProgress;

                // Initialize the metadata tool (including dispatcher and gateway)
                await _gameVersionManager.StarRailMetadataTool.Initialize(token, GetExistingGameRegionID(), Path.Combine(_gamePath, $"{Path.GetFileNameWithoutExtension(_gamePreset.GameExecutableName)}_Data\\Persistent"));

                // Read block metadata and convert to FilePropertiesRemote
                await _gameVersionManager.StarRailMetadataTool.ReadAsbMetadataInformation(token);
                await _gameVersionManager.StarRailMetadataTool.ReadBlockMetadataInformation(token);
                ConvertSRMetadataToAssetIndex(_gameVersionManager.StarRailMetadataTool.MetadataBlock, assetIndex);

                // Read Audio metadata and convert to FilePropertiesRemote
                await _gameVersionManager.StarRailMetadataTool.ReadAudioMetadataInformation(token);
                ConvertSRMetadataToAssetIndex(_gameVersionManager.StarRailMetadataTool.MetadataAudio, assetIndex);

                // Read Video metadata and convert to FilePropertiesRemote
                await _gameVersionManager.StarRailMetadataTool.ReadVideoMetadataInformation(token);
                ConvertSRMetadataToAssetIndex(_gameVersionManager.StarRailMetadataTool.MetadataVideo, assetIndex);
            }
            finally
            {
                // Unsubscribe the fetching progress and dispose it and unsubscribe cacheUtil progress to adapter
                _gameVersionManager.StarRailMetadataTool.HttpEvent -= _httpClient_FetchAssetProgress;
            }
        }

        #region Utilities
        private unsafe string GetExistingGameRegionID()
        {
#nullable enable
            object? value = GameSettings.Statics.RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            if (value == null)
            {
                return _gamePreset.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");
            }
#nullable disable

            ReadOnlySpan<byte> span = (value as byte[]).AsSpan();
            fixed (byte* valueSpan = span)
            {
                string name = Encoding.UTF8.GetString(valueSpan, span.Length - 1);
                return name;
            }
        }

        private void ConvertSRMetadataToAssetIndex(SRMetadataBase metadata, List<FilePropertiesRemote> assetIndex)
        {
            int voLangID = _gamePreset.GetVoiceLanguageID();
            string voLangName = _gamePreset.GetStarRailVoiceLanguageFullNameByID(voLangID);

            IEnumerable<SRAsset> srAssetEnumerateFiltered = metadata.GetAssets().AssetList
                .Where(x => IsAudioLangFile(x, voLangName));

            int count = 0;
            long countSize = 0;

            foreach (SRAsset asset in srAssetEnumerateFiltered)
            {
                assetIndex.Add(new FilePropertiesRemote
                {
                    N = asset.LocalName,
                    RN = asset.RemoteURL,
                    CRC = HexTool.BytesToHexUnsafe(asset.Hash),
                    FT = ConvertFileTypeEnum(asset.AssetType),
                    S = asset.Size,
                    IsPatchApplicable = asset.IsPatch
                });
                count++;
                countSize += asset.Size;

#if DEBUG
                LogWriteLine($"Adding {asset.LocalName} [T: {asset.AssetType}] [S: {SummarizeSizeSimple(asset.Size)} / {asset.Size} bytes]", LogType.Default, true);
#endif
            }

            LogWriteLine($"Added {count} assets with {SummarizeSizeSimple(countSize)}/{countSize} bytes in size", LogType.Default, true);
        }

        private bool IsAudioLangFile(SRAsset asset, string langName)
        {
            switch (asset.AssetType)
            {
                case SRAssetType.Audio:
                    string[] nameDef = asset.LocalName.Split('/');
                    if (nameDef.Length > 1)
                    {
                        return nameDef[0] == langName || nameDef[0] == "SFX";
                    }
                    return true;
                default:
                    return true;
            }
        }

        private void CountAssetIndex(List<FilePropertiesRemote> assetIndex)
        {
            // Sum the assetIndex size and assign to _progressTotalSize
            _progressTotalSize = assetIndex.Sum(x => x.S);

            // Assign the assetIndex count to _progressTotalCount
            _progressTotalCount = assetIndex.Count;
        }

        private FileType ConvertFileTypeEnum(SRAssetType assetType) => assetType switch
        {
            SRAssetType.Asb => FileType.Blocks,
            SRAssetType.Block => FileType.Blocks,
            SRAssetType.Audio => FileType.Audio,
            SRAssetType.Video => FileType.Video,
            _ => FileType.Generic
        };

        private RepairAssetType ConvertRepairAssetTypeEnum(FileType assetType) => assetType switch
        {
            FileType.Blocks => RepairAssetType.Block,
            FileType.Audio => RepairAssetType.Audio,
            FileType.Video => RepairAssetType.Video,
            _ => RepairAssetType.General
        };
        #endregion
    }
}
