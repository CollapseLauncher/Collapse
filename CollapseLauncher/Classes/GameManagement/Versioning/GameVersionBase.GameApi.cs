using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Interfaces.Class;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase
    {
        #region Game Region Resource Prop
        public virtual ILauncherApi? LauncherApi { get; }

        protected virtual HypChannelSdkData? GameDataSdk
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                LauncherApi?
                   .LauncherGameResourceSdk?
                   .Data?
                   .TryFindByBizOrId(GameBiz, GameId, out field);
                return field;
            }
        }

        protected virtual HypResourcePluginData? GameDataPlugin
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                LauncherApi?
                   .LauncherGameResourcePlugin?
                   .Data?
                   .TryFindByBizOrId(GameBiz, GameId, out field);
                return field;
            }
        }

        protected virtual HypResourcesData? GameDataPackage
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                LauncherApi?
                   .LauncherGameResourcePackage?
                   .Data?
                   .TryFindByBizOrId(GameBiz, GameId, out field);
                return field;
            }
        }

        protected virtual HypResourcePackageData? GameDataPackageMain
        {
            get => field ??= GameDataPackage?.MainPackage;
        }

        protected virtual HypResourcePackageData? GameDataPackagePreload
        {
            get => field ??= GameDataPackage?.PreDownload;
        }

        protected virtual HypLauncherSophonBranchesKind? GameDataSophonBranch
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                LauncherApi?
                   .LauncherGameSophonBranches?
                   .Data?
                   .TryFindByBizOrId(GameBiz, GameId, out field);
                return field;
            }
        }

        protected virtual HypGameInfoBranchData? GameDataSophonBranchMain
        {
            get => field ??= GameDataSophonBranch?.GameMainField;
        }

        protected virtual HypGameInfoBranchData? GameDataSophonBranchPreload
        {
            get => field ??= GameDataSophonBranch?.GamePreloadField;
        }

        // Assign for the Game Delta-Patch properties (if any).
        // If there's no Delta-Patch, then set it to null.
        protected virtual DeltaPatchProperty? GameDeltaPatchProp
        {
            get
            {
                if (string.IsNullOrEmpty(GamePreset.ProfileName))
                {
                    throw new NullReferenceException("Cannot get delta patch property as GamePreset -> ProfileName is null or empty!");
                }
                return CheckDeltaPatchUpdate(GameDirPath, GamePreset.ProfileName, GameVersionAPI ?? throw new NullReferenceException("GameVersionAPI returns a null"));
            }
        }

        protected virtual List<HypPluginPackageInfo> MismatchPlugin { get; set; } = [];
        #endregion

        #region Game Version API Properties
        protected virtual GameVersion? SdkVersionAPI
        {
            get => field ??= GameDataSdk?.Version;
        }

        private static GameVersion? TryGetVersionFromSophonOrPackage(GameVersion? versionFromSophon,
                                                                     GameVersion? versionFromPackage)
        {
            // Note 2025/11/08:
            // Since the refactor, we are going prioritize to lookup version from Sophon field first.
            // If not available, then fallback to legacy package field.

            // Check if both have values.
            // Then check which one is higher to avoid wrong version from legacy package field.
            if (versionFromSophon.HasValue &&
                versionFromPackage.HasValue &&
                versionFromSophon > versionFromPackage)
            {
                return versionFromSophon;
            }

            // If field is still null, try assign either from sophon or package.
            return versionFromSophon ?? versionFromPackage;
        }

        protected virtual GameVersion? GameVersionAPI
        {
            get
            {
                if (field.HasValue && field != GameVersion.Empty)
                {
                    return field;
                }

                GameVersion? versionFromSophon  = GameDataSophonBranchMain?.Tag;
                GameVersion? versionFromPackage = GameDataPackageMain?.CurrentVersion?.Version;

                field ??= TryGetVersionFromSophonOrPackage(versionFromSophon, versionFromPackage);
                return field;
            }
        }

        protected virtual GameVersion? GameVersionAPIPreload
        {
            get
            {
                if (field.HasValue && field != GameVersion.Empty)
                {
                    return field;
                }

                GameVersion? versionFromSophon  = GameDataSophonBranchPreload?.Tag;
                GameVersion? versionFromPackage = GameDataPackagePreload?.CurrentVersion?.Version;

                field ??= TryGetVersionFromSophonOrPackage(versionFromSophon, versionFromPackage);
                return field;
            }
        }

        [field: AllowNull, MaybeNull]
        protected virtual Dictionary<string, GameVersion> PluginVersionsAPI
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                // Initialize dictionary and lock upon write
                lock (field ??= new Dictionary<string, GameVersion>(StringComparer.OrdinalIgnoreCase))
                {
                    if (GameDataPlugin == null ||
                        GameDataPlugin.Plugins.Count == 0)
                    {
                        return field;
                    }

                    foreach (HypPluginPackageInfo plugin in GameDataPlugin.Plugins)
                    {
                        if (string.IsNullOrEmpty(plugin.PluginId))
                        {
                            continue;
                        }
                        field.Add(plugin.PluginId, plugin.Version);
                    }
                }

                return field;
            }
        }

        protected virtual GameVersion? GameVersionInstalled
        {
            get
            {
                // Check if the INI has game_version key...
                if (!GameIniVersion[DefaultIniVersionSection]
                    .TryGetValue("game_version", out IniValue gameVersion))
                {
                    return null;
                }

                // Get the game version
                string? val = gameVersion;

                // Otherwise, return the game version
                return val;
            }
            set
            {
                UpdateGameVersion(value ?? GameVersionAPI, false);
                UpdateGameChannels(false);
            }
        }

        [field: AllowNull, MaybeNull]
        protected virtual Dictionary<string, GameVersion> PluginVersionsInstalled
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                // Initialize dictionary and lock upon write
                lock (field ??= new Dictionary<string, GameVersion>(StringComparer.OrdinalIgnoreCase))
                {
                    if (GameDataPlugin == null ||
                        GameDataPlugin.Plugins.Count == 0)
                    {
                        return field;
                    }

                    // Get the version and convert it into GameVersion
                    foreach (HypPluginPackageInfo plugin in GameDataPlugin.Plugins)
                    {
                        if (string.IsNullOrEmpty(plugin.PluginId))
                        {
                            continue;
                        }

                        // Check if the INI has plugin_ID_version key...
                        string keyName = $"plugin_{plugin.PluginId}_version";
                        if (!GameIniVersion[DefaultIniVersionSection]
                               .TryGetValue(keyName, out IniValue getPluginVersion))
                        {
                            continue;
                        }

                        string? val = getPluginVersion;
                        if (string.IsNullOrEmpty(val))
                        {
                            continue;
                        }

                        field.TryAdd(plugin.PluginId, val);
                    }
                }

                return field;
            }
            set
            {
                field = value;
                UpdatePluginVersions(field);
            }
        }

        protected virtual GameVersion? SdkVersionInstalled
        {
            get
            {
                // Check if the game config has SDK version. If not, return null
                const string keyName = "plugin_sdk_version";
                if (!GameIniVersion[DefaultIniVersionSection].TryGetValue(keyName, out IniValue pluginSdkVersion))
                    return null;

                // Check if it has no value, then return null
                string? versionName = pluginSdkVersion;

                // Try parse the version.
                return versionName;
            }
            set => UpdateSdkVersion(value ?? SdkVersionAPI);
        }
        #endregion

        #region Game Version API Methods
        public virtual GameVersion? GetGameExistingVersion() => GameVersionInstalled;

        public virtual GameVersion? GetGameVersionApi() => GameVersionAPI;

        public virtual GameVersion? GetGameVersionApiPreload() => GameVersionAPIPreload;

        public virtual GameVersion? GetSdkVersionInstalled() => SdkVersionInstalled;

        public virtual GameVersion? GetSdkVersionApi() => SdkVersionAPI;

        public virtual Dictionary<string, GameVersion> GetPluginVersionsInstalled() => PluginVersionsInstalled;

        public virtual List<HypPluginPackageInfo> GetMismatchPlugin() => MismatchPlugin;
        #endregion

        #region Game Info Methods
        public virtual DeltaPatchProperty? GetDeltaPatchInfo() => GameDeltaPatchProp;

        public virtual GamePackageResult GetGameLatestZip(GameInstallStateEnum gameState)
        {
            // Initialize the return list
            List<HypPackageData> returnMain  = [];
            List<HypPackageData> returnAudio = [];

            // If the current latest region package is null, then throw
            if (GameDataPackageMain?.CurrentVersion is not {} currentLatestRegionPackage)
            {
                throw new NullReferenceException("LauncherApi.data.game.latest returns a null!");
            }

            // If the GameVersion is not installed, then return the full latest package one
            if (gameState is GameInstallStateEnum.NotInstalled or GameInstallStateEnum.GameBroken)
            {
                // Add the latest prop to the return list
                returnMain.AddRange(currentLatestRegionPackage.GamePackages);
                returnAudio.AddRange(currentLatestRegionPackage.AudioPackages);

                return new GamePackageResult(returnMain,
                                             returnAudio,
                                             currentLatestRegionPackage.ResourceListUrl,
                                             currentLatestRegionPackage.Version);
            }

            string?     uncompressedUrl;
            GameVersion version;

            // Otherwise on update, use diff package one.
            // Try to get the patch file by the first or default (null)
            if (GameDataPackageMain?.Patches is {} currentLatestRegionPatch &&
                currentLatestRegionPatch.FirstOrDefault(x => x.Version == GameVersionInstalled) is { } selectedVersionPatch)
            {
                returnMain.AddRange(selectedVersionPatch.GamePackages);
                returnAudio.AddRange(selectedVersionPatch.AudioPackages);
                uncompressedUrl = selectedVersionPatch.ResourceListUrl;
                version         = selectedVersionPatch.Version;

                return new GamePackageResult(returnMain,
                                             returnAudio,
                                             uncompressedUrl,
                                             version);
            }

            // If empty, otherwise add the latest one.
            returnMain.AddRange(currentLatestRegionPackage.GamePackages);
            returnAudio.AddRange(currentLatestRegionPackage.AudioPackages);
            uncompressedUrl = currentLatestRegionPackage.ResourceListUrl;
            version         = currentLatestRegionPackage.Version;

            return new GamePackageResult(returnMain,
                                         returnAudio,
                                         uncompressedUrl,
                                         version);
        }

        public virtual GamePackageResult GetGamePreloadZip()
        {
            // Initialize the return list
            List<HypPackageData> returnMain  = [];
            List<HypPackageData> returnAudio = [];
            string?              uncompressedUrl;
            GameVersion          version;

            // Try to add patches if available
            if (GameDataPackagePreload?.Patches is {} preloadRegionPatch &&
                preloadRegionPatch.FirstOrDefault(x => x.Version == GameVersionInstalled) is {} preloadSelectedPatchVersion)
            {
                returnMain.AddRange(preloadSelectedPatchVersion.GamePackages);
                returnAudio.AddRange(preloadSelectedPatchVersion.AudioPackages);
                uncompressedUrl = preloadSelectedPatchVersion.ResourceListUrl;
                version         = preloadSelectedPatchVersion.Version;

                return new GamePackageResult(returnMain,
                                             returnAudio,
                                             uncompressedUrl,
                                             version);
            }

            if (GameDataPackagePreload?.CurrentVersion is not {} preloadRegionPackage)
            {
                return new GamePackageResult(returnMain,
                                             returnAudio);
            }

            // Otherwise, grab from latest one if available and not empty
            returnMain.AddRange(preloadRegionPackage.GamePackages);
            returnAudio.AddRange(preloadRegionPackage.AudioPackages);
            uncompressedUrl = preloadRegionPackage.ResourceListUrl;
            version         = preloadRegionPackage.Version;

            // Return the list
            return new GamePackageResult(returnMain,
                                         returnAudio,
                                         uncompressedUrl,
                                         version);
        }

        public virtual List<HypPluginPackageInfo> GetGamePluginZip()
        {
            if (GameDataPlugin is not { } pluginPackage ||
                pluginPackage.Plugins.Count == 0)
            {
                return [];
            }

            return [.. pluginPackage.Plugins];
        }

        public virtual List<HypChannelSdkData> GetGameSdkZip()
        {
            // Check if the sdk is not empty, then add it
            if (GameDataSdk is not {} sdkPackage)
            {
                return [];
            }

            return [sdkPackage];
        }
        #endregion
    }
}
