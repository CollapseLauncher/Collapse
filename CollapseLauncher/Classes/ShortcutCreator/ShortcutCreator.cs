using CollapseLauncher.Classes.Helper.Image;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Plugins;
using Hi3Helper;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.ShortcutUtils
{
    public static class ShortcutCreator
    {
        public static string GetIconName(PresetConfig preset)
        {
            return preset.GameType switch
            {
                GameNameType.Genshin => "icon-genshin.ico",
                GameNameType.StarRail => "icon-starrail.ico",
                GameNameType.Zenless => "icon-zenless.ico",
                GameNameType.Plugin => $"{preset.ProfileName}.ico",
                _ => "icon-honkai.ico"
            };
        }

#nullable enable
        public static string GetIconPath(PresetConfig preset)
        {
            string appPath = Path.GetDirectoryName(AppExecutablePath)!;
            string iconPath = Path.Combine(appPath, @"Assets\Images\GameIcon");

            string icon = Path.Combine(iconPath, GetIconName(preset));
            if (preset is not PluginPresetConfigWrapper pluginPresetConfig)
            {
                return icon;
            }

            pluginPresetConfig.Plugin.GetPluginAppIconUrl(out string? iconUrl);
            string? appIconUrl = ImageLoaderHelper.CopyToLocalIfBase64(iconUrl, iconPath);

            if (appIconUrl == null)
                return icon;

            icon = appIconUrl;
            if (Path.GetExtension(icon) == ".ico")
            {
                return icon;
            }

            string           expectedPath = Path.ChangeExtension(icon, ".ico");
            using FileStream stream       = File.OpenWrite(expectedPath);
            Image            img          = Image.FromFile(icon);
            ImageConverterHelper.ConvertToIcon(img)
                                .Save(stream);

            return expectedPath;
        }
#nullable disable

        internal static void CreateShortcut(string path, PresetConfig preset, bool play = false)
        {
            string translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.GameName,
                                                                        Lang._GameClientTitles)!;
            string translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.ZoneName,
                                                                        Lang._GameClientRegions);
            string shortcutName = $"{translatedGameTitle} ({translatedGameRegion}) - Collapse Launcher.url".Replace(":", "");
            string url = $"collapse://open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";

            if (play) url += " -p";

            string icon = GetIconPath(preset);

            string fullPath = Path.Combine(path, shortcutName);

            using StreamWriter writer = new StreamWriter(fullPath, false);
            writer.WriteLine($"[InternetShortcut]\nURL={url}\nIconIndex=0\nIconFile={icon}");
        }

        /// Heavily based on Heroic Games Launcher "Add to Steam" feature.
        /// 
        /// Source:
        /// https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/blob/8bdee1383446d3b81e240a4300baaf337d48ec92/src/backend/shortcuts/nonesteamgame/nonesteamgame.ts
        internal static async Task<bool> AddToSteam(PresetConfig preset, bool play)
        {
            string[] paths = GetShortcutsPath();

            if (paths == null || paths.Length == 0)
                return false;

            CancellationTokenSourceWrapper tokenSource = new CancellationTokenSourceWrapper();

            LoadingMessageHelper.ShowActionButton(Lang._Misc.Cancel, "", (_, _) =>
            {
                tokenSource.Cancel();
                LogWriteLine("[ShortcutCreator::AddToSteam] Cancelled manually.");
            });

            List<SteamShortcut> shortcuts = [];

            foreach (string path in paths)
            {
                SteamShortcutParser parser = new SteamShortcutParser(path);

                string[] splitPath = path.Split('\\');
                string userId = splitPath[^3];

                shortcuts.Add(parser.Insert(preset, play));

                parser.Save();
                LogWriteLine($"[ShortcutCreator::AddToSteam] Added shortcut for {preset.GameName} - {preset.ZoneName} for Steam3ID {userId}");
            }

            foreach (SteamShortcut shortcut in shortcuts)
            {
                await shortcut.MoveImages(tokenSource.Token);
            }

            return true;
        }

        private static string[] GetShortcutsPath()
        {
            RegistryKey reg = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false);

            if (reg == null)
                return null;

            string steamPath = (string)reg.GetValue("InstallPath", @"C:\Program Files (x86)\Steam");

            string steamUserData = Path.Combine(steamPath, "userdata");

            if (!Directory.Exists(steamUserData))
            {
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] " + steamUserData + " is not a valid folder.", LogType.Error);
                return null;
            }

            string[] res = Directory.GetDirectories(steamUserData)
                                    .Where(x =>
                                           {
                                               string y = x.Split("\\").Last();
                                               return y != "ac" && y != "0" && y != "anonymous";
                                           }).ToArray();

            for (int i = 0; i < res.Length; i++)
            {
                res[i] = Path.Combine(res[i], "config", "shortcuts.vdf");
                LogWriteLine("[ShortcutCreator::GetShortcutsPath] Found profile: " + res[i], LogType.Debug);
            }

            return res;
        }
    }
}
