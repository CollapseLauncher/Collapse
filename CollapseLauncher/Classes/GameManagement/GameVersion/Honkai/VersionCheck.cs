using CollapseLauncher.Interfaces;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using System;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeHonkaiVersion : GameVersionBase, IGameVersionCheck
    {
        #region Statics
        private static Version senadinaVersion = new Version(7, 3, 0);
        #endregion

        #region Public properties
        public bool IsCurrentSenadinaVersion { get => GameVersionAPI.ToVersion() >= senadinaVersion; }
        public bool IsPreloadSenadinaVersion { get => GameVersionAPIPreload.HasValue ? GameVersionAPIPreload.Value.ToVersion() >= senadinaVersion : false; }
        #endregion

        public GameTypeHonkaiVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfigV2 gamePreset)
            : base(parentUIElement, gameRegionProp, gamePreset)
        {
            // Try check for reinitializing game version from XMF file.
            TryReinitializeGameVersion();
        }

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp == null ? null : GameDeltaPatchProp;

        public override void Reinitialize()
        {
            // Do base reinitialization first
            base.Reinitialize();

            // Then try Reinitialize game version provided by XMF
            TryReinitializeGameVersion();
        }

        private void TryReinitializeGameVersion()
        {
            // Check if the GameVersionInstalled == null (version config doesn't exist)
            // and if the XMF file version matches the version from GameVersionAPI, then reinitialize the version config
            // and save the version config by assigning GameVersionInstalled.
            if (GameVersionInstalled == null)
            {
                GameVersionInstalled = GameVersionAPI;
            }
        }
    }
}
