using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Parser.AssetMetadata.SRMetadataAsset;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
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
                ConvertSRMetadataToAssetIndex(_gameVersionManager.StarRailMetadataTool.MetadataAudio, assetIndex, true);

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
            // Try get the value as nullable object
            object? value = GameSettings.Statics.RegistryRoot?.GetValue("App_LastServerName_h2577443795", null);
            // Check if the value is null, then return the default name
            if (value == null)
            {
                // Return the dispatch default name. If none, then throw
                return _gamePreset.GameDispatchDefaultName ?? throw new KeyNotFoundException("Default dispatcher name in metadata is not exist!");
            }
#nullable disable

            // Cast the value as byte span
            ReadOnlySpan<byte> span = ((byte[])value).AsSpan();
            // Get the name from the span and trim the \0 character at the end
            string name = Encoding.UTF8.GetString(span.Slice(0, span.Length - 1));
            return name;
        }

        private void ConvertSRMetadataToAssetIndex(SRMetadataBase metadata, List<FilePropertiesRemote> assetIndex, bool writeAudioLangRedord = false)
        {
            // Get the voice Lang ID
            int voLangID = _gamePreset.GetVoiceLanguageID();
            // Get the voice Lang name by ID
            string voLangName = _gamePreset.GetStarRailVoiceLanguageFullNameByID(voLangID);

            // If prompt to write Redord file
            if (writeAudioLangRedord)
            {
                // Get game executable name, directory and file path
                string execName = Path.GetFileNameWithoutExtension(_gamePreset.GameExecutableName);
                string audioRedordDir = Path.Combine(_gamePath, @$"{execName}_Data\Persistent\Audio\AudioPackage\Windows");
                string audioRedordPath = Path.Combine(audioRedordDir, "AudioLangRedord.txt");

                // Create the directory if not exist
                if (!Directory.Exists(audioRedordDir)) Directory.CreateDirectory(audioRedordDir);
                // Then write the Redord file content
                File.WriteAllText(audioRedordPath, "{\"AudioLang\":\"" + voLangName + "\"}");
            }

            int count = 0;
            long countSize = 0;

            // Enumerate the Asset List
            foreach (SRAsset asset in metadata.EnumerateAssets())
            {
                // Get the hash by bytes
                string hash = HexTool.BytesToHexUnsafe(asset.Hash);

                // Filter only current audio language file and other assets
                if (FilterCurrentAudioLangFile(asset, voLangName, out bool IsHasHashMark))
                {
                    // Convert and add the asset as FilePropertiesRemote to assetIndex
                    assetIndex.Add(new FilePropertiesRemote
                    {
                        N = asset.LocalName,
                        RN = asset.RemoteURL,
                        CRC = hash,
                        FT = ConvertFileTypeEnum(asset.AssetType),
                        S = asset.Size,
                        IsPatchApplicable = asset.IsPatch,
                        IsHasHashMark = IsHasHashMark
                    });
                    count++;
                    countSize += asset.Size;

#if DEBUG
                    LogWriteLine($"Adding {asset.LocalName} [T: {asset.AssetType}] [S: {SummarizeSizeSimple(asset.Size)} / {asset.Size} bytes]", LogType.Default, true);
#endif
                }
            }

            LogWriteLine($"Added {count} assets with {SummarizeSizeSimple(countSize)}/{countSize} bytes in size", LogType.Default, true);
        }

        private bool FilterCurrentAudioLangFile(SRAsset asset, string langName, out bool isHasHashMark)
        {
            // Set output value as false
            isHasHashMark = false;
            switch (asset.AssetType)
            {
                // In case if the type is SRAssetType.Audio, then do filtering
                case SRAssetType.Audio:
                    // Set isHasHashMark to true
                    isHasHashMark = true;
                    // Split the name definition from LocalName
                    string[] nameDef = asset.LocalName.Split('/');
                    // If the name definition array length > 1, then start do filtering
                    if (nameDef.Length > 1)
                    {
                        // Compare if the first name definition is equal to target langName.
                        // Also return if the file is an audio language file if it is a SFX file or not.
                        return nameDef[0] == langName || nameDef[0] == "SFX";
                    }
                    // If it's not in criteria of name definition, then return true as "normal asset"
                    return true;
                default:
                    // return true as "normal asset"
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
