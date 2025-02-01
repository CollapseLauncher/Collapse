using CollapseLauncher.Interfaces;
using System.IO;
// ReSharper disable GrammarMistakeInComment

#nullable enable
namespace CollapseLauncher.GameSettings.Base
{
    internal sealed class SettingsGameVersionManager
    {
        /// <summary>
        /// Create an instance of the IGameVersion adapter for <see cref="MagicNodeBaseValues{T}"/> members.<br/>
        /// Note: Use the formatting string with index 0 for executable and 1 for the config filename.<br/>
        /// <br/>
        /// For example:<br/>
        /// "{0}_Data\Persistent\LocalStorage\{1}"
        /// </summary>
        /// <param name="gameVersionManager">The game version manager to be injected</param>
        /// <param name="fileDirPathFormat">The formatted string for the file location</param>
        /// <param name="fileName">Name of the config file</param>
        /// <returns><see cref="SettingsGameVersionManager"/> to be used for <see cref="MagicNodeBaseValues{T}"/> members.</returns>
        internal static SettingsGameVersionManager Create(IGameVersion? gameVersionManager, string fileDirPathFormat, string fileName)
        {
            return new SettingsGameVersionManager
            {
                VersionManager = gameVersionManager,
                ConfigFileLocationFormat = fileDirPathFormat,
                ConfigFileName = fileName
            };
        }

        internal IGameVersion? VersionManager { get; set; }

        internal string? GameFolder => VersionManager?.GameDirPath;
        internal string GameExecutable => Path.GetFileNameWithoutExtension(VersionManager?.GamePreset.GameExecutableName!);
        internal string? ConfigFileLocationFormat { get; set; }
        internal string? ConfigFileName { get; set; }
        internal string ConfigFilePath { get => Path.Combine(GameFolder ?? string.Empty, string.Format(ConfigFileLocationFormat ?? string.Empty, GameExecutable, ConfigFileName)); }
    }

}
