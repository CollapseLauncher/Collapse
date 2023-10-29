using CollapseLauncher.Interfaces;
using Hi3Helper.EncTool.Parser.AssetMetadata;
using Hi3Helper.Preset;
using Microsoft.UI.Xaml;
using System.IO;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeHonkaiVersion : GameVersionBase, IGameVersionCheck
    {
        #region Properties
        private string GameXMFPath { get => Path.Combine(GameDirPath, $"{Path.GetFileNameWithoutExtension(GamePreset.GameExecutableName)}_Data", "StreamingAssets\\Asb\\pc\\Blocks.xmf"); }
        #endregion

        public GameTypeHonkaiVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, PresetConfigV2 gamePreset)
            : base(parentUIElement, gameRegionProp, gamePreset)
        {
            // Try check for reinitializing game version from XMF file.
            TryReinitializeGameVersion();
        }

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp == null ? null : GameDeltaPatchProp;

        public override bool IsGameVersionMatch()
        {
            // In this override, the check will be done twice.
            // Check the version in INI file first, then check the version based on XMF file.
            bool IsBaseGameVersionMatch = base.IsGameVersionMatch();
            (bool, int[]) IsXMFVersionMatches = XMFUtility.CheckIfXMFVersionMatches(GameXMFPath, GameVersionAPI.VersionArrayManifest);

            // Choose either one of them in which one is matches.
            return IsBaseGameVersionMatch || IsXMFVersionMatches.Item1;
        }

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
            (bool, int[]) IsXMFVersionMatches = XMFUtility.CheckIfXMFVersionMatches(GameXMFPath, GameVersionAPI.VersionArrayManifest);
            if (GameVersionInstalled == null && IsXMFVersionMatches.Item1)
            {
                GameVersionInstalled = GameVersionAPI;
            }

            // If the version has proper length, keep set the installed version
            if (IsXMFVersionMatches.Item2.Length == XMFUtility.XMFVersionLength)
            {
                GameVersionInstalled = new GameVersion(IsXMFVersionMatches.Item2);
            }
        }
    }
}
