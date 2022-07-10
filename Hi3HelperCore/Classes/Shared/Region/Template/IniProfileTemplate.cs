using Hi3Helper.Data;
using System.Collections.Generic;
using System.IO;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace Hi3Helper.Shared.Region
{
    public static partial class InstallationManagement
    {
        public static void BuildGameIniProfile()
        {
            gameIni.Profile.Add("launcher", new Dictionary<string, IniValue>
            {
                { "cps", new IniValue() },
                { "channel", new IniValue("1") },
                { "sub_channel", new IniValue("1") },
                { "game_install_path", new IniValue(Path.Combine(gamePath, CurrentRegion.GameDirectoryName).Replace('\\', '/')) },
                { "game_start_name", new IniValue(CurrentRegion.GameExecutableName) },
                { "is_first_exit", new IniValue(false) },
                { "exit_type", new IniValue(2) }
            });

            gameIni.Profile.Save(gameIni.ProfilePath);
        }

        public static void BuildGameIniConfig()
        {
            gameIni.Config.Add("General", new Dictionary<string, IniValue>
            {
                { "channel", new IniValue(1) },
                { "cps", new IniValue() },
                { "game_version", new IniValue(regionResourceProp.data.game.latest.version) },
                { "sub_channel", new IniValue(1) },
                { "sdk_version", new IniValue() }
            });

            gameIni.Config.Save(gameIni.ConfigPath);
        }
    }
}
