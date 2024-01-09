using IWshRuntimeLibrary;
using Hi3Helper.Preset;
using static Hi3Helper.Shared.Region.LauncherConfig;
using System.IO;

namespace CollapseLauncher
{
    public static class ShortcutCreator
    {
        public static void CreateShortcut(string path, PresetConfigV2 preset)
        {
            string shortcutName = preset.ZoneFullname + " - Collapse Launcher" + ".lnk";
            WshShell shell = new WshShell();
            IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(Path.Combine(path, shortcutName));
            shortcut.Description = string.Format("Shortcut for Collapse Launcher ({0})", preset.ZoneFullname);
            shortcut.TargetPath = AppExecutablePath;
            shortcut.Arguments = string.Format("open -g \"{0}\" -r \"{1}\"", preset.GameName, preset.ZoneName);
            
            shortcut.IconLocation = Path.GetFullPath(Path.Combine(AppExecutablePath, preset.GameType switch
            {
                GameType.Honkai => @"Assets\Images\GameLogo\honkai-logo.ico",
                GameType.Genshin => @"Assets\Images\GameLogo\genshin-logo.ico",
                GameType.StarRail => @"Assets\Images\GameLogo\starrail-logo.ico",
                GameType.Zenless => @"Assets\Images\GameLogo\zenless-logo.ico",
                _ => @"icon.ico"
            }));
            shortcut.Save();
        }
    }
}
