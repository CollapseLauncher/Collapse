using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Hi3Helper.Data;

namespace CollapseLauncher.Region
{
    internal static partial class InstallationManagement
    {
        static void BuildIniProfile()
        {
            gameIni.Profile.Add("launcher", new Dictionary<string, IniValue>
            {
                { "cps", new IniValue() },
                { "channel", new IniValue("1") },
                { "sub_channel", new IniValue("1") },
                { "game_install_path", new IniValue(Path.Combine(gamePath, "Games").Replace('\\', '/')) },
                { "game_start_name", new IniValue("BH3.exe") },
                { "is_first_exit", new IniValue(false) },
                { "exit_type", new IniValue(2) }
            });

            gameIni.Profile.Save(gameIni.ProfileStream);
        }
    }
}
