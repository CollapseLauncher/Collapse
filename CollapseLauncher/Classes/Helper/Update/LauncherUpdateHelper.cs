using CollapseLauncher.Extension;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using System;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Velopack;
using Velopack.Locators;
using Velopack.Logging;
using Velopack.Sources;
// ReSharper disable UnusedMember.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Helper.Update
{
    internal static class LauncherUpdateHelper
    {
        internal static AppUpdateVersionProp? AppUpdateVersionProp;
        private static  bool                  _isLauncherUpdateAvailable;

        internal static GameVersion? LauncherCurrentVersion => field ??= LauncherConfig.AppCurrentVersionString;

        [field: AllowNull, MaybeNull]
        internal static string LauncherCurrentVersionString => field = LauncherConfig.AppCurrentVersionString;

        internal static async Task RunUpdateCheckDetached()
        {
            try
            {
                bool isUpdateAvailable = await IsUpdateAvailable();
                LauncherUpdateWatcher.GetStatus(new LauncherUpdateProperty { IsUpdateAvailable = isUpdateAvailable, NewVersionName = AppUpdateVersionProp?.Version ?? default });
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"The update manager check throws an error, Skipping update check!\r\n{ex}",
                                    LogType.Warning, true);
                if (!ex.Message.Contains("application which is not installed",
                                         StringComparison.InvariantCultureIgnoreCase)) 
                    await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        internal static async Task<bool> IsUpdateAvailable(bool isForceCheckUpdate = false)
        {
            string updateChannel = LauncherConfig.IsPreview ? "preview" : "stable";

            CDNURLProperty launcherUpdatePreferredCdn = FallbackCDNUtil.GetPreferredCDN();
            string launcherUpdateManagerBaseUrl =
                launcherUpdatePreferredCdn
                   .URLPrefix
                   .CombineURLFromString("velopack", updateChannel);

            // Register the update manager adapter
            IFileDownloader updateManagerHttpAdapter = new UpdateManagerHttpAdapter();
            // Initialize update manager logger, locator and options
            IVelopackLogger? velopackLogger = ILoggerHelper.GetILogger("Velopack").ToVelopackLogger();
            IVelopackLocator updateManagerLocator = VelopackLocator.CreateDefaultForPlatform(velopackLogger);
            UpdateOptions updateManagerOptions = new()
            {
                AllowVersionDowngrade = true,
                ExplicitChannel = updateChannel
            };

            // Initialize update manager source
            IUpdateSource updateSource = new SimpleWebSource(launcherUpdateManagerBaseUrl, updateManagerHttpAdapter);

            // Initialize the update manager
            UpdateManager updateManager = new(updateSource,
                                              updateManagerOptions,
                                              updateManagerLocator);

            // Get the update info. If it's null, then return false (no update)
            UpdateInfo? updateInfo = await updateManager.CheckForUpdatesAsync();
            if (updateInfo == null)
            {
                return false;
            }

            // If there's an update, then get the update metadata
            GameVersion updateVersion = updateInfo.TargetFullRelease.Version.ToString();
            AppUpdateVersionProp = await GetUpdateMetadata(updateChannel);
            if (AppUpdateVersionProp == null)
            {
                return false;
            }

            // Compare the version
            _isLauncherUpdateAvailable = LauncherCurrentVersion < updateVersion;

            // Get the status if the update is ignorable or forced update.
            bool isUserIgnoreUpdate = (LauncherConfig.GetAppConfigValue("DontAskUpdate").ToBoolNullable() ?? false) && !isForceCheckUpdate;
            bool isUpdateRoutineSkipped = isUserIgnoreUpdate && !AppUpdateVersionProp.IsForceUpdate;

            return _isLauncherUpdateAvailable && !isUpdateRoutineSkipped;
        }

        private static async ValueTask<AppUpdateVersionProp?> GetUpdateMetadata(string updateChannel)
        {
            string             relativePath = updateChannel.CombineURLFromString("fileindex.json");
            await using Stream ms           = await FallbackCDNUtil.TryGetCDNFallbackStream(relativePath);
            return await ms.DeserializeAsync(AppUpdateVersionPropJsonContext.Default.AppUpdateVersionProp);
        }
    }
}
