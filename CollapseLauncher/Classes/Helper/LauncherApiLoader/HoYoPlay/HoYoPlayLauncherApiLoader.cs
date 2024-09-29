#nullable enable
    using CollapseLauncher.Extension;
    using CollapseLauncher.Helper.LauncherApiLoader.Sophon;
    using CollapseLauncher.Helper.Metadata;
    using Hi3Helper;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
// ReSharper disable IdentifierTypo

    // ReSharper disable once CheckNamespace
namespace CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay
{
    internal class HoYoPlayLauncherApiLoader : LauncherApiBase, ILauncherApi
    {
        private HoYoPlayLauncherApiLoader(PresetConfig presetConfig, string gameName, string gameRegion)
            : base(presetConfig, gameName, gameRegion) { }

        public static ILauncherApi CreateApiInstance(PresetConfig presetConfig, string gameName, string gameRegion)
            => new HoYoPlayLauncherApiLoader(presetConfig, gameName, gameRegion);

        protected override async Task LoadLauncherGameResource(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            EnsurePresetConfigNotNull();

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypResourceResponseCallback =
                async innerToken =>
                    await FallbackCDNUtil.DownloadAsJSONType(PresetConfig?.LauncherResourceURL, InternalAppJSONContext.Default.HoYoPlayLauncherResources, innerToken);

            HoYoPlayLauncherResources? hypResourceResponse = await hypResourceResponseCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token);

            HoYoPlayLauncherResources? hypPluginResource = null;
            HoYoPlayLauncherResources? hypSdkResource = null;
            if (!string.IsNullOrEmpty(PresetConfig?.LauncherPluginURL) && (PresetConfig.IsPluginUpdateEnabled ?? false))
            {
                ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypPluginResourceCallback =
                    async innerToken =>
                        await FallbackCDNUtil.DownloadAsJSONType(PresetConfig?.LauncherPluginURL, InternalAppJSONContext.Default.HoYoPlayLauncherResources, innerToken);

                hypPluginResource = await hypPluginResourceCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                  ExecutionTimeoutAttempt, onTimeoutRoutine, token);
            }

            if (!string.IsNullOrEmpty(PresetConfig?.LauncherGameChannelSDKURL))
            {
                ActionTimeoutValueTaskCallback<HoYoPlayLauncherResources?> hypSdkResourceCallback =
                    async innerToken =>
                        await FallbackCDNUtil.DownloadAsJSONType(PresetConfig?.LauncherGameChannelSDKURL, InternalAppJSONContext.Default.HoYoPlayLauncherResources, innerToken);

                hypSdkResource = await hypSdkResourceCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                               ExecutionTimeoutAttempt, onTimeoutRoutine, token);
            }

            RegionResourceLatest sophonResourceCurrentPackage = new RegionResourceLatest();

            RegionResourceGame sophonResourceData = new RegionResourceGame
            {
                game = sophonResourceCurrentPackage
            };

            RegionResourceProp sophonResourcePropRoot = new RegionResourceProp
            {
                data = sophonResourceData
            };

            ConvertPluginResources(ref sophonResourceData, hypPluginResource);
            ConvertSdkResources(ref sophonResourceData, hypSdkResource);
            ConvertPackageResources(sophonResourceData, hypResourceResponse?.Data?.LauncherPackages);
            
            base.LauncherGameResource = sophonResourcePropRoot;

            PerformDebugRoutines();
        }

        #region Convert Sdk Resources
        private void ConvertSdkResources(ref RegionResourceGame sophonResourceData, HoYoPlayLauncherResources? hypSdkResources)
        {
            LauncherSdkPackages? sdkPackages = hypSdkResources?.Data?.ChannelSdks?
                .FirstOrDefault(x => x.GameDetail?
                    .GameBiz?
                    .Equals(PresetConfig?.LauncherBizName, StringComparison.OrdinalIgnoreCase) ?? false);

            if (sdkPackages == null) return;
            if (sdkPackages.SdkPackageDetail == null) return;

            sophonResourceData.sdk = new RegionResourceVersion
            {
                md5 = sdkPackages.SdkPackageDetail.PackageMD5Hash,
                size = sdkPackages.SdkPackageDetail.PackageDecompressSize,
                package_size = sdkPackages.SdkPackageDetail.PackageSize ?? 0,
                path = sdkPackages.SdkPackageDetail.PackageUrl,
                url = sdkPackages.SdkPackageDetail.PackageUrl,
                version = sdkPackages.Version,
                pkg_version = sdkPackages.PkgVersionFileName
            };
        }
        #endregion

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
                RegionResourcePlugin sophonPlugin = new RegionResourcePlugin();
                sophonPlugin.version = firstPluginSection.Version;
                sophonPlugin.plugin_id = firstPluginSection.PluginId;
                sophonPlugin.release_id = firstPluginSection.ReleaseId;
                sophonPlugin.package = new RegionResourceVersion
                {
                    validate = firstPluginSection.PluginPackage?.PackageAssetValidationList,
                    md5 = firstPluginSection.PluginPackage?.PackageMD5Hash,
                    url = firstPluginSection.PluginPackage?.PackageUrl,
                    path = firstPluginSection.PluginPackage?.PackageUrl,
                    size = firstPluginSection.PluginPackage?.PackageDecompressSize ?? 0,
                    package_size = firstPluginSection.PluginPackage?.PackageSize ?? 0,
                    run_command = firstPluginSection.PluginPackage?.PackageRunCommand,
                    version = firstPluginSection.Version
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
                PackageResourceSections? hypMainPackageSection    = hypRootPackage.MainPackage?.CurrentVersion;
                RegionResourceVersion    sophonMainPackageSection = new RegionResourceVersion();
                if (hypMainPackageSection != null)
                    ConvertHYPSectionToResourceVersion(ref hypMainPackageSection, ref sophonMainPackageSection);

                // If the main game package reference is null, skip it
                if (sophonPackageResources.game == null)
                    continue;

                sophonPackageResources.game.latest = sophonMainPackageSection;

                // Assign and convert main game package (diff)
                if (hypRootPackage.MainPackage?.Patches != null)
                {
                    sophonPackageResources.game.diffs = new List<RegionResourceVersion>();
                    foreach (PackageResourceSections hypMainDiffPackageSection in hypRootPackage.MainPackage.Patches)
                    {
                        PackageResourceSections hypMainDiffPackageSectionRef = hypMainDiffPackageSection;
                        RegionResourceVersion   sophonResourceVersion        = new RegionResourceVersion();
                        ConvertHYPSectionToResourceVersion(ref hypMainDiffPackageSectionRef, ref sophonResourceVersion);
                        sophonPackageResources.game.diffs.Add(sophonResourceVersion);
                    }
                }
                sophonPackageResources.pre_download_game = new RegionResourceLatest();

                // Convert if preload entry is not empty or null
                if (hypRootPackage.PreDownload?.CurrentVersion != null || (hypRootPackage.PreDownload?.Patches?.Count ?? 0) != 0)
                {

                    // Assign and convert preload game package (latest)
                    PackageResourceSections? hypPreloadPackageSection = hypRootPackage.PreDownload?.CurrentVersion;
                    if (hypPreloadPackageSection != null)
                    {
                        RegionResourceVersion sophonPreloadPackageSection = new RegionResourceVersion();
                        ConvertHYPSectionToResourceVersion(ref hypPreloadPackageSection, ref sophonPreloadPackageSection);
                        sophonPackageResources.pre_download_game.latest = sophonPreloadPackageSection;
                    }

                    // Assign and convert preload game package (diff)
                    if (hypRootPackage.PreDownload?.Patches == null || hypRootPackage.PreDownload.Patches.Count == 0)
                    {
                        continue;
                    }

                    sophonPackageResources.pre_download_game.diffs = new List<RegionResourceVersion>();
                    foreach (PackageResourceSections hypPreloadDiffPackageSection in hypRootPackage.PreDownload
                                .Patches)
                    {
                        PackageResourceSections hypPreloadDiffPackageSectionRef = hypPreloadDiffPackageSection;
                        RegionResourceVersion   sophonResourceVersion           = new RegionResourceVersion();
                        ConvertHYPSectionToResourceVersion(ref hypPreloadDiffPackageSectionRef,
                                                           ref sophonResourceVersion);
                        sophonPackageResources.pre_download_game.diffs.Add(sophonResourceVersion);
                    }
                }
            }
        }

        private void ConvertHYPSectionToResourceVersion(ref PackageResourceSections hypPackageResourceSection, ref RegionResourceVersion sophonResourceVersion)
        {
            if (hypPackageResourceSection == null)
            {
                throw new ArgumentNullException(nameof(hypPackageResourceSection));
            }

            if (sophonResourceVersion == null)
            {
                throw new ArgumentNullException(nameof(sophonResourceVersion));
            }

            // Convert game packages
            RegionResourceVersion packagesVersion = new RegionResourceVersion();
            DelegatePackageResConversionMode(ref packagesVersion, hypPackageResourceSection.GamePackages, hypPackageResourceSection.AudioPackages, hypPackageResourceSection.Version, hypPackageResourceSection.ResourceListUrl);
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
                async innerToken =>
                    await FallbackCDNUtil.DownloadAsJSONType(launcherSpriteUrl, InternalAppJSONContext.Default.HoYoPlayLauncherNews, innerToken);

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherNews?> hypLauncherNewsCallback =
                async innerToken =>
                    await FallbackCDNUtil.DownloadAsJSONType(launcherNewsUrl, InternalAppJSONContext.Default.HoYoPlayLauncherNews, innerToken);

            HoYoPlayLauncherNews? hypLauncherBackground = await hypLauncherBackgroundCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token);
            HoYoPlayLauncherNews? hypLauncherNews = await hypLauncherNewsCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token);

            // Merge background image
            if (hypLauncherBackground?.Data?.GameInfoList != null && hypLauncherNews?.Data != null)
                hypLauncherNews.Data.GameInfoList = hypLauncherBackground.Data?.GameInfoList;

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
            sophonLauncherNewsData.Content.Background.BackgroundImg           = hypLauncherInfoData.BackgroundImageUrl;
            sophonLauncherNewsData.Content.Background.FeaturedEventIconBtnImg = hypLauncherInfoData.FeaturedEventIconUrl;
            sophonLauncherNewsData.Content.Background.FeaturedEventIconBtnUrl = hypLauncherInfoData.FeaturedEventIconClickLink;
        }

        private void ConvertLauncherNews(ref LauncherGameNews? sophonLauncherNewsData, LauncherInfoData? hypLauncherInfoData)
        {
            int index = 0;
            if (hypLauncherInfoData?.GameNewsContent?.NewsPostsList != null)
            {
                // Post
                foreach (LauncherContentData hypPostData in hypLauncherInfoData.GameNewsContent.NewsPostsList)
                {
                    sophonLauncherNewsData?.Content?.NewsPost?.Add(new LauncherGameNewsPost
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
                        IconId       = hypSocMedData.SocialMediaId,
                        IconImg      = hypSocMedData.SocialMediaIcon?.ImageUrl,
                        IconImgHover = string.IsNullOrEmpty(hypSocMedData.SocialMediaIcon?.ImageHoverUrl) ? hypSocMedData.SocialMediaIcon?.ImageUrl : hypSocMedData.SocialMediaIcon?.ImageHoverUrl,
                        Title        = hypSocMedData.SocialMediaIcon?.Title,
                        SocialMediaUrl = (hypSocMedData.SocialMediaLinks?.Count ?? 0) != 0 ?
                            hypSocMedData.SocialMediaLinks?.FirstOrDefault()?.ClickLink :
                            hypSocMedData.SocialMediaIcon?.ClickLink,
                        QrImg   = hypSocMedData.SocialMediaQrImage?.ImageUrl,
                        QrTitle = hypSocMedData.SocialMediaQrDescription,
                        QrLinks = hypSocMedData.SocialMediaLinks?.Select(x =>
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

        protected override async ValueTask LoadLauncherGameInfo(ActionOnTimeOutRetry? onTimeoutRoutine, CancellationToken token)
        {
            if (PresetConfig?.LauncherGameInfoDisplayURL == null)
            {
                base.LauncherGameInfoField = new HoYoPlayGameInfoField();
                return;
            }

            bool isUseMultiLang = PresetConfig?.LauncherSpriteURLMultiLang ?? false;

            string localeCode = isUseMultiLang ? Locale.Lang.LanguageID.ToLower() : PresetConfig?.LauncherSpriteURLMultiLangFallback!;
            string launcherGameInfoUrl = string.Format(PresetConfig?.LauncherGameInfoDisplayURL!, localeCode);

            ActionTimeoutValueTaskCallback<HoYoPlayLauncherGameInfo?> hypLauncherGameInfoCallback =
                async innerToken =>
                    await FallbackCDNUtil.DownloadAsJSONType(launcherGameInfoUrl, InternalAppJSONContext.Default.HoYoPlayLauncherGameInfo, innerToken);

            HoYoPlayLauncherGameInfo? hypLauncherGameInfo = await hypLauncherGameInfoCallback.WaitForRetryAsync(ExecutionTimeout, ExecutionTimeoutStep,
                                                           ExecutionTimeoutAttempt, onTimeoutRoutine, token);

            HoYoPlayGameInfoField? sophonLauncherGameInfoRoot = new HoYoPlayGameInfoField();
            if (hypLauncherGameInfo != null)
            {
                ConvertGameInfoResources(ref sophonLauncherGameInfoRoot, hypLauncherGameInfo?.GameInfoData);

                base.LauncherGameInfoField = sophonLauncherGameInfoRoot ?? new HoYoPlayGameInfoField();
            }
        }

        #region Convert Game Info Resources
        private void ConvertGameInfoResources([DisallowNull] ref HoYoPlayGameInfoField? sophonGameInfo, HoYoPlayGameInfoData? hypLauncherGameInfoList)
        {
            if (sophonGameInfo == null)
            {
                throw new ArgumentNullException(nameof(sophonGameInfo));
            }

            if (hypLauncherGameInfoList == null)
            {
                throw new ArgumentNullException(nameof(hypLauncherGameInfoList));
            }

            sophonGameInfo = hypLauncherGameInfoList.Data?.FirstOrDefault(x => x.BizName?.Equals(PresetConfig?.LauncherBizName) ?? false);
        }
        #endregion
    }
}
