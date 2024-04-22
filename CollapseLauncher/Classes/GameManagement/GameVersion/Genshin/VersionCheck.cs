using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System.Collections.Generic;

namespace CollapseLauncher.GameVersioning
{
    internal class GameTypeGenshinVersion : GameVersionBase, IGameVersionCheck
    {
        #region Properties
        public readonly List<string> _audioVoiceLanguageList = new List<string> { "Chinese", "English(US)", "Japanese", "Korean" };
        #endregion

        public GameTypeGenshinVersion(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gamePreset)
            : base(parentUIElement, gameRegionProp, gameName, gamePreset)
        {
            // Try check for reinitializing game version.
            TryReinitializeGameVersion();
        }

        public override bool IsGameHasDeltaPatch() => false;

        public override DeltaPatchProperty GetDeltaPatchInfo() => null;
    }
}
