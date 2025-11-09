using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using System;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase : IGameVersion
    {
        protected GameVersionBase() {}

        protected GameVersionBase(
            ILauncherApi launcherApi,
            PresetConfig presetConfig)
        {
            ArgumentException.ThrowIfNullOrEmpty(presetConfig.GameName);
            ArgumentException.ThrowIfNullOrEmpty(presetConfig.ZoneName);

            GamePreset  = presetConfig;
            GameName    = presetConfig.GameName;
            GameRegion  = presetConfig.ZoneName;
            GameBiz     = presetConfig.LauncherBizName ?? "";
            GameId      = presetConfig.LauncherGameId ?? "";
            LauncherApi = launcherApi;

            // Initialize INI Prop ahead of other operations
            // ReSharper disable once VirtualMemberCallInConstructor
            InitializeIniProp();
        }
    }
}