using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeStarRailVersion : GameVersionBase, IGameVersionCheck
    {
        #region Properties
        public SRMetadata StarRailMetadataTool { get; set; }
        #endregion

        public GameTypeStarRailVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfigV2 gamePreset)
            : base(parentUIElement, gameRegionProp, gamePreset)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();

            // Initialize Star Rail metadata tool
            if (GamePreset.ProtoDispatchKey != null)
            {
                StarRailMetadataTool = new SRMetadata(
                    GamePreset.GameDispatchArrayURL[0],
                    GamePreset.ProtoDispatchKey,
                    GamePreset.GameDispatchURLTemplate,
                    GamePreset.GameGatewayURLTemplate,
                    GamePreset.GameDispatchChannelName,
                    GameVersionAPI.VersionString);
            }
        }

        ~GameTypeStarRailVersion() => StarRailMetadataTool?.Dispose();

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp == null ? null : GameDeltaPatchProp;

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
