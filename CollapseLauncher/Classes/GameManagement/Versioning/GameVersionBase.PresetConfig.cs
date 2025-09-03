using CollapseLauncher.Helper.Metadata;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase
    {
        #region Game Config Properties
        public virtual string?        GameName       { get; set; }
        public virtual string?        GameRegion     { get; set; }
        public virtual GameVendorProp VendorTypeProp { get; private set; }
        public virtual GameNameType   GameType
        {
            get => GamePreset.GameType;
        }

        [field: AllowNull, MaybeNull]
        public virtual PresetConfig GamePreset
        {
            get
            {
                // Return cached preset if field is not null
                if (field != null)
                {
                    return field;
                }

                // Throw if GameName or GameRegion is null
                if (string.IsNullOrEmpty(GameName) || string.IsNullOrEmpty(GameRegion))
                {
                    throw new ArgumentException("Cannot get current Game Preset as GameName and GameRegion property must NOT be null!");
                }

                // Throw if the preset of the GameName is not found. Otherwise, get the preset of the GameName
                if (!(LauncherMetadataHelper.LauncherMetadataConfig?
                    .TryGetValue(GameName, out Dictionary<string, PresetConfig>? gamePreset) ?? false))
                {
                    throw new ArgumentException($"Cannot get current preset from GameName: '{GameName}' as it is not found in the LauncherMetadataConfig!");
                }

                // Throw if the preset of the GameRegion is not found. Otherwise, get the preset of the GameRegion
                if (!(gamePreset?
                    .TryGetValue(GameRegion, out PresetConfig? regionConfig) ?? false))
                {
                    throw new ArgumentException($"Cannot get current preset from GameRegion: '{GameRegion}' as it is not found in the LauncherMetadataConfig!");
                }

                // Assign to the field and cache, then return.
                return field = regionConfig;
            }
        }
        #endregion
    }
}