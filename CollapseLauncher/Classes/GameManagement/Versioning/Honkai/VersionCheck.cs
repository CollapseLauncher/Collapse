using CollapseLauncher.GameManagement.Versioning;
using Microsoft.UI.Xaml;
using System;
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.GameVersioning
{
    internal sealed class GameTypeHonkaiVersion(
        UIElement          parentUIElement,
        RegionResourceProp gameRegionProp,
        string             gameName,
        string             gameRegion)
        : GameVersionBase(parentUIElement, gameRegionProp, gameName, gameRegion)
    {
        #region Statics
        private static readonly Version senadinaVersion = new(7, 3, 0);
        #endregion

        #region Public properties
        public bool IsCurrentSenadinaVersion { get => GameVersionAPI?.ToVersion() >= senadinaVersion; }
        public bool IsPreloadSenadinaVersion { get => GameVersionAPIPreload.HasValue && GameVersionAPIPreload.Value.ToVersion() >= senadinaVersion; }
        #endregion

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp;
    }
}
