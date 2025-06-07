using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using System;
using System.Collections.Generic;
using System.Linq;

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
        public virtual RegionResourceProp? GameApiProp { get; set; }

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

        protected virtual List<RegionResourcePlugin>? MismatchPlugin { get; set; }
        #endregion

        #region Game Version API Properties
        protected virtual GameVersion? SdkVersionAPI
        {
            get
            {
                // Return null if the plugin is not exist
                if (GameApiProp?.data?.sdk == null)
                    return null;

                // If the version provided by the SDK API, return the result
                return GameVersion.TryParse(GameApiProp.data?.sdk?.version, out GameVersion? result) ? result :
                    // Otherwise, return null
                    null;
            }
        }

        protected virtual GameVersion? GameVersionAPI
        {
            get => GameVersion.TryParse(GameApiProp?.data?.game?.latest?.version, out GameVersion? version) ? version : null;
        }

        protected virtual GameVersion? GameVersionAPIPreload
        {
            get
            {
                GameVersion? currentInstalled = GameVersionInstalled;

                // If no installation installed, then return null
                if (!currentInstalled.HasValue)
                    return null;

                // Check if the pre_download_game property has value. If not, then return null
                if (string.IsNullOrEmpty(GameApiProp?.data?.pre_download_game?.latest?.version))
                    return null;

                return new GameVersion(GameApiProp.data.pre_download_game.latest.version);
            }
        }

        protected virtual Dictionary<string, GameVersion> PluginVersionsAPI
        {
            get
            {
                // Initialize dictionary
                Dictionary<string, GameVersion> value = new();

                // Return empty if the plugin is not exist
                if (GameApiProp?.data?.plugins == null || GameApiProp.data.plugins.Count == 0)
                {
                    return value;
                }

                // Get the version and convert it into GameVersion
                foreach (var plugin in GameApiProp.data.plugins
                                                  .Where(plugin => plugin.plugin_id != null)
                                                  .Select(plugin => (plugin.plugin_id, plugin.version)))
                {
                    if (string.IsNullOrEmpty(plugin.plugin_id))
                    {
                        continue;
                    }
                    value.TryAdd(plugin.plugin_id, new GameVersion(plugin.version));
                }

                return value;
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

                // If not, then return as null
                if (string.IsNullOrEmpty(val))
                {
                    return null;
                }

                // Otherwise, return the game version
                return new GameVersion(val);
            }
            set
            {
                UpdateGameVersion(value ?? GameVersionAPI, false);
                UpdateGameChannels(false);
            }
        }

        protected virtual Dictionary<string, GameVersion>? PluginVersionsInstalled
        {
            get
            {
                // Initialize dictionary
                Dictionary<string, GameVersion> value = new();

                // Return empty if the plugin is not exist
                if (GameApiProp?.data?.plugins == null || GameApiProp.data?.plugins?.Count == 0)
                {
                    return value;
                }

                // Get the version and convert it into GameVersion
                foreach (var plugin in GameApiProp?.data?.plugins!)
                {
                    // Check if the INI has plugin_ID_version key...
                    string keyName = $"plugin_{plugin.plugin_id}_version";
                    if (!GameIniVersion[DefaultIniVersionSection].TryGetValue(keyName, out IniValue getPluginVersion))
                    {
                        continue;
                    }

                    string? val = getPluginVersion;
                    if (string.IsNullOrEmpty(val))
                    {
                        continue;
                    }

                    if (plugin.plugin_id != null)
                    {
                        _ = value.TryAdd(plugin.plugin_id, new GameVersion(val));
                    }
                }

                return value;
            }
            set => UpdatePluginVersions(value ?? PluginVersionsAPI);
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
                if (string.IsNullOrEmpty(versionName))
                    return null;

                // Try parse the version.
                return !GameVersion.TryParse(versionName, out GameVersion? result) ?
                    // If it's not valid, then return null
                    null :
                    // Otherwise, return the result
                    result;
            }
            set => UpdateSdkVersion(value ?? SdkVersionAPI);
        }
        #endregion

        #region Game Version API Methods
        public virtual GameVersion? GetGameExistingVersion() => GameVersionInstalled;

        public virtual GameVersion? GetGameVersionApi() => GameVersionAPI;

        public virtual GameVersion? GetGameVersionApiPreload() => GameVersionAPIPreload;
        #endregion

        #region Game Info Methods
        public virtual DeltaPatchProperty? GetDeltaPatchInfo() => GameDeltaPatchProp;

        public virtual List<RegionResourceVersion> GetGameLatestZip(GameInstallStateEnum gameState)
        {
            // Initialize the return list
            List<RegionResourceVersion> returnList                 = [];
            RegionResourceVersion?      currentLatestRegionPackage = GameApiProp?.data?.game?.latest;

            // If the current latest region package is null, then throw
            if (currentLatestRegionPackage == null)
            {
                throw new NullReferenceException("GameApiProp.data.game.latest returns a null!");
            }

            // If the GameVersion is not installed, then return the latest one
            if (gameState is GameInstallStateEnum.NotInstalled or GameInstallStateEnum.GameBroken)
            {
                // Add the latest prop to the return list
                returnList.Add(currentLatestRegionPackage);

                return returnList;
            }

            // Try to get the diff file  by the first or default (null)
            if (GameApiProp?.data?.game?.diffs == null)
            {
                return returnList;
            }

            RegionResourceVersion? diff = GameApiProp.data?.game?.diffs
                                                     .FirstOrDefault(x => x.version == GameVersionInstalled?.VersionString);

            // Return if the diff is null, then get the latest. If found, then return the diff one.
            returnList.Add(diff ?? currentLatestRegionPackage);

            return returnList;
        }

        public virtual List<RegionResourceVersion>? GetGamePreloadZip()
        {
            // Initialize the return list
            List<RegionResourceVersion> returnList = [];

            // If the preload is not exist, then return null
            if (GameApiProp?.data?.pre_download_game == null
                || (GameApiProp.data?.pre_download_game?.diffs?.Count ?? 0) == 0
                && GameApiProp.data?.pre_download_game?.latest == null)
            {
                return null;
            }

            // Try to get the diff file  by the first or default (null)
            RegionResourceVersion? diff = GameApiProp.data?.pre_download_game?.diffs?
               .FirstOrDefault(x => x.version == GameVersionInstalled?.VersionString);

            // If the single entry of the diff is null, then return null
            // If the diff is null, then get the latest.
            // If diff is found, then add the diff one.
            returnList.Add(diff ?? GameApiProp.data?.pre_download_game?.latest ?? throw new NullReferenceException("Preload neither have diff or latest package!"));

            // Return the list
            return returnList;
        }

        public virtual List<RegionResourcePlugin>? GetGamePluginZip()
        {
            // Check if the plugin is not empty, then add it
            if ((GameApiProp?.data?.plugins?.Count ?? 0) != 0)
                return [.. GameApiProp?.data?.plugins!];

            // Return null if plugin is unavailable
            return null;
        }

        public virtual List<RegionResourcePlugin>? GetGameSdkZip()
        {
            // Check if the sdk is not empty, then add it
            if (GameApiProp?.data?.sdk == null)
            {
                return null;
            }

            // Convert the value
            RegionResourcePlugin sdkPlugin = new RegionResourcePlugin
            {
                plugin_id  = "sdk",
                release_id = "sdk",
                version    = GameApiProp.data?.sdk.version,
                package    = GameApiProp.data?.sdk
            };

            // If the package is not null, then add the validation
            if (sdkPlugin.package != null)
            {
                sdkPlugin.package.pkg_version = GameApiProp.data?.sdk?.pkg_version;
            }

            // Return a single list
            return [sdkPlugin];

            // Return null if sdk is unavailable
        }
        #endregion
    }
}