#nullable enable
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using System;
using System.Threading.Tasks;
#if !USEVELOPACK
using Squirrel;
using Squirrel.Sources;
#else
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Locators;
using Velopack.Sources;
#endif
// ReSharper disable CheckNamespace

namespace CollapseLauncher.Helper.Update
{
    internal static class LauncherUpdateHelper
    {
        static LauncherUpdateHelper()
        {
            string? versionString = LauncherConfig.AppCurrentVersionString;
            if (string.IsNullOrEmpty(versionString))
                throw new NullReferenceException("App cannot retrieve the current version of the executable!");

            _launcherCurrentVersion = new GameVersion(versionString);
            _launcherCurrentVersionString = _launcherCurrentVersion.VersionString;
        }

        internal static AppUpdateVersionProp? AppUpdateVersionProp;
        internal static bool IsLauncherUpdateAvailable;

        private static readonly GameVersion _launcherCurrentVersion;
        internal static GameVersion? LauncherCurrentVersion
            => _launcherCurrentVersion;

        private static readonly string _launcherCurrentVersionString;
        internal static string LauncherCurrentVersionString
            => _launcherCurrentVersionString;

        internal static async Task RunUpdateCheckDetached()
        {
            try
            {
                bool isUpdateAvailable = await IsUpdateAvailable();
                LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { IsUpdateAvailable = isUpdateAvailable, NewVersionName = AppUpdateVersionProp?.Version ?? default });
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"The update manager check throws an error, Skipping update check!\r\n{ex}", LogType.Warning, true);
            }
        }

        internal static async Task<bool> IsUpdateAvailable(bool isForceCheckUpdate = false)
        {
            string updateChannel = LauncherConfig.IsPreview ? "preview" : "stable";

            CDNURLProperty launcherUpdatePreferredCdn = FallbackCDNUtil.GetPreferredCDN();
            string? launcherUpdateManagerBaseUrl = ConverterTool.CombineURLFromString(launcherUpdatePreferredCdn.URLPrefix,
#if USEVELOPACK
                "velopack",
                updateChannel
#else
                "squirrel",
                updateChannel
#endif
                );

            // Register the update manager adapter
            IFileDownloader updateManagerHttpAdapter = new UpdateManagerHttpAdapter();
#if USEVELOPACK
            // Initialize update manager logger, locator and options
            ILogger velopackLogger = ILoggerHelper.CreateCollapseILogger();
            VelopackLocator updateManagerLocator = WindowsVelopackLocator.GetDefault(velopackLogger);
            UpdateOptions updateManagerOptions = new UpdateOptions
            {
                AllowVersionDowngrade = true,
                ExplicitChannel = updateChannel
            };

            // Initialize update manager source
            IUpdateSource updateSource = new SimpleWebSource(launcherUpdateManagerBaseUrl, updateManagerHttpAdapter);

            // Initialize the update manager
            UpdateManager updateManager = new UpdateManager(
                updateSource,
                updateManagerOptions,
                velopackLogger,
                updateManagerLocator);

            // Get the update info. If it's null, then return false (no update)
            UpdateInfo? updateInfo = await updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                return false;
            }

            // If there's an update, then get the update metadata
            GameVersion updateVersion = new GameVersion(updateInfo.TargetFullRelease.Version.ToString());
            AppUpdateVersionProp = await GetUpdateMetadata(updateChannel);
            if (AppUpdateVersionProp == null)
            {
                return false;
            }

            // Compare the version
            IsLauncherUpdateAvailable = LauncherCurrentVersion.Compare(updateVersion);

            // Get the status if the update is ignorable or forced update.
            bool isUserIgnoreUpdate = (LauncherConfig.GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false) && !isForceCheckUpdate;
            bool isUpdateRoutineSkipped = isUserIgnoreUpdate && !AppUpdateVersionProp.IsForceUpdate;

            return IsLauncherUpdateAvailable && !isUpdateRoutineSkipped;
#else
            using (UpdateManager updateManager = new UpdateManager(launcherUpdateManagerBaseUrl, null, null, updateManagerHttpAdapter))
            {
                UpdateInfo info = await updateManager.CheckForUpdate();
                if (info == null) return false;

                GameVersion remoteVersion = new GameVersion(info.FutureReleaseEntry.Version.Version);

                AppUpdateVersionProp = await GetUpdateMetadata(updateChannel);
                if (AppUpdateVersionProp == null) return false;

                IsLauncherUpdateAvailable = LauncherCurrentVersion.Compare(remoteVersion);

                bool isUserIgnoreUpdate = (LauncherConfig.GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false) && !isForceCheckUpdate;
                bool isUpdateRoutineSkipped = isUserIgnoreUpdate && !AppUpdateVersionProp.IsForceUpdate;

                return IsLauncherUpdateAvailable && !isUpdateRoutineSkipped;
            }
#endif
        }

        private static async ValueTask<AppUpdateVersionProp?> GetUpdateMetadata(string updateChannel)
        {
            string relativePath = ConverterTool.CombineURLFromString(updateChannel, "fileindex.json");
            await using BridgedNetworkStream ms = await FallbackCDNUtil.TryGetCDNFallbackStream(relativePath);
            return await ms.DeserializeAsync<AppUpdateVersionProp>(InternalAppJSONContext.Default);
        }
    }
}
