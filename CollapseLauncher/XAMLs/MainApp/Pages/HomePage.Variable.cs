using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Management;
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
#pragma warning disable IDE0130
namespace CollapseLauncher.Pages;

public sealed partial class HomePage
{
    private string GameDirPath => CurrentGameProperty.GameVersion?.GameDirPath ?? throw new NullReferenceException();

    private static ILauncherApi? CurrentGameLauncherApi => LauncherMetadataHelper.CurrentMetadataConfig?.GameLauncherApi;

    private static HypLauncherBackgroundList? GameBackgroundData => CurrentGameLauncherApi?.LauncherGameBackground?.Data;

    private static HypLauncherContentKind? GameContentData => CurrentGameLauncherApi?.LauncherGameContent?.Data?.Content;

    internal static List<HypLauncherSocialMediaContentData>? GameSocialMediaData => GameContentData?.SocialMedia;

    private static List<HypLauncherMediaContentData>? GameNewsDataAll => GameContentData?.News;

    internal static List<HypLauncherMediaContentData>? GameNewsDataEventKind => GameContentData?.NewsEventKind;

    internal static List<HypLauncherMediaContentData>? GameNewsDataAnnouncementKind => GameContentData?.NewsAnnouncementKind;

    internal static List<HypLauncherMediaContentData>? GameNewsDataInformationKind => GameContentData?.NewsInformationKind;

    internal static List<HypLauncherCarouselContentData>? GameCarouselData => GameContentData?.Carousel;

    private static HypGameInfoData? GameInfoDisplayField => CurrentGameLauncherApi?.LauncherGameInfoField;

    private static bool IsGameStatusPreRegister =>
        GameInfoDisplayField?.DisplayStatus ==
        LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_RESERVATION_ENABLED;

    private static bool IsGameStatusComingSoon =>
        GameInfoDisplayField?.DisplayStatus ==
        LauncherGameAvailabilityStatus.LAUNCHER_GAME_DISPLAY_STATUS_COMING_SOON;

    internal static string? GamePreRegisterLink => GameInfoDisplayField?.ReservationLink?.ClickLink;

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

    private static bool IsCarouselPanelAvailable => GameCarouselData?.Count > 0;

    private static bool IsNewsPanelAvailable => GameNewsDataAll?.Count > 0;

    private static bool IsSocialMediaPanelAvailable => GameSocialMediaData?.Count > 0;

    internal static bool IsEventsPanelScaleUp
    {
        get
        {
            bool ret = GetAppConfigValue("ScaleUpEventsPanel").ToBoolNullable() ?? true;
            return ret;
        }
        set => SetAndSaveConfigValue("ScaleUpEventsPanel", value);
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

    internal int CurrentBannerIconHeight =>
        CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
            WindowSize.WindowSize.CurrentWindowSize.BannerIconHeight :
            WindowSize.WindowSize.CurrentWindowSize.BannerIconHeightHYP;

    internal Thickness CurrentBannerIconMargin =>
        CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
            WindowSize.WindowSize.CurrentWindowSize.BannerIconMargin :
            WindowSize.WindowSize.CurrentWindowSize.BannerIconMarginHYP;

    internal int CurrentBannerIconColumn =>
        CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
            1 :
            0;

    internal static int CurrentBannerIconColumnSpan => 1;

    internal int CurrentBannerIconRow =>
        CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
            1 :
            0;

    internal static int CurrentBannerIconRowSpan => 1;

    internal HorizontalAlignment CurrentBannerIconHorizontalAlign =>
        CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
            WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignHorizontal :
            WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignHorizontalHYP;

    internal VerticalAlignment CurrentBannerIconVerticalAlign =>
        CurrentGameProperty?.GamePreset.LauncherType == LauncherType.Sophon ?
            WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignVertical :
            WindowSize.WindowSize.CurrentWindowSize.BannerIconAlignVerticalHYP;

    private static ImageBackgroundManager CurrentBackgroundManager => ImageBackgroundManager.Shared;

    internal string? StartTooltipText
    {
        get
        {
            GameVersion? installed = CurrentGameProperty?.GameVersion?.GetGameExistingVersion();
            return installed is null ? null : string.Format(Locale.Lang._HomePage.StartGameTooltip, installed);
        }
    }

    internal string? InstallUpdateTooltipText
    {
        get
        {
            if (CurrentGameProperty?.GameVersion == null)
                return null;

            GameInstallStateEnum state = Task.Run(async () => await CurrentGameProperty.GameVersion.GetGameState()).GetAwaiter().GetResult();
            switch (state)
            {
                case GameInstallStateEnum.NotInstalled:
                {
                    GameVersion? remote = CurrentGameProperty.GameVersion.GetGameVersionApi();
                    return string.Format(Locale.Lang._HomePage.InstallGameTooltip, remote);
                }
                case GameInstallStateEnum.NeedsUpdate:
                {
                    GameVersion? installed = CurrentGameProperty.GameVersion.GetGameExistingVersion();
                    GameVersion? remote    = CurrentGameProperty.GameVersion.GetGameVersionApi();
                    if (remote is null || installed == remote)
                        return null;

                    return installed is null
                        ? string.Format(Locale.Lang._HomePage.InstallGameTooltip, remote)
                        : string.Format(Locale.Lang._HomePage.UpdateGameTooltip, installed, remote);
                }
                case GameInstallStateEnum.InstalledHavePlugin:
                {
                    StringBuilder tooltip = new();

                    // SDK
                    {
                        GameVersion? installed = CurrentGameProperty.GameVersion.GetSdkVersionInstalled();
                        GameVersion? remote    = CurrentGameProperty.GameVersion.GetSdkVersionApi();
                        if (remote is not null && installed != remote)
                        {
                            tooltip.Append(installed is null
                                               ? string.Format(Locale.Lang._HomePage.InstallSdkTooltip, remote)
                                               : string.Format(Locale.Lang._HomePage.UpdateSdkTooltip, installed,
                                                               remote));
                        }
                    }

                    // Plugin
                    {
                        Dictionary<string, GameVersion> installedDict = CurrentGameProperty.GameVersion.GetPluginVersionsInstalled();
                        List<HypPluginPackageInfo>      mismatchList  = CurrentGameProperty.GameVersion.GetMismatchPlugin();
                        foreach (HypPluginPackageInfo mismatch in mismatchList)
                        {
                            if (tooltip.Length != 0)
                                tooltip.Append('\n');

                            GameVersion remote = mismatch.Version;
                            tooltip.Append(!installedDict.TryGetValue(mismatch.PluginId!, out GameVersion installed)
                                               ? string.Format(Locale.Lang._HomePage.InstallPluginTooltip,
                                                               mismatch.PluginId, remote)
                                               : string.Format(Locale.Lang._HomePage.UpdatePluginTooltip,
                                                               mismatch.PluginId, installed, remote));
                        }
                    }

                    return tooltip.Length == 0
                        ? null : tooltip.ToString();
                }
                default:
                    return null;
            }
        }
    }
}
