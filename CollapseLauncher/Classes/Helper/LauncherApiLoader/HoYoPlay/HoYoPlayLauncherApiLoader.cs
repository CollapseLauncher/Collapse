using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using TaskExtensions = CollapseLauncher.Extension.TaskExtensions;

#nullable enable
namespace CollapseLauncher.Helper.LauncherApiLoader.Sophon
{
    internal class HoYoPlayLauncherApiLoader : LauncherApiBase, ILauncherApi
    {
        private HoYoPlayLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion) { }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new HoYoPlayLauncherApiLoader(presetConfig, gameName, gameRegion);

        protected override async Task LoadLauncherGameResource(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            // TODO: HoYoPlay API reading and conversion into Sophon format
            EnsurePresetConfigNotNull();
            EnsurePresetConfigNotNull();

            HoYoPlayLauncherResources? hypResourceResponse = await TaskExtensions
                                        .RetryTimeoutAfter(async () => await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherResources>(PresetConfig?.LauncherResourceURL, InternalAppJSONContext.Default, token),
                                                           ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                                        .ConfigureAwait(false);

            HoYoPlayLauncherResources? hypPluginResource = await TaskExtensions
                                        .RetryTimeoutAfter(async () => await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherResources>(PresetConfig?.LauncherPluginURL, InternalAppJSONContext.Default, token),
                                                           ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                                        .ConfigureAwait(false);

            RegionResourceLatest sophonResourceCurrentPackage = new RegionResourceLatest
            {

            };

            RegionResourceLatest sophonResourcePreDownload = new RegionResourceLatest
            {

            };

            RegionResourceGame sophonResourceData = new RegionResourceGame
            {
                pre_download_game = sophonResourcePreDownload,
                game = sophonResourceCurrentPackage
            };

            RegionResourceProp sophonResourcePropRoot = new RegionResourceProp
            {
                data = sophonResourceData
            };

            ConvertPluginResources(sophonResourceData, hypPluginResource);
            ConvertPackageResources(sophonResourceData, hypResourceResponse?.Data?.LauncherPackages);

            base.LauncherGameResource = sophonResourcePropRoot;
        }

        #region Convert Plugin Resources
        private void ConvertPluginResources(RegionResourceGame sophonResourceData, HoYoPlayLauncherResources? hypPluginResources)
        {
            List<LauncherPackages>? hypPluginResourcesList = hypPluginResources?.Data?.PluginPackages;
            if (hypPluginResourcesList == null || hypPluginResourcesList.Count == 0) return;

            List<RegionResourcePlugin> pluginCurrentPackageList = new List<RegionResourcePlugin>();

            foreach (LauncherPackages hypPluginPackage in hypPluginResourcesList)
            {
                RegionResourcePlugin plugin = new RegionResourcePlugin()
                {
                    package = new RegionResourceVersion()
                };
                GuessAssignPluginConversion(plugin, hypPluginPackage);
            }
        }

        private void GuessAssignPluginConversion(RegionResourcePlugin sophonPlugin, LauncherPackages hypPlugin)
        {
            PackagePartition? packagePartition = null;

            if (hypPlugin.MainPackage != null) packagePartition = hypPlugin.MainPackage;
            else if (hypPlugin.PluginPackage != null) packagePartition = hypPlugin.PluginPackage;

            if (packagePartition == null) return;

            // TODO: Implement package conversion
        }
        #endregion

        #region Convert Package Resources
        private void ConvertPackageResources(RegionResourceGame sophonPackageResources, List<LauncherPackages>? hypLauncherPackagesList)
        {
            if (hypLauncherPackagesList == null) throw new NullReferenceException("HoYoPlay package list is null!");

            foreach (LauncherPackages hypRootPackage in hypLauncherPackagesList)
            {
                // Assign main game package
                string? version = hypRootPackage?.MainPackage?.CurrentVersion?.Version;
                List<PackageDetails>? hypMainGamePackageList = hypRootPackage?.MainPackage?.CurrentVersion?.GamePackages;
                if (hypMainGamePackageList != null)
                {
                    // Main game package
                    RegionResourceVersion? sophonMainGamePackage = null;
                    PackageDetails? hypMainGamePackage = hypMainGamePackageList?.FirstOrDefault();
                    if (hypMainGamePackage != null)
                    {
                        sophonMainGamePackage = new RegionResourceVersion
                        {
                            md5 = hypMainGamePackage.PackageMD5Hash,
                            url = hypMainGamePackage.PackageUrl,
                            package_size = hypMainGamePackage.PackageSize ?? 0,
                            size = hypMainGamePackage.PackageDecompressSize,
                            version = version,
                            decompressed_path = hypMainGamePackage.UnpackedBaseUrl,
                            path = hypMainGamePackage.PackageUrl // As fallback for PackageUrl
                        };
                        sophonPackageResources.game.latest = sophonMainGamePackage;

                        // TODO: Add main diff game package download

                        // Main audio package
                        List<PackageDetails>? hypMainAudioDetailList = hypRootPackage?.MainPackage?.CurrentVersion?.AudioPackages;
                        if (hypMainAudioDetailList != null)
                        {
                            sophonMainGamePackage.voice_packs = new List<RegionResourceVersion>();
                            foreach (PackageDetails? hypAudioGamePackage in hypMainAudioDetailList)
                            {
                                sophonMainGamePackage.voice_packs.Add(new RegionResourceVersion
                                {
                                    md5 = hypAudioGamePackage.PackageMD5Hash,
                                    url = hypAudioGamePackage.PackageUrl,
                                    package_size = hypAudioGamePackage.PackageSize ?? 0,
                                    size = hypAudioGamePackage.PackageDecompressSize,
                                    version = version,
                                    decompressed_path = hypAudioGamePackage.UnpackedBaseUrl,
                                    path = hypAudioGamePackage.PackageUrl // As fallback for PackageUrl
                                });
                            }
                        }
                    }
                }

                // Assign preload game package
                string? preloadVersion = hypRootPackage?.PreDownload?.CurrentVersion?.Version;
                List<PackageDetails>? hypPreloadGamePackageList = hypRootPackage?.PreDownload?.CurrentVersion?.GamePackages;
                if (hypPreloadGamePackageList != null)
                {
                    // Preload game package
                    RegionResourceVersion? sophonPreloadGamePackage = null;
                    PackageDetails? hypPreloadGamePackage = hypPreloadGamePackageList?.FirstOrDefault();
                    if (hypPreloadGamePackage != null)
                    {
                        sophonPreloadGamePackage = new RegionResourceVersion
                        {
                            md5 = hypPreloadGamePackage.PackageMD5Hash,
                            url = hypPreloadGamePackage.PackageUrl,
                            package_size = hypPreloadGamePackage.PackageSize ?? 0,
                            size = hypPreloadGamePackage.PackageDecompressSize,
                            version = preloadVersion,
                            decompressed_path = hypPreloadGamePackage.UnpackedBaseUrl,
                            path = hypPreloadGamePackage.PackageUrl // As fallback for PackageUrl
                        };
                        sophonPackageResources.pre_download_game.latest = sophonPreloadGamePackage;

                        // TODO: Add preload diff game package download

                        // Preload audio package
                        List<PackageDetails>? hypPreloadAudioDetailList = hypRootPackage?.PreDownload?.CurrentVersion?.AudioPackages;
                        if (hypPreloadAudioDetailList != null)
                        {
                            sophonPreloadGamePackage.voice_packs = new List<RegionResourceVersion>();
                            foreach (PackageDetails? hypAudioGamePackage in hypPreloadAudioDetailList)
                            {
                                sophonPreloadGamePackage.voice_packs.Add(new RegionResourceVersion
                                {
                                    md5 = hypAudioGamePackage.PackageMD5Hash,
                                    url = hypAudioGamePackage.PackageUrl,
                                    package_size = hypAudioGamePackage.PackageSize ?? 0,
                                    size = hypAudioGamePackage.PackageDecompressSize,
                                    version = version,
                                    decompressed_path = hypAudioGamePackage.UnpackedBaseUrl,
                                    path = hypAudioGamePackage.PackageUrl // As fallback for PackageUrl
                                });
                            }
                        }
                    }
                }
            }
        }
        #endregion

        #region Convert Launcher News
        private void EnsureInitializeSophonLauncherNews(ref LauncherGameNews? sophonLauncherNewsData)
        {
            if (sophonLauncherNewsData == null)
                sophonLauncherNewsData = new LauncherGameNews();

            if (sophonLauncherNewsData.Content == null)
                sophonLauncherNewsData.Content = new LauncherGameNewsData();

            if (sophonLauncherNewsData.Content.Background == null)
                sophonLauncherNewsData.Content.Background = new LauncherGameNewsBackground();
        }

        private void ConvertLauncherBackground(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            if (string.IsNullOrEmpty(hypLauncherInfoData?.BackgroundImageUrl)) return;

            if (sophonLauncherNewsData?.Content?.Background == null) return;
            sophonLauncherNewsData.Content.Background.BackgroundImg = hypLauncherInfoData?.BackgroundImageUrl;
        }
        #endregion

        protected override async ValueTask LoadLauncherNews(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            HoYoPlayLauncherNews? hypLauncherBackground = await TaskExtensions
                                        .RetryTimeoutAfter(async () => await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherNews>(PresetConfig?.LauncherSpriteURL, InternalAppJSONContext.Default, token),
                                                           ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                                        .ConfigureAwait(false);

            HoYoPlayLauncherNews? hypLauncherNews = await TaskExtensions
                                        .RetryTimeoutAfter(async () => await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherNews>(PresetConfig?.LauncherNewsURL, InternalAppJSONContext.Default, token),
                                                           ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token)
                                        .ConfigureAwait(false);

            // Merge background image
            if (hypLauncherBackground?.Data?.GameInfoList != null && hypLauncherNews?.Data != null)
                hypLauncherNews.Data.GameInfoList = hypLauncherBackground?.Data?.GameInfoList;

            LauncherGameNews? sophonLauncherNewsRoot = null;
            EnsureInitializeSophonLauncherNews(ref sophonLauncherNewsRoot);
            ConvertLauncherBackground(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);

            base.LauncherGameNews = sophonLauncherNewsRoot;
        }
    }
}
