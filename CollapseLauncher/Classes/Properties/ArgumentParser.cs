using Hi3Helper;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Linq;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global

namespace CollapseLauncher
{
    public static class ArgumentParser
    {
        private static readonly List<string> AllowedProtocolCommands = ["tray", "open"];

        private static RootCommand _rootCommand = new();

        public static void ParseArguments(params string[] args)
        {
            if (args.Length == 0)
            {
                m_appMode = AppMode.Launcher;
                return;
            }

            if (args[0].StartsWith("collapse://")) 
            {
                args[0] = args[0].Replace("collapse://", "");

                if (args[0] == "/" || args[0] == "")
                {
                    m_appMode = AppMode.Launcher;
                    return;
                }

                // Convert web browser format (contains %20 or %22 but no " or space)
                if ((args[0].Contains("%20") || args[0].Contains("%22")) 
                    && !(args[0].Contains(' ') || args[0].Contains('"')))
                {
                    string convertedArg = args[0].Replace("%20", " ").Replace("%22", "\"");

                    args = AppActivation.MatchDoubleOrEscapedQuote()
                        .Matches(convertedArg)
                        .Select(x => x.Value.Trim('"'))
                        .ToArray();
                } 
                
                args = args.Select(x => x.Trim('/')).Where(x => x != "").ToArray();

                if (AllowedProtocolCommands.IndexOf(args[0]) == -1)
                {
                    LogWriteLine("This command does not exist or cannot be activated using a protocol.", LogType.Error);
                    m_appMode = AppMode.Launcher;
                    return;
                }
            }

            switch (args[0].ToLower())
            {
                case "hi3cacheupdate":
                    m_appMode = AppMode.Hi3CacheUpdater;
                    ParseHi3CacheUpdaterArguments();
                    break;
                case "update":
                    m_appMode = AppMode.Updater;
                    ParseUpdaterArguments();
                    break;
                case "elevateupdate":
                    m_appMode = AppMode.ElevateUpdater;
                    ParseElevateUpdaterArguments();
                    break;
                case "takeownership":
                    m_appMode = AppMode.InvokerTakeOwnership;
                    ParseTakeOwnershipArguments();
                    break;
                case "migrate":
                    m_appMode = AppMode.InvokerMigrate;
                    ParseMigrateArguments(false);
                    break;
                case "migratebhi3l":
                    m_appMode = AppMode.InvokerMigrate;
                    ParseMigrateArguments(true);
                    break;
                case "movesteam":
                    m_appMode = AppMode.InvokerMoveSteam;
                    ParseMoveSteamArguments();
                    break;
                case "oobesetup":
                    m_appMode = AppMode.OOBEState;
                    ParseOobeArguments();
                    break;
                case "tray":
                    m_appMode = AppMode.StartOnTray;
                    break;
                case "open":
                    m_appMode = AppMode.Launcher;
                    break;
                case "generatevelopackmetadata":
                    m_appMode = AppMode.GenerateVelopackMetadata;
                    ParseGenerateVelopackMetadataArguments();
                    break;
            }

            AddPublicCommands();
            _rootCommand.Description = "Collapse Launcher is a game client for all currently released miHoYo/Hoyoverse games.\n" +
                                      "It supports installing games, repairing game files and much more!";
            
            var result = _rootCommand.Parse(args);
            
            if (result.Errors.Count <= 0)
                return;

            var e = new ArgumentException("Argument to run this command is invalid! See the information above for more detail.");
            Console.WriteLine(e);
            m_appMode = AppMode.Launcher;
        }

        private static void ParseHi3CacheUpdaterArguments()
        {
            Command hi3CacheUpdate = new Command("hi3cacheupdate", "Update the app or change the Release Channel of the app");
            _rootCommand.Add(hi3CacheUpdate);
            AddHi3CacheUpdaterOptions();
        }

        private static void ParseUpdaterArguments()
        {
            Command updater = new Command("update", "Update the app or change the Release Channel of the app");
            _rootCommand.Add(updater);
            AddUpdaterOptions(updater);
        }

        private static void ParseElevateUpdaterArguments()
        {
            Command elevateUpdater = new Command("elevateupdate", "Elevate updater to run as administrator");
            _rootCommand.Add(elevateUpdater);
            AddUpdaterOptions(elevateUpdater);
        }

        private static void AddHi3CacheUpdaterOptions() { }

        private static void AddUpdaterOptions(Command command)
        {
            var oInput = new Option<string>("--input-path")
            {
                Description = "Output path for the output file",
                Required    = true
            };

            var oChannel = new Option<AppReleaseChannel>("--channel")
            {
                Aliases = { "-c" },
                Required = true,
                Description = "App release channel"
            };

            command.Options.Add(oInput);
            command.Options.Add(oChannel);
            
            command.Action = CommandHandler.Create((string inputPath, AppReleaseChannel channel) =>
            {
                m_arguments.Updater = new ArgumentUpdater
                {
                    AppPath = inputPath,
                    UpdateChannel = channel
                };
            });
        }

        private static void ParseTakeOwnershipArguments()
        {
            var inputOption = new Option<string>("--input-path")
            {
                Required = true,
                Description = "Folder path to claim"
            };

            var command = new Command("takeownership", "Take ownership of the folder");
            command.Options.Add(inputOption);
            command.Action = CommandHandler.Create((string inputPath) =>
            {
                m_arguments.TakeOwnership = new ArgumentReindexer
                {
                    AppPath = inputPath
                };
            });
            
            _rootCommand.Add(command);
        }

        private static void ParseMigrateArguments(bool isBhi3L)
        {
            var migrate = !isBhi3L ? new Command("migrate", "Migrate Game from one installation to another location") : new Command("migratebhi3l", "Migrate Game from BetterHi3Launcher to another location");
            AddMigrateOptions(isBhi3L, migrate);
            _rootCommand.Add(migrate);
        }

        private static void ParseOobeArguments()
        {
            _rootCommand.Add(new Command("oobesetup", "Starts Collapse in OOBE mode, to simulate first-time setup"));
        }

        private static void ParseGenerateVelopackMetadataArguments()
        {
            _rootCommand.Add(new Command("generatevelopackmetadata", "Generate Velopack metadata to enable update management"));
        }

        private static void AddMigrateOptions(bool isBhi3L, Command command)
        {
            var inputOption = new Option<string>("--input")
            {
                Aliases     = { "-i" },
                Description = "Installation Source",
                Required    = true
            };

            var outputOption = new Option<string>("--output")
            {
                Aliases     = { "-o" },
                Description = "Installation Target",
                Required    = true
            };

            command.Add(inputOption);
            command.Add(outputOption);
            if (isBhi3L)
            {
                var gameVerOption = new Option<string>("--gamever")
                {
                    Aliases     = { "-g" },
                    Description = "Game version string (Format: x.x.x)",
                    Required    = true
                };

                var regLocOption = new Option<string>("--regloc")
                {
                    Aliases     = { "-r" },
                    Description = "Location of game registry for BetterHI3Launcher keys",
                    Required    = true
                };

                command.Add(gameVerOption);
                command.Add(regLocOption);
                command.Action = CommandHandler.Create(
                    (string input, string output, string gameVer, string regLoc) =>
                    {
                        m_arguments.Migrate = new ArgumentMigrate
                        {
                            InputPath = input,
                            OutputPath = output,
                            GameVer = gameVer,
                            RegLoc = regLoc,
                            IsBhi3L = true
                        };
                    });
                return;
            }
            command.Action = CommandHandler.Create(
                (string input, string output) =>
                {
                    m_arguments.Migrate = new ArgumentMigrate
                    {
                        InputPath = input,
                        OutputPath = output,
                        GameVer = null,
                        RegLoc = null,
                        IsBhi3L = false
                    };
                });
        }

        private static void ParseMoveSteamArguments()
        {
            var inputOption = new Option<string>("--input")
            {
                Aliases     = { "-i" },
                Required    = true,
                Description = "Steam Game Installation Source Path"
            };

            var outputOption = new Option<string>("--output")
            {
                Aliases     = { "-o" },
                Required    = true,
                Description = "Steam Game Installation Target"
            };
            
            var keyNameOption = new Option<string>("--keyname")
            {
                Aliases     = { "-k" },
                Required    = true,
                Description = "Registry key name for the game"
            };
            
            var regLocOption = new Option<string>("--regloc")
            {
                Aliases     = { "-r" },
                Required    = true,
                Description = "Location of game registry for BetterHI3Launcher keys"
            };

            var command = new Command("movesteam", "Migrate Game from Steam to another location");
            command.Options.Add(inputOption);
            command.Options.Add(outputOption);
            command.Options.Add(keyNameOption);
            command.Options.Add(regLocOption);
            command.Action = CommandHandler.Create(
                (string input, string output, string keyName, string regLoc) =>
                {
                    m_arguments.Migrate = new ArgumentMigrate
                    {
                        InputPath = input,
                        OutputPath = output,
                        KeyName = keyName,
                        RegLoc = regLoc,
                        IsBhi3L = false
                    };
                });
            
            _rootCommand.Add(command);
        }

        private static void AddPublicCommands()
        {
            _rootCommand.Add(new Command("tray", "Start Collapse in system tray"));
            AddOpenCommand();
        }

        private static void AddOpenCommand()
        {
            var gameOption = new Option<string>("--game")
            {
                Aliases = { "-g" },
                Description = "Game number/name\n" +
                              "e.g. 0 or \"Honkai Impact 3rd\"",
                Required = true
            };

            var regionOption = new Option<string>("--region")
            {
                Aliases = { "-r" },
                Description = "Region number/name\n" +
                              "e.g. For Genshin Impact, 0 or \"Global\" would load the Global region for the game",
                Required = false
            };
            
            var startGameOption = new Option<bool>("--play")
            {
                Aliases = { "-p" },
                Description = "Start Game after loading the Game/Region",
                Required = false
            };

            var command = new Command("open", "Open the Launcher in a specific Game and Region (if specified).\n" +
                                "Note that game/regions provided will be ignored if invalid.\n" +
                                "Quotes are required if the game/region name has spaces.");
            command.Options.Add(gameOption);
            command.Options.Add(regionOption);
            command.Options.Add(startGameOption);
            command.Action = CommandHandler.Create(
                (string game, string region, bool play) =>
                {
                    m_arguments.StartGame = new ArgumentStartGame
                    {
                        Game = game,
                        Region = region,
                        Play = play
                    };
                });
            _rootCommand.Add(command);
        }

        public static void ResetRootCommand()
        {
            _rootCommand = new RootCommand();
        }
    }

    public class Arguments
    {
        public ArgumentUpdater   Updater       { get; set; }
        public ArgumentReindexer Reindexer     { get; set; }
        public ArgumentReindexer TakeOwnership { get; set; }
        public ArgumentMigrate   Migrate       { get; set; }
        public ArgumentStartGame StartGame     { get; set; }
    }

    public class ArgumentUpdater
    {
        public string            AppPath       { get; init; }
        public AppReleaseChannel UpdateChannel { get; init; }
    }

    public class ArgumentReindexer
    {
        public string AppPath { get; init; }
        public string Version { get; set; }
    }

    public class ArgumentMigrate
    {
        public string InputPath  { get; init; }
        public string OutputPath { get; init; }
        public string GameVer    { get; init; }
        public string RegLoc     { get; init; }
        public string KeyName    { get; init; }
        public bool   IsBhi3L    { get; init; }
    }

    public class ArgumentStartGame
    {
        public string Game   { get; init; }
        public string Region { get; init; }
        public bool   Play   { get; set; }
    }
}
