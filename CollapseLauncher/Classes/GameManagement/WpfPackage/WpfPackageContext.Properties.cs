using CollapseLauncher.Extension;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Shared.Region;
using System;
using System.IO;
#pragma warning disable IDE0290
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.GameManagement.WpfPackage;

internal partial class WpfPackageContext
{
    #region Constants
    private const string WpfPackageVersionKey = "wpf_version";
    private       string ConfigAutoUpdateKey { get; }
    #endregion

    #region Fields
    private volatile CancellationTokenSourceWrapper _localCts = new();
    #endregion

    #region Properties
    /// <summary>
    /// Game info display data for the current game and region.
    /// </summary>
    private HypGameInfoData? WpfGetGameData
    {
        get => field ??= GameVersionManager
                        .LauncherApi
                        .LauncherGetGame?
                        .Data?
                        .TryFindByBizOrId(GameVersionManager.GameBiz,
                                          GameVersionManager.GameId,
                                          out HypGameInfoData? data) ?? false
            ? data
            : null;
    }

    /// <summary>
    /// Gets the WPF Package Data from the API. If the package is unavailable, it will return null.
    /// </summary>
    private HypPackageData? WpfPackageData
    {
        get => field ??= (GameVersionManager
                        .LauncherApi
                        .LauncherGameResourceWpf?
                        .Data?
                        .TryFindByBizOrId(GameVersionManager.GameBiz,
                                          GameVersionManager.GameId,
                                          out HypWpfPackageData? data) ?? false) &&
                         data.PackageInfo != null
            ? data.PackageInfo
            : null;
    }

    /// <summary>
    /// Whether to skip WPF package verification before update if flag file exists
    /// </summary>
    private bool IsSkipPackageVerification
    {
        get => File.Exists(Path.Combine(GamePath, "@NoVerification"));
    }

    /// <summary>
    /// Whether to delete WPF package after installation
    /// </summary>
    private bool IsDeletePackageAfterInstall
    {
        get => !File.Exists(Path.Combine(GamePath, "@NoDeleteZip"));
    }

    /// <summary>
    /// Whether WPF package is enabled for the game
    /// </summary>
    public bool IsWpfPackageEnabled
    {
        get => GameVersionManager.IsGameInstalled() &&
               GameVersionManager.GamePreset.IsWpfUpdateEnabled &&
               WpfPackageData != null &&
               WpfGetGameData != null;
    }

    /// <summary>
    /// Gets the localized WPF package name
    /// </summary>
    public string WpfPackageNameLocalized
    {
        get => Locale
              .Lang
              ._WpfPackageName
              .TryGetValueIgnoreCase(GameVersionManager.GamePreset.GameName ?? "")
               ?? "Unknown";
    }

    /// <summary>
    /// Gets the WPF package icon URL.
    /// </summary>
    public string? WpfPackageIconUrl
    {
        get => field ??= WpfGetGameData?.Display?.WpfIcon?.ImageUrl;
    }

    /// <summary>
    /// Gets or sets the current directory of the game installation.
    /// </summary>
    public override string GamePath
    {
        get => GameVersionManager.GameDirPath;
        set => GameVersionManager.GameDirPath = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether changes are currently in progress.
    /// </summary>
    public bool ChangesInProgress
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// To indicate whether an update is available.
    /// </summary>
    public bool IsUpdateAvailable
    {
        get => CurrentAvailableVersion != GameVersion.Empty &&
               CurrentAvailableVersion > CurrentInstalledVersion;
    }

    /// <summary>
    /// Gets or sets the last error to be displayed while processing WPF Package updates.
    /// </summary>
    public Exception? LastError
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Gets or sets the currently installed WPF Package version. <br/>
    /// For the getter, if the game is not installed or if the WPF package is not installed, it will return <see cref="GameVersion.Empty"/>.<br/>
    /// For the setter, if the game is not installed, it won't save the changes to the game version config.ini
    /// </summary>
    public GameVersion CurrentInstalledVersion
    {
        get
        {
            if (!IsWpfPackageEnabled)
            {
                return GameVersion.Empty;
            }

            if (field != GameVersion.Empty)
            {
                return field;
            }

            if (!(GameVersionManager
               .GameIniVersionSection?
               .TryGetValue(WpfPackageVersionKey, out IniValue versionValue) ?? false) ||
                versionValue.IsEmpty ||
                !GameVersion.TryParse(versionValue, out GameVersion version))
            {
                return GameVersion.Empty;
            }

            return field = version;
        }
        set
        {
            if (!IsWpfPackageEnabled ||
                GameVersionManager.GameIniVersionSection == null)
            {
                return;
            }

            if (GameVersionManager.GameIniVersionSection["wpf_version"] == value)
            {
                return;
            }

            field = value;

            GameVersionManager.GameIniVersionSection["wpf_version"] = value.ToString("f");
            GameVersionManager.SaveVersionConfig();
            OnPropertyChanged();

            // Note:
            // This triggers the UI binding to be updated.
            // So trigger the IsUpdateAvailable property here.
            OnPropertyChanged(nameof(IsUpdateAvailable));
        }
    }

    /// <summary>
    /// Gets the currently available WPF Package version from the API. This returns <see cref="GameVersion.Empty"/> if the game is not installed or if the API isn't available.
    /// </summary>
    public GameVersion CurrentAvailableVersion
    {
        get
        {
            if (!IsWpfPackageEnabled)
            {
                return GameVersion.Empty;
            }

            if (field != GameVersion.Empty)
            {
                return field;
            }

            if (WpfPackageData != null)
            {
                field = WpfPackageData?.Version ?? default;
            }

            return field;
        }
    }

    /// <summary>
    /// Gets or sets auto-update toggle for WPF Package updates.
    /// </summary>
    public bool IsAutoUpdateEnabled
    {
        get
        {
            string? value = LauncherConfig.GetAppConfigValue(ConfigAutoUpdateKey);

            if (string.IsNullOrEmpty(value) ||
                !bool.TryParse(value, out bool isEnabled))
            {
                return true;
            }

            return isEnabled;
        }
        set
        {
            LauncherConfig.SetAndSaveConfigValue(ConfigAutoUpdateKey, value);
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Indicates whether the update check has already been performed.
    /// This is used as a state so the launcher won't perform multiple update checks.
    /// </summary>
    public bool IsCheckAlreadyPerformed
    {
        get;
        private set;
    }

    private void ResetCancelToken()
    {
        if (_localCts is { IsDisposed: false, IsCancelled: false })
        {
            return;
        }

        _localCts.Dispose();
        _localCts = new CancellationTokenSourceWrapper();
    }
    #endregion
}
