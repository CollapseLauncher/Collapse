using System;
using System.IO;
using System.Diagnostics;

using Hi3Helper.Data;

namespace CollapseLauncher.Invoker
{
    public class MainClass
    {
        static string[] argument = Array.Empty<string>();
        public static void Main (string[] args)
        {
            argument = args;

            try
            {
                switch (argument[0].ToLowerInvariant())
                {
                    case "migrate":
                        DoMigration();
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

        static void DoMigration()
        {
            string source = argument[1];
            string target = argument[2];

            string sourceGame, targetGame = Path.Combine(target, "Games");

            IniFile iniFile = new IniFile();

            if (File.Exists(Path.Combine(source, "config.ini")))
            {
                iniFile.Load(Path.Combine(source, "config.ini"));
                sourceGame = ConverterTool.NormalizePath(iniFile["launcher"]["game_install_path"].ToString());

                Console.WriteLine($"Moving From:\r\n\t{source}\r\nTo Destination:\r\n\t{target}");
                try
                {
                    new DirectoryInfo(sourceGame).MoveTo(targetGame);
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

                Process takeOwner = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        FileName = "cmd.exe",
                        UseShellExecute = false,
                        Arguments = $"/c icacls \"{targetGame}\" /T /Q /C /RESET"
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
                        Arguments = $"/c takeown /f \"{targetGame}\" /r /d y"
                    }
                };

                takeOwner.Start();
                takeOwner.WaitForExit();

                iniFile["launcher"]["game_install_path"] = targetGame.Replace('\\', '/');
                iniFile.Save(Path.Combine(source, "config.ini"));
            }
        }
    }
}