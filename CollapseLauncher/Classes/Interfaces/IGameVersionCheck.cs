using CollapseLauncher.Helper.LauncherApiLoader;
using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces.Class;
using Hi3Helper.Data;
using Hi3Helper.Plugin.Core.Management;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Threading.Tasks;
// ReSharper disable CommentTypo
// ReSharper disable GrammarMistakeInComment
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Interfaces
{
    /// <summary>
    /// Interface to define how the game versioning and the game launcher data must be returned.<br/>
    /// In every member of this interface, <see cref="IGameVersion.InitializeIniProp"/> method must be called!
    /// </summary>
    internal interface IGameVersion
    {
        /// <summary>
        /// Get the game name
        /// </summary>
        string GameName { get; }

        /// <summary>
        /// Get the region name of the game
        /// </summary>
        string GameRegion { get; }

        /// <summary>
        /// Gets the game name alias
        /// </summary>
        string GameBiz { get; }

        /// <summary>
        /// Gets the HoYoPlay's game ID
        /// </summary>
        string GameId  { get; }

        /// <summary>
        /// Get the version section of the game INI's configuration
        /// </summary>
        IniSection? GameIniVersionSection { get; }

        /// <summary>
        /// Get the profile section of the game INI's configuration
        /// </summary>
        IniSection? GameIniProfileSection { get; }

        /// <summary>
        /// Returns or sets the path of the game.<br/>
        /// If you set the value from this property, the value won't be saved until you execute <c>UpdateGamePath()</c>
        /// </summary>
        string GameDirPath { get; set; }

        /// <summary>
        /// Returns the app data path of the game
        /// </summary>
        string GameDirAppDataPath { get; }

        /// <summary>
        /// Returns or sets the game preset
        /// </summary>
        PresetConfig GamePreset { get; }

        /// <summary>
        /// Returns or set the API properties
        /// </summary>
        ILauncherApi LauncherApi { get; }

        /// <summary>
        /// Returns the type of the game
        /// </summary>
        GameNameType GameType { get; }

        /// <summary>
        /// Returns the name of the engine output log file
        /// </summary>
        string GameOutputLogName { get; }

        /// <summary>
        /// Returns the game vendor type property and the game name based on <c>app.info</c> file
        /// </summary>
        GameVendorProp VendorTypeProp { get; }

        /// <summary>
        /// Initialize Ini file configuration. This method must be called after the instance is being initialized.
        /// </summary>
        void InitializeIniProp();

        /// <summary>
        /// Returns the current version of the game as provided by miHoYo's API.
        /// </summary>
        /// <returns>The current version of the game</returns>
        GameVersion? GetGameVersionApi();

        /// <summary>
        /// Returns the preload version of the game as provided by miHoYo's API.
        /// </summary>
        /// <returns>The preload version of the game</returns>
        GameVersion? GetGameVersionApiPreload();

        /// <summary>
        /// Returns the version of the game installed.<br/>
        /// It will return a <c>null</c> if the game doesn't installed.
        /// </summary>
        /// <returns>The version of the game installed</returns>
        GameVersion? GetGameExistingVersion();

        /// <summary>
        /// Checks if the game version is installed or matches the version provided from miHoYo's API.
        /// </summary>
        bool IsGameVersionMatch();

        /// <summary>
        /// Checks if the plugin version is installed or matches the version provided from miHoYo's API.
        /// </summary>
        ValueTask<bool> IsPluginVersionsMatch();

        /// <summary>
        /// Checks if the sdk version is installed or matches the version provided from miHoYo's API.
        /// This is used to obtain the status of the SDK .dlls for certain builds (for example: Bilibili version)
        /// </summary>
        ValueTask<bool> IsSdkVersionsMatch();

        /// <summary>
        /// Check if the game version is installed.
        /// </summary>
        bool IsGameInstalled();

        /// <summary>
        /// Check if the game has a pre-load.
        /// </summary>
        bool IsGameHasPreload();

        /// <summary>
        /// Check if the game has a delta-patch. (For Honkai only)
        /// </summary>
        bool IsGameHasDeltaPatch();

        /// <summary>
        /// Check if the game installation is forcefully redirected to Sophon.
        /// </summary>
        bool IsForceRedirectToSophon();

        /// <summary>
        /// Returns the state of the game.
        /// </summary>
        ValueTask<GameInstallStateEnum> GetGameState();

        /// <summary>
        /// Returns the Delta-patch file property.
        /// If the Delta-patch file doesn't exist, then it will return a null.<br/><br/>
        /// This method is only available for Honkai.
        /// </summary>
        DeltaPatchProperty? GetDeltaPatchInfo();

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Latest Zip based on the game state
        /// </summary>
        /// <param name="gameState">The state of the game</param>
        GamePackageResult GetGameLatestZip(GameInstallStateEnum gameState);

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Pre-load Zip.
        /// If the Pre-load doesn't exist, then it will return a null.
        /// </summary>
        GamePackageResult GetGamePreloadZip();

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Plugins
        /// </summary>
        List<HypPluginPackageInfo> GetGamePluginZip();

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the SDKs
        /// </summary>
        List<HypChannelSdkData> GetGameSdkZip();

        /// <summary>
        /// Ensure the validity of the game's config.ini file.
        /// This method also being used to check + prompt the user to whether
        /// repair the game's config.ini or ignore it.
        /// </summary>
        /// <param name="uiParentElement">The parent UI element where the dialog will be spawned (if needed)</param>
        /// <returns><c>True</c> means the values within config.ini is valid or action is cancelled. <c>False</c> means config.ini has been repaired or changed.</returns>
        ValueTask<bool> EnsureGameConfigIniCorrectiveness(UIElement uiParentElement);

        /// <summary>
        /// Try to find game installation path from the given path.
        /// If it returns null, then there's no game installation found.
        /// </summary>
        Task<string?> FindGameInstallationPath(string path);

        /// <summary>
        /// Update the location of the game folder and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="path">The path of the game folder</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGamePath(string? path, bool saveValue = true);

        /// <summary>
        /// Update the version of the game to the latest one provided by miHoYo's API and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGameVersionToLatest(bool saveValue = true);

        /// <summary>
        /// Update the version of the game to the given value and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="version">The version to change</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGameVersion(GameVersion? version, bool saveValue = true);

        /// <summary>
        /// Update the game channel and save it to the config.
        /// </summary>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGameChannels(bool saveValue = true);

        /// <summary>
        /// Update the game plugin versions and save it to the config.
        /// </summary>
        /// <param name="versions">The dictionary collection of the plugins</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdatePluginVersions(Dictionary<string, GameVersion> versions, bool saveValue = true);

        /// <summary>
        /// Update the game SDK version and save it to the config.
        /// </summary>
        /// <param name="version">The version of the SDK</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateSdkVersion(GameVersion? version, bool saveValue = true);

        /// <summary>
        /// Save the <see cref="GameIniVersionSection"/> changes to the game version config.ini file.
        /// </summary>
        void SaveVersionConfig();

        /// <summary>
        /// Reinitialize the game version configs, including the INIs.
        /// </summary>
        void Reinitialize();
    }
}
