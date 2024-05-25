﻿using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypResourceResponseCallback =
                new ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?>(async (innerToken) =>
                await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherResources>(PresetConfig?.LauncherResourceURL, InternalAppJSONContext.Default, innerToken));

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypPluginResourceCallback =
                new ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?>(async (innerToken) =>
                await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherResources>(PresetConfig?.LauncherPluginURL, InternalAppJSONContext.Default, innerToken));

            HoYoPlayLauncherResources? hypResourceResponse = await hypResourceResponseCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token).ConfigureAwait(false);
            HoYoPlayLauncherResources? hypPluginResource = await hypPluginResourceCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token).ConfigureAwait(false);

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

            ConvertPluginResources(ref sophonResourceData, hypPluginResource);
            ConvertPackageResources(sophonResourceData, hypResourceResponse?.Data?.LauncherPackages);

            base.LauncherGameResource = sophonResourcePropRoot;
        }

        #region Convert Plugin Resources
        private void ConvertPluginResources(ref RegionResourceGame sophonResourceData, HoYoPlayLauncherResources? hypPluginResources)
        {
            LauncherPackages? hypPluginPackage = hypPluginResources?.Data?
                .PluginPackages?
                .FirstOrDefault(x => x.GameDetail?
                    .GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (hypPluginPackage == null) return;

            List<RegionResourcePlugin> pluginCurrentPackageList = new List<RegionResourcePlugin>();
            RegionResourcePlugin plugin = new RegionResourcePlugin()
            {
                package = new RegionResourceVersion()
            };
            GuessAssignPluginConversion(pluginCurrentPackageList, hypPluginPackage);
            sophonResourceData.plugins = pluginCurrentPackageList;
        }

        private void GuessAssignPluginConversion(List<RegionResourcePlugin> sophonPluginList, LauncherPackages hypPlugin)
        {
            List<PackagePluginSections>? pluginSectionsList = hypPlugin.PluginPackageSections;
            if ((pluginSectionsList?.Count ?? 0) == 0) return;
            if (pluginSectionsList == null) return;

            foreach (PackagePluginSections firstPluginSection in pluginSectionsList)
            {
                if (firstPluginSection == null) continue;

                RegionResourcePlugin sophonPlugin = new RegionResourcePlugin();
                sophonPlugin.version = firstPluginSection.Version;
                // sophonPlugin.plugin_id = firstPluginSection.PluginId;
                sophonPlugin.package = new RegionResourceVersion
                {
                    validate = firstPluginSection.PluginPackage?.PackageAssetValidationList,
                    // channel_id = firstPluginSection.PluginId,
                    md5 = firstPluginSection.PluginPackage?.PackageMD5Hash,
                    url = firstPluginSection.PluginPackage?.PackageUrl,
                    path = firstPluginSection.PluginPackage?.PackageUrl,
                    size = firstPluginSection.PluginPackage?.PackageDecompressSize ?? 0,
                    package_size = firstPluginSection.PluginPackage?.PackageSize ?? 0
                };

                sophonPluginList.Add(sophonPlugin);
            }
        }
        #endregion

        #region Convert Package Resources
        private void ConvertPackageResources(RegionResourceGame sophonPackageResources, List<LauncherPackages>? hypLauncherPackagesList)
        {
            if (hypLauncherPackagesList == null) throw new NullReferenceException("HoYoPlay package list is null!");

            foreach (LauncherPackages hypRootPackage in hypLauncherPackagesList
                .Where(x => x.GameDetail?.GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                // Assign and convert main game package (latest)
                PackageResourceSections? hypMainPackageSection = hypRootPackage?.MainPackage?.CurrentVersion;
                RegionResourceVersion sophonMainPackageSection = new RegionResourceVersion();
                if (hypMainPackageSection != null)
                    ConvertHYPSectionToResourceVersion(ref hypMainPackageSection, ref sophonMainPackageSection);
                sophonPackageResources.game.latest = sophonMainPackageSection;

                // Assign and convert main game package (diff)
                if (hypRootPackage?.MainPackage?.Patches != null)
                {
                    sophonPackageResources.game.diffs = new List<RegionResourceVersion>();
                    foreach (PackageResourceSections hypMainDiffPackageSection in hypRootPackage.MainPackage.Patches)
                    {
                        if (hypMainDiffPackageSection != null)
                        {
                            PackageResourceSections hypMainDiffPackageSectionRef = hypMainDiffPackageSection;
                            RegionResourceVersion sophonResourceVersion = new RegionResourceVersion();
                            ConvertHYPSectionToResourceVersion(ref hypMainDiffPackageSectionRef, ref sophonResourceVersion);
                            sophonPackageResources.game.diffs.Add(sophonResourceVersion);
                        }
                    }
                }

                // Assign and convert preload game package (latest)
                PackageResourceSections? hypPreloadPackageSection = hypRootPackage?.PreDownload?.CurrentVersion;
                RegionResourceVersion sophonPreloadPackageSection = new RegionResourceVersion();
                if (hypPreloadPackageSection != null)
                    ConvertHYPSectionToResourceVersion(ref hypPreloadPackageSection, ref sophonPreloadPackageSection);
                sophonPackageResources.pre_download_game.latest = sophonPreloadPackageSection;

                // Assign and convert preload game package (diff)
                if (hypRootPackage?.PreDownload?.Patches != null)
                {
                    sophonPackageResources.pre_download_game.diffs = new List<RegionResourceVersion>();
                    foreach (PackageResourceSections hypPreloadDiffPackageSection in hypRootPackage.PreDownload.Patches)
                    {
                        if (hypPreloadDiffPackageSection != null)
                        {
                            PackageResourceSections hypPreloadDiffPackageSectionRef = hypPreloadDiffPackageSection;
                            RegionResourceVersion sophonResourceVersion = new RegionResourceVersion();
                            ConvertHYPSectionToResourceVersion(ref hypPreloadDiffPackageSectionRef, ref sophonResourceVersion);
                            sophonPackageResources.pre_download_game.diffs.Add(sophonResourceVersion);
                        }
                    }
                }
            }
        }

        private void ConvertHYPSectionToResourceVersion(ref PackageResourceSections hypPackageResourceSection, ref RegionResourceVersion sophonResourceVersion)
        {
            // Convert game packages
            RegionResourceVersion packagesVersion = new RegionResourceVersion();
            DelegatePackageResConversionMode(ref packagesVersion, hypPackageResourceSection?.GamePackages, hypPackageResourceSection?.AudioPackages, hypPackageResourceSection?.Version, hypPackageResourceSection?.ResourceListUrl);
            sophonResourceVersion = packagesVersion;
        }

        private delegate void PackageResConversionModeDelegate(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails> hypPackageList,
            string? version, string? resourceListUrl);

        private void DelegatePackageResConversionMode(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails>? hypGamePackageList, List<PackageDetails>? hypAudioPackageList,
            string? version, string? resourceListUrl)
        {
            // If the main package list is not null or empty, then process
            if (hypGamePackageList != null && hypGamePackageList.Count != 0)
            {
                // Delegate the conversion mode for the resource, then process it
                PackageResConversionModeDelegate conversionDelegate = hypGamePackageList.Count > 1 ? ConvertMultiPackageResource : ConvertSinglePackageResource;
                conversionDelegate(ref sophonPackageVersion, hypGamePackageList, version, resourceListUrl);
            }

            // If the audio package list is not null or empty, then process
            if (hypAudioPackageList != null && hypAudioPackageList.Count != 0)
            {
                sophonPackageVersion.voice_packs = new List<RegionResourceVersion>();
                foreach (PackageDetails hypAudioPackage in hypAudioPackageList)
                {
                    sophonPackageVersion.voice_packs.Add(new RegionResourceVersion
                    {
                        url = hypAudioPackage.PackageUrl,
                        path = hypAudioPackage.PackageUrl, // As fallback for PackageUrl
                        size = hypAudioPackage.PackageDecompressSize,
                        package_size = hypAudioPackage.PackageSize ?? 0,
                        md5 = hypAudioPackage.PackageMD5Hash,
                        language = hypAudioPackage.Language
                    });
                }
            }
        }

        private void ConvertSinglePackageResource(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails> hypPackageList,
            string? version, string? resourceListUrl)
        {
            PackageDetails packageDetail = hypPackageList[0];
            sophonPackageVersion.url = packageDetail.PackageUrl;
            sophonPackageVersion.path = packageDetail.PackageUrl; // As fallback for PackageUrl
            sophonPackageVersion.size = packageDetail.PackageDecompressSize;
            sophonPackageVersion.package_size = packageDetail.PackageSize ?? 0;
            sophonPackageVersion.md5 = packageDetail.PackageMD5Hash;
            sophonPackageVersion.entry = PresetConfig?.GameExecutableName;
            sophonPackageVersion.version = version;
            sophonPackageVersion.decompressed_path = resourceListUrl;
        }

        private void ConvertMultiPackageResource(ref RegionResourceVersion sophonPackageVersion, List<PackageDetails> hypPackageList,
            string? version, string? resourceListUrl)
        {
            long totalSize = hypPackageList.Sum(x => x.PackageDecompressSize);
            long totalPackageSize = hypPackageList.Sum(x => x.PackageSize ?? 0);

            sophonPackageVersion.url = string.Empty;
            sophonPackageVersion.path = string.Empty; // As fallback for PackageUrl
            sophonPackageVersion.md5 = string.Empty;
            sophonPackageVersion.size = totalSize;
            sophonPackageVersion.package_size = totalPackageSize;
            sophonPackageVersion.entry = PresetConfig?.GameExecutableName;
            sophonPackageVersion.version = version;
            sophonPackageVersion.decompressed_path = resourceListUrl;

            sophonPackageVersion.segments = new List<RegionResourceVersion>();

            foreach (PackageDetails packageDetail in hypPackageList)
            {
                sophonPackageVersion.segments.Add(new RegionResourceVersion
                {
                    url = packageDetail.PackageUrl,
                    path = packageDetail.PackageUrl, // As fallback for PackageUrl
                    size = packageDetail.PackageDecompressSize,
                    package_size = packageDetail.PackageSize ?? 0,
                    md5 = packageDetail.PackageMD5Hash
                });
            }
        }
        #endregion

        protected override async ValueTask LoadLauncherNews(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            bool isUseMultiLang = PresetConfig?.LauncherSpriteURLMultiLang ?? false;

            string localeCode = isUseMultiLang ? Locale.Lang.LanguageID.ToLower() : PresetConfig?.LauncherSpriteURLMultiLangFallback!;
            string launcherSpriteUrl = string.Format(PresetConfig?.LauncherSpriteURL!, localeCode);
            string launcherNewsUrl = string.Format(PresetConfig?.LauncherNewsURL!, localeCode);

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?> hypLauncherBackgroundCallback =
                new ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?>(async (innerToken) =>
                await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherNews>(launcherSpriteUrl, InternalAppJSONContext.Default, innerToken));

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?> hypLauncherNewsCallback =
                new ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?>(async (innerToken) =>
                await FallbackCDNUtil.DownloadAsJSONType<HoYoPlayLauncherNews>(launcherNewsUrl, InternalAppJSONContext.Default, innerToken));

            HoYoPlayLauncherNews? hypLauncherBackground = await hypLauncherBackgroundCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token).ConfigureAwait(false);
            HoYoPlayLauncherNews? hypLauncherNews = await hypLauncherNewsCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token).ConfigureAwait(false);

            // Merge background image
            if (hypLauncherBackground?.Data?.GameInfoList != null && hypLauncherNews?.Data != null)
                hypLauncherNews.Data.GameInfoList = hypLauncherBackground?.Data?.GameInfoList;

            LauncherGameNews? sophonLauncherNewsRoot = null;
            EnsureInitializeSophonLauncherNews(ref sophonLauncherNewsRoot);
            ConvertLauncherBackground(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);
            ConvertLauncherNews(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);
            ConvertLauncherSocialMedia(ref sophonLauncherNewsRoot, hypLauncherNews?.Data);

            base.LauncherGameNews = sophonLauncherNewsRoot;
            base.LauncherGameNews?.Content?.InjectDownloadableItemCancelToken(token);
        }

        #region Convert Launcher News
        private void EnsureInitializeSophonLauncherNews(ref LauncherGameNews? sophonLauncherNewsData)
        {
            if (sophonLauncherNewsData == null)
                sophonLauncherNewsData = new LauncherGameNews();

            if (sophonLauncherNewsData.Content == null)
                sophonLauncherNewsData.Content = new LauncherGameNewsData();

            if (sophonLauncherNewsData.Content.Background == null)
                sophonLauncherNewsData.Content.Background = new LauncherGameNewsBackground();

            if (sophonLauncherNewsData.Content.NewsPost == null)
                sophonLauncherNewsData.Content.NewsPost = new List<LauncherGameNewsPost>();

            if (sophonLauncherNewsData.Content.NewsCarousel == null)
                sophonLauncherNewsData.Content.NewsCarousel = new List<LauncherGameNewsCarousel>();

            if (sophonLauncherNewsData.Content.SocialMedia == null)
                sophonLauncherNewsData.Content.SocialMedia = new List<LauncherGameNewsSocialMedia>();
        }

        private void ConvertLauncherBackground(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            if (string.IsNullOrEmpty(hypLauncherInfoData?.BackgroundImageUrl)) return;

            if (sophonLauncherNewsData?.Content?.Background == null) return;
            sophonLauncherNewsData.Content.Background.BackgroundImg = hypLauncherInfoData?.BackgroundImageUrl;
            sophonLauncherNewsData.Content.Background.FeaturedEventIconBtnImg = hypLauncherInfoData?.FeaturedEventIconUrl;
            sophonLauncherNewsData.Content.Background.FeaturedEventIconBtnUrl = hypLauncherInfoData?.FeaturedEventIconClickLink;
        }

        private void ConvertLauncherNews(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            int index = 0;
            if (hypLauncherInfoData?.GameNewsContent?.NewsPostsList != null)
            {
                // Post
                foreach (LauncherContentData hypPostData in hypLauncherInfoData.GameNewsContent.NewsPostsList)
                {
                    sophonLauncherNewsData?.Content?.NewsPost?.Add(new LauncherGameNewsPost()
                    {
                        PostDate = hypPostData.Date,
                        PostUrl = hypPostData.ClickLink,
                        PostId = string.Empty,
                        PostOrder = index++,
                        PostType = hypPostData.ContentType,
                        Title = hypPostData.Title
                    });
                }
            }

            index = 0;
            if (hypLauncherInfoData?.GameNewsContent?.NewsCarouselList != null)
            {
                // Carousel
                foreach (LauncherNewsBanner hypCarouselData in hypLauncherInfoData.GameNewsContent.NewsCarouselList)
                {
                    sophonLauncherNewsData?.Content?.NewsCarousel?.Add(new LauncherGameNewsCarousel
                    {
                        CarouselId = hypCarouselData.Id,
                        CarouselOrder = index++,
                        CarouselImg = hypCarouselData.Image?.ImageUrl,
                        CarouselUrl = hypCarouselData.Image?.ClickLink,
                        CarouselTitle = hypCarouselData.Image?.Title
                    });
                }
            }
        }

        private void ConvertLauncherSocialMedia(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            if (hypLauncherInfoData?.GameNewsContent?.SocialMediaList != null)
            {
                // Social Media list
                foreach (LauncherSocialMedia hypSocMedData in hypLauncherInfoData.GameNewsContent.SocialMediaList)
                {
                    sophonLauncherNewsData?.Content?.SocialMedia?.Add(new LauncherGameNewsSocialMedia
                    {
                        IconId = hypSocMedData?.SocialMediaId,
                        IconImg = hypSocMedData?.SocialMediaIcon?.ImageUrl,
                        IconImgHover = string.IsNullOrEmpty(hypSocMedData?.SocialMediaIcon?.ImageHoverUrl) ? hypSocMedData?.SocialMediaIcon?.ImageUrl : hypSocMedData?.SocialMediaIcon?.ImageHoverUrl,
                        Title = hypSocMedData?.SocialMediaIcon?.Title,
                        SocialMediaUrl = hypSocMedData?.SocialMediaLinks?.FirstOrDefault()?.ClickLink,
                        QrImg = hypSocMedData?.SocialMediaQrImage?.ImageUrl,
                        QrTitle = hypSocMedData?.SocialMediaQrDescription,
                        QrLinks = hypSocMedData?.SocialMediaLinks?.Select(x =>
                        {
                            LauncherGameNewsSocialMediaQrLinks qrLink = new LauncherGameNewsSocialMediaQrLinks
                            {
                                Title = x.Title,
                                Url = x.ClickLink
                            };
                            return qrLink;
                        }).ToList()
                    });
                }
            }
        }
        #endregion
    }
}
