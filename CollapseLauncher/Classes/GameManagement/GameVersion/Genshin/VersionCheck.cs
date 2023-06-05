using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Linq;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeGenshinVersion : GameVersionBase, IGameVersionCheck
    {
        #region Properties
        public readonly List<string> _audioVoiceLanguageList = new List<string> { "Chinese", "English(US)", "Japanese", "Korean" };
        #endregion

        public GameTypeGenshinVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfigV2 gamePreset)
            : base(parentUIElement, gameRegionProp, gamePreset)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();
        }

        public override bool IsGameHasDeltaPatch() => false;

        public override DeltaPatchProperty GetDeltaPatchInfo() => null;

        public override List<RegionResourceVersion> GetGameLatestZip(GameInstallStateEnum gameState)
        {
            // If the GameVersion is not installed, then return the latest one
            if (gameState == GameInstallStateEnum.NotInstalled || gameState == GameInstallStateEnum.GameBroken)
            {
                return new List<RegionResourceVersion> { GameAPIProp.data.game.latest };
            }

            // Try get the diff file  by the first or default (null)
            RegionResourceVersion diff = GameAPIProp.data.game.diffs
                .Where(x => x.version == GameVersionInstalled?.VersionString)
                .FirstOrDefault();

            // Return if the diff is null, then get the latest. If found, then return the diff one.
            return new List<RegionResourceVersion> { diff == null ? GameAPIProp.data.game.latest : diff };
        }

        private void TryReinitializeGameVersion()
        {
            // Check if the GameVersionInstalled == null (version config doesn't exist),
            // Reinitialize the version config and save the version config by assigning GameVersionInstalled.
            if (GameVersionInstalled == null)
            {
                GameVersionInstalled = GameVersionAPI;
            }
        }
    }
}
