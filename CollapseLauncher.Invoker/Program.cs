using System;
using System.IO;

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
            catch (Exception)
            {
                Console.WriteLine($"Invalid argument!");
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
                new DirectoryInfo(sourceGame).MoveTo(targetGame);

                iniFile["launcher"]["game_install_path"] = targetGame.Replace('\\', '/');
                iniFile.Save(Path.Combine(source, "config.ini"));
            }
        }
    }
}