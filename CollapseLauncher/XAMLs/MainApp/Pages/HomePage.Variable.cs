using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.Pages
{
    public sealed partial class HomePage
    {
        private string GameDirPath
        {
            get => CurrentGameProperty.GameVersion?.GameDirPath ?? throw new NullReferenceException();
        }

        private static ILauncherApi? CurrentGameLauncherApi
        {
            get => LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi;
        }

        private static HypLauncherBackgroundList? GameBackgroundData
        {
            get => CurrentGameLauncherApi?.LauncherGameBackground?.Data;
        }

        private static HypLauncherContentKind? GameContentData
        {
            get => CurrentGameLauncherApi?.LauncherGameContent?.Data?.Content;
        }

        internal static List<HypLauncherSocialMediaContentData>? GameSocialMediaData
        {
            get => GameContentData?.SocialMedia;
        }

        private static List<HypLauncherMediaContentData>? GameNewsDataAll
        {
            get => GameContentData?.News;
        }

        internal static List<HypLauncherMediaContentData>? GameNewsDataEventKind
        {
            get => GameContentData?.NewsEventKind;
        }

        internal static List<HypLauncherMediaContentData>? GameNewsDataAnnouncementKind
        {
            get => GameContentData?.NewsAnnouncementKind;
        }

        internal static List<HypLauncherMediaContentData>? GameNewsDataInformationKind
        {
            get => GameContentData?.NewsInformationKind;
        }

        internal static List<HypLauncherCarouselContentData>? GameCarouselData
        {
            get => GameContentData?.Carousel;
        }

        private static HypGameInfoData? GameInfoDisplayField
        {
            get => CurrentGameLauncherApi?.LauncherGameInfoField;
        }

        private static bool IsGameStatusPreRegister
        {
            get =>
                GameInfoDisplayField?.DisplayStatus ==
                LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_RESERVATION_ENABLED;
        }

        private static bool IsGameStatusComingSoon
        {
            get =>
                GameInfoDisplayField?.DisplayStatus ==
                LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_COMING_SOON;
        }

        internal static string? GamePreRegisterLink
        {
            get => GameInfoDisplayField?.ReservationLink?.ClickLink;
        }

        internal static Visibility IsPostEventPanelVisible  => GameNewsDataEventKind?.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostEventPanelEmpty    => GameNewsDataEventKind?.Count != 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostNoticePanelVisible => GameNewsDataAnnouncementKind?.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostNoticePanelEmpty   => GameNewsDataAnnouncementKind?.Count != 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostInfoPanelVisible   => GameNewsDataInformationKind?.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        internal static Visibility IsPostInfoPanelEmpty     => GameNewsDataInformationKind?.Count != 0 ? Visibility.Collapsed : Visibility.Visible;

        internal static Visibility IsPostInfoPanelAllEmpty  =>
            IsPostEventPanelVisible == Visibility.Collapsed
            && IsPostNoticePanelVisible == Visibility.Collapsed
            && IsPostInfoPanelVisible == Visibility.Collapsed ? Visibility.Collapsed : Visibility.Visible;

        internal static int PostEmptyMascotTextWidth => Locale.Lang._HomePage.PostPanel_NoNews.Length > 30 ? 200 : 100;

        internal static Visibility CommunityToolsButtonVisibility
        {
            get => !IsCommunityToolsOfficialAvailable &&
                   !IsCommunityToolsCommunityAvailable ? Visibility.Collapsed : Visibility.Visible;
        }

        internal static Visibility CommunityToolsOfficialGridVisibility
        {
            get => !IsCommunityToolsOfficialAvailable ? Visibility.Collapsed : Visibility.Visible;
        }

        private static bool IsCommunityToolsOfficialAvailable
        {
            get => (PageStatics.CommunityToolsProperty?.OfficialToolsList?.Count ?? 0) != 0;
        }

        internal static Visibility CommunityToolsCommunityGridVisibility
        {
            get => !IsCommunityToolsCommunityAvailable ? Visibility.Collapsed : Visibility.Visible;
        }

        private static bool IsCommunityToolsCommunityAvailable
        {
            get => (PageStatics.CommunityToolsProperty?.CommunityToolsList?.Count ?? 0) != 0;
        }

        internal static int DefaultPostPanelIndex
        {
            get
            {
                if (IsPostEventPanelVisible != Visibility.Collapsed)
                    return 0;

                if (IsPostNoticePanelVisible != Visibility.Collapsed)
                    return 1;

                return IsPostInfoPanelVisible != Visibility.Collapsed ? 2 : 0;
            }
        }

        private static bool IsCarouselPanelAvailable
        {
            get => GameCarouselData?.Count > 0;
        }

        private static bool IsNewsPanelAvailable
        {
            get => GameNewsDataAll?.Count > 0;
        }

        private static bool IsSocialMediaPanelAvailable
        {
            get => GameSocialMediaData?.Count > 0;
        }

        internal static bool IsEventsPanelScaleUp
        {
            get
            {
                bool ret = GetAppConfigValue("ScaleUpEventsPanel").ToBoolNullable() ?? true;
                return ret;
            }
            set
            {
                SetAndSaveConfigValue("ScaleUpEventsPanel", value);
            }
        }

        internal bool IsPlaytimeBtnVisible
        {
            get
            {
                bool v = GetAppConfigValue("ShowGamePlaytime").ToBoolNullable() ?? true;
                HidePlaytimeButton(!v);

                return v;
            }
            set
            {
                SetAndSaveConfigValue("ShowGamePlaytime", value);
                HidePlaytimeButton(!value);
            }
        }

        internal bool IsShowSidePanel
        {
            get => GetAppConfigValue("ShowEventsPanel") &&
                   IsCarouselPanelAvailable &&
                   IsNewsPanelAvailable;
            set
            {
                SetAndSaveConfigValue("ShowEventsPanel", value);
                HideImageCarousel(!value);
            }
        }

        internal bool IsShowSocialMediaPanel
        {
            get => GetAppConfigValue("ShowSocialMediaPanel") &&
                   IsSocialMediaPanelAvailable;
            set
            {
                SetAndSaveConfigValue("ShowSocialMediaPanel", value);
                HideSocialMediaPanel(!value);
            }
        }

        internal bool IsPlaytimeSyncDb
        {
            get => CurrentGameProperty.GameSettings?.SettingsCollapseMisc.IsSyncPlaytimeToDatabase ?? false;
            set
            {
                if (CurrentGameProperty.GameSettings == null)
                {
                    return;
                }

                CurrentGameProperty.GameSettings.SettingsCollapseMisc.IsSyncPlaytimeToDatabase = value;
                CurrentGameProperty?.GameSettings?.SaveBaseSettings();
                SyncDbPlaytimeBtn.IsEnabled = value;

                // Run DbSync if toggle is changed to enable
                if (value) CurrentGameProperty?.GamePlaytime?.CheckDb();
            }
        }

        internal string NoNewsSplashMascot
        {
            get
            {
                GameNameType? gameType = CurrentGameProperty.GamePreset.GameType;
                return gameType switch
                {
                    GameNameType.Honkai => "ms-appx:///Assets/Images/GameMascot/AiShocked.png",
                    GameNameType.StarRail => "ms-appx:///Assets/Images/GameMascot/PomPomWhat.png",
                    GameNameType.Zenless => "ms-appx:///Assets/Images/GameMascot/BangbooShocked.png",
                    _ => "ms-appx:///Assets/Images/GameMascot/PaimonWhat.png"
                };
            }
        }

        internal int CurrentBannerIconHeight
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconHeight :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconHeightHYP;
        }

        internal Thickness CurrentBannerIconMargin
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconMargin :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconMarginHYP;
        }

        internal int CurrentBannerIconColumn
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   1 :
                   0;
        }

        internal static int CurrentBannerIconColumnSpan
        {
            get => 1;
        }

        internal int CurrentBannerIconRow
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   1 :
                   0;
        }

        internal static int CurrentBannerIconRowSpan
        {
            get => 1;
        }

        internal HorizontalAlignment CurrentBannerIconHorizontalAlign
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignHorizontal :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignHorizontalHYP;
        }

        internal VerticalAlignment CurrentBannerIconVerticalAlign
        {
            get => CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignVertical :
                   WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignVerticalHYP;
        }

        internal string? StartTooltipText
        {
            get
            {
                if (CurrentGameProperty.GameVersion == null)
                    return null;

                var installed = CurrentGameProperty.GameVersion.GetGameExistingVersion();
                if (installed is null)
                    return null;
                return string.Format(Locale.Lang._HomePage.StartGameTooltip, installed);
            }
        }

        internal string? InstallUpdateTooltipText
        {
            get
            {
                if (CurrentGameProperty.GameVersion == null)
                    return null;

                var state = Task.Run(async () => await CurrentGameProperty.GameVersion.GetGameState()).GetAwaiter().GetResult();
                switch (state)
                {
                    case GameInstallStateEnum.NotInstalled:
                    {
                        var remote = CurrentGameProperty.GameVersion.GetGameVersionApi();
                        return string.Format(Locale.Lang._HomePage.InstallGameTooltip, remote);
                    }
                    case GameInstallStateEnum.NeedsUpdate:
                    {
                        var installed = CurrentGameProperty.GameVersion.GetGameExistingVersion();
                        var remote = CurrentGameProperty.GameVersion.GetGameVersionApi();
                        if (remote is null || installed == remote)
                            return null;

                        if (installed is null)
                            return string.Format(Locale.Lang._HomePage.InstallGameTooltip, remote);

                        return string.Format(Locale.Lang._HomePage.UpdateGameTooltip, installed, remote);
                    }
                    case GameInstallStateEnum.InstalledHavePlugin:
                    {
                        var tooltip = new StringBuilder();

                        // SDK
                        {
                            var installed = CurrentGameProperty.GameVersion.GetSdkVersionInstalled();
                            var remote = CurrentGameProperty.GameVersion.GetSdkVersionApi();
                            if (remote is not null && installed != remote)
                            {
                                if (installed is null)
                                    tooltip.Append(string.Format(Locale.Lang._HomePage.InstallSdkTooltip, remote));
                                else
                                    tooltip.Append(string.Format(Locale.Lang._HomePage.UpdateSdkTooltip, installed, remote));
                            }
                        }

                        // Plugin
                        {
                            var installedDict = CurrentGameProperty.GameVersion.GetPluginVersionsInstalled();
                            var mismatchList = CurrentGameProperty.GameVersion.GetMismatchPlugin();
                            foreach (var mismatch in mismatchList)
                            {
                                if (tooltip.Length != 0)
                                    tooltip.Append('\n');

                                var remote = mismatch.Version;
                                if (!installedDict.TryGetValue(mismatch.PluginId!, out var installed))
                                    tooltip.Append(string.Format(Locale.Lang._HomePage.InstallPluginTooltip, mismatch.PluginId, remote));
                                else
                                    tooltip.Append(string.Format(Locale.Lang._HomePage.UpdatePluginTooltip, mismatch.PluginId, installed, remote));
                            }
                        }

                        if (tooltip.Length == 0)
                            return null;
                        return tooltip.ToString();
                    }
                    default:
                        return null;
                }
            }
        }
    }
}
