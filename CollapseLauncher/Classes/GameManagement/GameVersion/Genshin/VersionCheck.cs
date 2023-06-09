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
