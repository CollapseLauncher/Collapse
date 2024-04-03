using CollapseLauncher.GameVersioning;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper.Shared.ClassStruct;
using System.Collections.Generic;

namespace CollapseLauncher.Interfaces
{
    internal static class IGameVersionCheckExtension
    {
        /// <summary>
        /// Casting the IGameVersionCheck as its origin or another version class.
        /// </summary>
        /// <typeparam name="TCast">The type of version class to cast</typeparam>
        /// <returns>The version class to get casted</returns>
        internal static TCast CastAs<TCast>(this IGameVersionCheck versionCheck)
            where TCast : GameVersionBase, IGameVersionCheck => (TCast)versionCheck;
    }

    internal interface IGameVersionCheck
    {
        /// <summary>
        /// Get the base of the instance
        /// </summary>
        GameVersionBase AsVersionBase { get; }

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
        PresetConfig GamePreset { get; set; }

        /// <summary>
        /// Returns or set the API properties
        /// </summary>
        RegionResourceProp GameAPIProp { get; set; }

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
        /// Returns the current version of the game as provided by miHoYo's API.
        /// </summary>
        /// <returns>The current version of the game</returns>
        GameVersion GetGameVersionAPI();

        /// <summary>
        /// Returns the preload version of the game as provided by miHoYo's API.
        /// </summary>
        /// <returns>The preload version of the game</returns>
        GameVersion? GetGameVersionAPIPreload();

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
        /// Returns the state of the game.
        /// </summary>
        GameInstallStateEnum GetGameState();

        /// <summary>
        /// Returns the Delta-patch file property.
        /// If the Delta-patch file doesn't exist, then it will return a null.<br/><br/>
        /// This method is only available for Honkai.
        /// </summary>
        DeltaPatchProperty GetDeltaPatchInfo();

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Latest Zip based on the game state
        /// </summary>
        /// <param name="gameState">The state of the game</param>
        List<RegionResourceVersion> GetGameLatestZip(GameInstallStateEnum gameState);

        /// <summary>
        /// Returns the <c>List</c> of the Resource Version for the Pre-load Zip.
        /// If the Pre-load doesn't exist, then it will return a null.
        /// </summary>
        List<RegionResourceVersion> GetGamePreloadZip();

#nullable enable
        /// <summary>
        /// Try find game installation path from the given path.
        /// If it returns null, then there's no game installation found.
        /// </summary>
        string? FindGameInstallationPath(string path);
#nullable disable

        /// <summary>
        /// Update the location of the game folder and also save it to the Game Profile's Ini file.
        /// </summary>
        /// <param name="path">The path of the game folder</param>
        /// <param name="saveValue">Save the config file</param>
        void UpdateGamePath(string path, bool saveValue = true);

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
        void UpdateGameVersion(GameVersion version, bool saveValue = true);

        /// <summary>
        /// Reinitialize the game version configs, including the INIs.
        /// </summary>
        void Reinitialize();
    }
}
