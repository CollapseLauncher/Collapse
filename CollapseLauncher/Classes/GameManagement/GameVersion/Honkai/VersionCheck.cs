using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using System.IO;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeHonkaiVersion : GameVersionBase, IGameVersionCheck
    {
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
