using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Hi3Helper.Data
{
    public static class SteamTool
    {
        // Reference:
        // https://stackoverflow.com/a/67679123
        public static List<AppInfo> GetSteamApps(List<string> steamLibs)
        {
            var apps = new List<AppInfo>();
            foreach (var lib in steamLibs)
            {
                var appMetaDataPath = Path.Combine(lib, "SteamApps");
                var files = Directory.GetFiles(appMetaDataPath, "*.acf");
                foreach (var file in files)
                {
                    var appInfo = GetAppInfo(file);
                    if (appInfo != null)
                    {
                        apps.Add(appInfo);
                    }
                }
            }
            return apps;
        }

        public static AppInfo GetAppInfo(string appMetaFile)
        {
            var fileDataLines = File.ReadAllLines(appMetaFile);

            var dic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in fileDataLines)
            {
                var match = Regex.Match(line, @"\s*""(?<key>\w+)""\s+""(?<val>.*)""", RegexOptions.Compiled);
                if (match.Success)
                {
                    var key = match.Groups["key"].Value;
                    var val = match.Groups["val"].Value;
                    dic[key] = val;
                }
            }

            AppInfo appInfo = null;

            if (dic.Keys.Count > 0)
            {
                appInfo = new AppInfo();
                var appId = dic["appid"];
                var name = dic["name"];
                var installDir = dic["installDir"];

                var path = Path.GetDirectoryName(appMetaFile);
                var libGameRoot = Path.Combine(path, "common", installDir);

                if (!Directory.Exists(libGameRoot)) return null;

                appInfo.Id = int.Parse(appId);
                appInfo.Name = name;
                appInfo.Manifest = appMetaFile;
                appInfo.GameRoot = libGameRoot;
                appInfo.InstallDir = installDir;
                appInfo.SteamUrl = $"steam://runsteamid/{appId}";
                appInfo.Executable = GetExecutable(appInfo);
            }

            return appInfo;
        }

        private static string _appInfoText = null;
        public static string GetExecutable(AppInfo appInfo)
        {
            if (_appInfoText == null)
            {
                var appInfoFile = Path.Combine(GetSteamPath(), "appcache", "appinfo.vdf");
                var bytes = File.ReadAllBytes(appInfoFile);
                _appInfoText = Encoding.UTF8.GetString(bytes);
            }
            var startIndex = 0;
            int maxTries = 50;
            var fullName = "";

            do
            {
                var startOfDataArea = _appInfoText.IndexOf($"\x00\x01name\x00{appInfo.Name}\x00", startIndex);
                if (startOfDataArea < 0 && maxTries == 50) startOfDataArea = _appInfoText.IndexOf($"\x00\x01gamedir\x00{appInfo.Name}\x00", startIndex); //Alternative1
                if (startOfDataArea < 0 && maxTries == 50) startOfDataArea = _appInfoText.IndexOf($"\x00\x01name\x00{appInfo.Name}\x00", startIndex); //Alternative2
                if (startOfDataArea > 0)
                {
                    startIndex = startOfDataArea + 10;
                    int nextLaunch = -1;
                    do
                    {
                        var executable = _appInfoText.IndexOf($"\x00\x01executable\x00", startOfDataArea);
                        if (executable > -1 && nextLaunch == -1)
                        {
                            nextLaunch = _appInfoText.IndexOf($"\x00\x01launch\x00", executable);
                        }

                        if ((nextLaunch <= 0 || executable < nextLaunch) && executable > 0)
                        {
                            if (executable > 0)
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

                                fullName = Path.Combine(appInfo.GameRoot, filename);

                                startOfDataArea = executable + 1;
                                startIndex = startOfDataArea + 10;
                            }
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

            if (File.Exists(fullName)) return fullName;

            return null;
        }

        public static List<string> GetSteamLibs()
        {
            var steamPath = GetSteamPath();
            var libraries = new List<string>() { steamPath };

            var listFile = Path.Combine(steamPath, @"steamapps\libraryfolders.vdf");
            var lines = File.ReadAllLines(listFile);
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"""(?<path>\w:\\\\.*)""", RegexOptions.Compiled);
                if (match.Success)
                {
                    var path = match.Groups["path"].Value.Replace(@"\\", @"\");
                    if (Directory.Exists(path))
                    {
                        libraries.Add(path);
                    }
                }
            }
            return libraries;
        }

        private static string GetSteamPath()
        {
            object a = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", "C:/Program Files (x86)/Steam");
            if (a == null) return null;
            return ((string)a).Replace('\\', '/');
        }

        public class AppInfo
        {
            public int Id { get; internal set; }
            public string Name { get; internal set; }
            public string SteamUrl { get; internal set; }
            public string Manifest { get; internal set; }
            public string GameRoot { get; internal set; }
            public string Executable { get; internal set; }
            public string InstallDir { get; internal set; }

            public override string ToString()
            {
                return $"{Name} ({Id}) - {SteamUrl} - {Executable}";
            }
        }
    }
}
