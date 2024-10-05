using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CollapseLauncher
{
    public class Migrate : TakeOwnership
    {
        public void DoMoveSteam(string source, string target, string keyLoc, string keyName)
        {
            MoveOperation(source, target);
            StartTakingOwnership(target);

            Registry.SetValue(keyLoc, keyName, target, RegistryValueKind.String);
        }

        public void MoveOperation(string source, string target)
        {
            string pripath = Path.GetPathRoot(source).ToLower();
            Console.WriteLine($"Using \"Cross-disk\" method while moving to target");
            string[] fileList = Directory.GetFiles(source, "*", SearchOption.AllDirectories);
            string basepath;
            string targetpath;

            for (int i = 0; i < fileList.Length; i++)
            {
                basepath = fileList[i].Substring(source.Length + 1);
                targetpath = Path.Combine(target, basepath);

                if (!Directory.Exists(Path.GetDirectoryName(targetpath)))
                    Directory.CreateDirectory(Path.GetDirectoryName(targetpath));

                if (File.Exists(targetpath))
                    File.Delete(targetpath);

                File.Move(fileList[i], targetpath);
                Console.WriteLine($"\rMoving {i + 1}/{fileList.Length}: {basepath}");
            }
        }

        public void DoMigrationBHI3L(string version, string registryName, string sourceGame, string targetGame)
        {
            IniFile iniFile = new IniFile();

            if (sourceGame != targetGame)
            {
                try
                {
                    targetGame = Path.Combine(targetGame, "Games");
                    MoveOperation(sourceGame, targetGame);
                    BHI3LInfo info = new BHI3LInfo
                    {
                        game_info = new BHI3LInfo_GameInfo
                        {
                            installed = true,
                            install_path = targetGame,
                            version = version,
                        }
                    };

                    Registry.CurrentUser.OpenSubKey(@"Software\Bp\Better HI3 Launcher", true)
                        .SetValue(registryName,
                        Encoding.UTF8.GetBytes(info.Serialize(InternalAppJSONContext.Default.BHI3LInfo)),
                        RegistryValueKind.Binary);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex}");
                }
            }

            if (!File.Exists(Path.Combine(targetGame, "config.ini")))
            {
                iniFile.Add("General", new Dictionary<string, IniValue>
                {
                    { "channel", new IniValue(1) },
                    { "cps", new IniValue() },
                    { "game_version", new IniValue(version) },
                    { "sub_channel", new IniValue(1) },
                    { "sdk_version", new IniValue() },
                });
                iniFile.Save(Path.Combine(targetGame, "config.ini"));
            }

            StartTakingOwnership(targetGame);
        }

        public void DoMigration(string source, string target)
        {
            string sourceGame, targetGame;

            IniFile iniFile = new IniFile();

            if (File.Exists(Path.Combine(source, "config.ini")))
            {
                iniFile.Load(Path.Combine(source, "config.ini"));
                sourceGame = ConverterTool.NormalizePath(iniFile["launcher"]["game_install_path"].ToString());
                targetGame = Path.Combine(target, Path.GetFileName(sourceGame));
                Console.WriteLine($"Moving From:\r\n\t{source}\r\nTo Destination:\r\n\t{target}");
                try
                {
                    MoveOperation(sourceGame, targetGame);
                }
                // Use move process if the method above throw a fucking nonsense reason even the folder is actually exist...
                catch (DirectoryNotFoundException)
                {
                    Process move = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            FileName = "cmd.exe",
                            UseShellExecute = false,
                            Arguments = $"/c move /Y \"{sourceGame}\" \"{targetGame}\""
                        }
                    };

                    move.Start();
                    move.WaitForExit();
                }

                StartTakingOwnership(targetGame);

                iniFile["launcher"]["game_install_path"] = targetGame.Replace('\\', '/');
                iniFile.Save(Path.Combine(source, "config.ini"));
            }
        }
    }
}
