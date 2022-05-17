using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using Newtonsoft.Json;

using Hi3Helper.Data;
using Hi3Helper.Preset;

using static CollapseLauncher.Invoker.SettingsGraphics;
using static Hi3Helper.InvokeProp;

namespace CollapseLauncher.Invoker
{
    public class MainClass
    {
        static string[] argument = Array.Empty<string>();
        public static void Main (string[] args)
        {
            // InitializeConsole();
            argument = args;

            try
            {
                switch (argument[0].ToLowerInvariant())
                {
                    case "migrate":
                        DoMigration();
                        break;
                    case "migratebhi3l":
                        DoMigrationBHI3L();
                        break;
                    case "applygamesettings":
                        DoApplyGameSettings(argument[1], argument[2]);
                        break;
                    case "loadgamesettings":
                        DoLoadGameSettings();
                        break;
                    case "assignoriginalpath":
                        DoAssignOriginalPath();
                        break;
                    case "takeownership":
                        TakeOwnership(argument[1]);
                        break;
                    case "aswindowhandler":
                        DoAsWindowHandler();
                        break;
                    case "movesteam":
                        DoMoveSteam();
                        break;
                    default:
                        Console.WriteLine($"Invalid argument!");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Invalid argument! {ex}");
                Console.ReadLine();
            }
        }

        static void DoAsWindowHandler()
        {
            IntPtr h_windowPtr = GetConsoleWindow();
            Console.WriteLine($"Window Handler Pointer: {h_windowPtr}");
            Console.WriteLine("Please define the pointer number above by adding this parameter to your unity game:");
            Console.WriteLine($"--parentHWND {h_windowPtr}");

            while (true)
            {
                Task.Delay(1000).Wait();
            }
        }

        static void DoMoveSteam()
        {
            string source = argument[1];
            string target = argument[2];
            string keyLoc = argument[3];
            string keyName = argument[4];

            MoveOperation(source, target);
            TakeOwnership(target);

            Registry.SetValue(keyLoc, keyName, target, RegistryValueKind.String);
        }

        static void MoveOperation(string source, string target)
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

        static void DoAssignOriginalPath()
        {
            string gameVer = argument[1];
            string gameDirectoryName = argument[2];
            string sourceGame = argument[3];
            IniFile iniFile = new IniFile();

            TakeOwnership(sourceGame);

            string iniSourceGameLocation = Path.Combine(sourceGame, gameDirectoryName, "config.ini");

            if (!File.Exists(iniSourceGameLocation))
            {
                iniFile.Add("General", new Dictionary<string, IniValue>
                {
                    { "channel", new IniValue(1) },
                    { "cps", new IniValue() },
                    { "game_version", new IniValue(gameVer) },
                    { "sub_channel", new IniValue(1) },
                    { "sdk_version", new IniValue() },
                });
                iniFile.Save(new FileStream(iniSourceGameLocation, FileMode.OpenOrCreate, FileAccess.ReadWrite));
            }
        }

        static void DoMigrationBHI3L()
        {
            string version = argument[1];
            string registryName = argument[2];
            string sourceGame = argument[3];
            string targetGame = argument[4];

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
                        .SetValue(registryName, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(info)), RegistryValueKind.Binary);
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
                iniFile.Save(new FileStream(Path.Combine(targetGame, "config.ini"), FileMode.OpenOrCreate, FileAccess.ReadWrite));
            }

            TakeOwnership(targetGame);
        }

        static void TakeOwnership(string target)
        {
            Console.WriteLine("Trying to append ownership of the folder. Please do not close this console window!");

            if (!Directory.Exists(target))
                Directory.CreateDirectory(target);

            Process takeOwner = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    Arguments = $"/c icacls \"{target}\" /T /Q /C /RESET"
                }
            };

            takeOwner.Start();
            takeOwner.WaitForExit();

            takeOwner = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    Arguments = $"/c takeown /f \"{target}\" /r /d y"
                }
            };

            takeOwner.Start();
            takeOwner.WaitForExit();
        }

        static void DoMigration()
        {
            string source = argument[1];
            string target = argument[2];

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

                TakeOwnership(targetGame);

                iniFile["launcher"]["game_install_path"] = targetGame.Replace('\\', '/');
                iniFile.Save(Path.Combine(source, "config.ini"));
            }
        }
    }
}