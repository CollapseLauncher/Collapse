using CollapseLauncher.GameManagement.Versioning;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.Metadata;
using System;
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable CheckNamespace

namespace CollapseLauncher.GameVersioning
{
    internal sealed class GameTypeHonkaiVersion(
        ILauncherApi launcherApi,
        PresetConfig presetConfig) : GameVersionBase(launcherApi, presetConfig)
    {
        #region Statics
        private static readonly Version SenadinaVersion = new(7, 3, 0);
        #endregion

        #region Public properties
        public bool IsCurrentSenadinaVersion { get => GameVersionAPI?.ToVersion() >= SenadinaVersion; }
        public bool IsPreloadSenadinaVersion { get => GameVersionAPIPreload.HasValue && GameVersionAPIPreload.Value.ToVersion() >= SenadinaVersion; }
        #endregion

        public override bool IsGameHasDeltaPatch() => GameDeltaPatchProp != null;

        public override DeltaPatchProperty GetDeltaPatchInfo() => GameDeltaPatchProp;
    }
}
