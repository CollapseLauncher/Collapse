using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
// ReSharper disable StringLiteralTypo
// ReSharper disable LoopCanBeConvertedToQuery

namespace Hi3Helper.Data
{
    public class AppInfo
    {
        public int    Id           { get; internal set; }
        public string Name         { get; internal set; }
        public string SteamUrl     { get; internal set; }
        public string Manifest     { get; internal set; }
        public string GameRoot     { get; internal set; }
        public string Executable   { get; internal set; }
        public string InstallDir   { get; internal set; }
        public SteamInstallState InstallState { get; internal set; }
    }

    
    // Ref: https://github.com/SteamRE/open-steamworks/blob/f65c0439bf06981285da1e7639de82cd760755b7/Open%20Steamworks/AppsCommon.h#L39-L64
    [Flags]
    public enum SteamInstallState
    {
        Invalid        = 0,
        Uninstalled    = 1 << 0,  // 1
        UpdateRequired = 1 << 1,  // 2
        FullyInstalled = 1 << 2,  // 4
        DataEncrypted  = 1 << 3,  // 8
        DataLocked     = 1 << 4,  // 16
        FilesMissing   = 1 << 5,  // 32
        SharedOnly     = 1 << 6,  // 64
        FilesCorrupt   = 1 << 7,  // 128
        UpdateRunning  = 1 << 8,  // 256
        UpdatePaused   = 1 << 9,  // 512
        UpdateStarted  = 1 << 10, // 1024
        Uninstalling   = 1 << 11, // 2048
        BackupRunning  = 1 << 12, // 4096
        AppRunning     = 1 << 13  // 8192
    }

    public static partial class SteamTool
    {
        // Reference:
        // https://stackoverflow.com/a/67679123
        public static List<AppInfo> GetSteamApps(List<string> steamLibs)
        {
            List<AppInfo> apps = [];
            for (var index = 0; index < steamLibs!.Count; index++)
            {
                var lib             = steamLibs![index];
                var appMetaDataPath = Path.Combine(lib!, "SteamApps");
                foreach (var file in Directory.EnumerateFiles(appMetaDataPath, "*.acf"))
                {
                    var appInfo = GetAppInfo(file);
                    if (appInfo != null)
                    {
                        apps.Add(appInfo);
                    }
                }
            }

        #if DEBUG
            if (apps.Count == 0) Logger.LogWriteLine("AppInfo on steam cannot be found!");
#endif
            return apps;
        }

        [GeneratedRegex("""
                        \s*"(?<key>\w+)"\s+"(?<val>.*)"
                        """, RegexOptions.NonBacktracking)]
        public static partial Regex GetAppInfoKeyValueMatch();

        public static AppInfo GetAppInfo(string appMetaFile)
        {
            var                        fileDataLines = File.ReadAllLines(appMetaFile!);
            Dictionary<string, string> dic           = new(StringComparer.OrdinalIgnoreCase);

#if DEBUG
            Logger.LogWriteLine($"Reading .acf Steam file: {appMetaFile}");
#endif

            foreach (var line in fileDataLines)
            {
                var match = GetAppInfoKeyValueMatch().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var key = match.Groups["key"].Value;
                var val = match.Groups["val"].Value;
            #if DEBUG
                Logger.LogWriteLine($"    AppInfo key: {key} val: {val}");
            #endif
                dic[key] = val;
            }

            if (dic.Keys.Count <= 0)
            {
                return null;
            }

            var appInfo    = new AppInfo();
            var appId      = dic["appid"];
            var name       = dic["name"];
            var installDir = dic["installDir"];

            var path        = Path.GetDirectoryName(appMetaFile);
            var libGameRoot = Path.Combine(path!, "common", installDir!);

            if (!Directory.Exists(libGameRoot)) return null;

            appInfo.Id           = int.Parse(appId!);
            appInfo.Name         = name;
            appInfo.Manifest     = appMetaFile;
            appInfo.GameRoot     = libGameRoot;
            appInfo.InstallDir   = installDir;
            appInfo.SteamUrl     = $"steam://runsteamid/{appId}";
            appInfo.Executable   = GetExecutable(appInfo);
            appInfo.InstallState = GetGameInstallState(dic);

            return appInfo;
        }

        private static SteamInstallState GetGameInstallState(Dictionary<string, string> steamDic)
        {
            try
            {
                var rawState = steamDic["StateFlags"];

                if (int.TryParse(rawState, out int intState))
                {
                    SteamInstallState state = (SteamInstallState)intState;
                    return state;
                }
            }
            catch (KeyNotFoundException ex)
            {
                Logger.LogWriteLine($"[Steam Install State] StateFlags not found, returning 0 (Not installed!\r\n" +
                                    $"{ex.Message}", LogType.Error, true);
            }
            return 0; // Not installed
        }

        private static string _appInfoText;
        public static string GetExecutable(AppInfo appInfo)
        {
            if (_appInfoText == null)
            {
                var appInfoFile = Path.Combine(GetSteamPath()!, "appcache", "appinfo.vdf");
                var bytes = File.ReadAllBytes(appInfoFile);
                _appInfoText = Encoding.UTF8.GetString(bytes);
            }
            var startIndex = 0;
            int maxTries = 50;
            var fullName = "";

            do
            {
                var startOfDataArea =
                    _appInfoText.IndexOf("\0\x01" + $"name\0{appInfo!.Name}\0", startIndex, StringComparison.Ordinal);
                
                if (startOfDataArea < 0 && maxTries == 50) startOfDataArea = 
                    _appInfoText.IndexOf("\0\x01" + $"gamedir\0{appInfo!.Name}\0", startIndex, StringComparison.Ordinal); //Alternative1
                
                if (startOfDataArea < 0 && maxTries == 50) startOfDataArea = 
                    _appInfoText.IndexOf("\0\x01" + $"name\0{appInfo!.Name}\0",    startIndex, StringComparison.Ordinal); //Alternative2
                
                if (startOfDataArea > 0)
                {
                    startIndex = startOfDataArea + 10;
                    int nextLaunch = -1;
                    do
                    {
                        var executable = _appInfoText.IndexOf("\0\x01" + "executable\0", startOfDataArea, StringComparison.Ordinal);
                        if (executable > -1 && nextLaunch == -1)
                        {
                            nextLaunch = _appInfoText.IndexOf("\0\x01" + "launch\0", executable, StringComparison.Ordinal);
                        }

                        if ((nextLaunch <= 0 || executable < nextLaunch) && executable > 0)
                        {
                            executable += 10;
                            string filename = "";
                            while (_appInfoText[executable] != '\x00')
                            {
                                filename += _appInfoText[executable];
                                executable++;
                            }
                            if (filename.Contains("://"))
                            {
                                //EA or other external
                                return filename; //Need to use other means to grab the EXE here.
                            }

                            fullName = Path.Combine(appInfo.GameRoot!, filename);

                            startOfDataArea = executable + 1;
                            startIndex      = startOfDataArea + 10;
                        }
                        else
                        {
                            break;
                        }
                    }
                    while (!File.Exists(fullName) && maxTries-- > 0);
                }
                else
                {
                    return null;
                }
            } while (!File.Exists(fullName) && maxTries-- > 0);

            return File.Exists(fullName) ? fullName : null;
        }

        [GeneratedRegex("""(?<path>\w:\\\\.*)""", RegexOptions.NonBacktracking)]
        public static partial Regex GetLibraryPathMatch();

        public static List<string> GetSteamLibs()
        {
            var          steamPath = GetSteamPath();
            List<string> libraries = [steamPath];

            if (steamPath == null)
            {
                Logger.LogWriteLine("Steam not found. If you think this is an error report it on our GitHub.", LogType.Error, true);
                return null;
            }

            var listFile = Path.Combine(steamPath, @"steamapps\libraryfolders.vdf");
            if (!File.Exists(listFile))
            {
                Logger.LogWriteLine("LibraryFolder.vdf not found. If you think this is an error report it on our GitHub.", LogType.Error, true);
                return null;
            }

            var lines = File.ReadAllLines(listFile);
            foreach (var line in lines)
            {
                var match = GetLibraryPathMatch().Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            #if DEBUG
                Logger.LogWriteLine($"Checking Steam Lib Path: {path}", LogType.Debug, true);
            #endif
                if (!Directory.Exists(path))
                {
                    continue;
                }
            #if DEBUG
                Logger.LogWriteLine($"    Path: {path} is exist", LogType.Debug, true);
            #endif
                libraries.Add(path);
            }
            return libraries;
        }

        private static string GetSteamPath()
        {
            object a = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "C:/Program Files (x86)/Steam");
            if (a == null) return null;
            return !Directory.Exists(a as string) ? null : ((string)a).Replace('\\', '/');
        }
    }
}
