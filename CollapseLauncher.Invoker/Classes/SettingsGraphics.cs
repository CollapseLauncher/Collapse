using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Drawing;
using System.Threading.Tasks;
using Microsoft.Win32;

using Newtonsoft.Json;

using Hi3Helper.Data;

using static Hi3Helper.Shared.Region.InstallationManagement;
using static Hi3Helper.Shared.Region.GameSettingsManagement;

namespace CollapseLauncher.Invoker
{
    internal static class SettingsGraphics
    {
        public static void DoApplyGameSettings(string settingspath, string regpath)
        {
            gameIni.SettingsPath = settingspath;
            gameIni.SettingsStream = new FileStream(gameIni.SettingsPath, FileMode.Open, FileAccess.Read);
            gameIni.Settings = new IniFile();
            gameIni.Settings.Load(gameIni.SettingsStream);

            SaveWindowValue();
            SavePersonalGraphicsSettingsValue();
        }

        public static void DoLoadGameSettings()
        {

        }
    }
}
