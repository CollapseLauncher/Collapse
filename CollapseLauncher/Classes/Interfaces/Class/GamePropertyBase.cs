using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using System;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Interfaces;

internal class GamePropertyBase
{
    protected const int    BufferMediumLength                        = 1 << 20; // 1 MiB
    protected const int    BufferBigLength                           = 2 << 20; // 2 MiB
    private const   double DownloadThreadCountReservedMultiplication = 1.5d;

    protected virtual string UserAgent => "UnityPlayer/2017.4.18f1 (UnityWebRequest/1.0, libcurl/7.51.0-DEV)";

    protected GamePropertyBase(UIElement      parentUI,
                               IGameVersion?  gameVersionManager,
                               IGameSettings? gameSettings,
                               string?        gamePath,
                               string?        gameRepoURL,
                               string?        versionOverride)
    {
        GameSettings       = gameSettings;
        GameVersionManager = gameVersionManager;
        ParentUI           = parentUI;
        GamePathField      = gamePath;
        GameRepoURL        = gameRepoURL;
        Token              = new CancellationTokenSourceWrapper();
        IsVersionOverride  = versionOverride != null;

        // If the version override is not null, then assign the override value
        if (IsVersionOverride)
        {
            GameVersionOverride = versionOverride;
        }
    }

    private string? GamePathField
    {
        get;
    }

    private GameVersion GameVersionOverride
    {
        get;
    }

    protected static bool IsBurstDownloadEnabled
    {
        get => LauncherConfig.IsBurstDownloadModeEnabled;
    }

    protected bool IsVersionOverride
    {
        get;
    }

    protected CancellationTokenSourceWrapper? Token
    {
        get;
        set;
    }

    protected static int DownloadThreadCount
    {
        get => LauncherConfig.AppCurrentDownloadThread;
    }

    protected static int DownloadThreadWithReservedCount
    {
        get => (int)Math.Round(DownloadThreadCount * DownloadThreadCountReservedMultiplication);
    }

    protected static int ThreadCount
    {
        get => (byte)LauncherConfig.AppCurrentThread;
    }

    protected GameVersion GameVersion
    {
        get
        {
            if (GameVersionManager != null && IsVersionOverride)
            {
                return GameVersionOverride;
            }
            return GameVersionManager?.GetGameExistingVersion() ?? throw new NullReferenceException();
        }
    }

    protected IGameVersion? GameVersionManager
    {
        get;
    }

    protected IGameSettings? GameSettings
    {
        get;
        init;
    }

    protected string GamePath
    {
        get => (string.IsNullOrEmpty(GamePathField) ? GameVersionManager?.GameDirPath : GamePathField) ?? "";
    }

    protected string? GameRepoURL
    {
        get
        {
            if (!string.IsNullOrEmpty(field))
            {
                return field;
            }

            string gameBiz = GameVersionManager?.LauncherApi.GameBiz ?? "";
            string gameId = GameVersionManager?.LauncherApi.GameId ?? "";
            HypLauncherGameResourcePackageApi? resourcePackage = GameVersionManager?.LauncherApi.LauncherGameResourcePackage;

            if (!(resourcePackage?.Data?.TryFindByBizOrId(gameBiz, gameId, out HypResourcesData? data) ?? false))
            {
                return null;
            }

            field = data.MainPackage?.CurrentVersion?.ResourceListUrl;
            return field;
        }
        set;
    }

    protected bool UseFastMethod
    {
        get;
        set;
    }

    public UIElement ParentUI
    {
        get;
    }
}
