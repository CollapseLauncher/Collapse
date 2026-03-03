using Hi3Helper;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Logger;
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
// ReSharper disable UnusedMember.Global
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

#nullable enable
namespace CollapseLauncher
{
    public static class ArgumentParser
    {
        private static readonly List<string> AllowedProtocolCommands = ["tray", "open"];

        private static RootCommand _rootCommand     = new();

        private const string OptsKeyInput     = "--input";
        private const string OptsKeyInputPath = OptsKeyInput  + "-path";
        private const string OptsKeyChannel   = "--channel";
        private const string OptsKeyGame      = "--game";
        private const string OptsKeyGamever   = OptsKeyGame + "ver";
        private const string OptsKeyKeyname   = "--keyname";
        private const string OptsKeyPlay      = "--play";
        private const string OptsKeyRegion    = "--region";
        private const string OptsKeyRegloc    = "--regloc";
        private const string OptsKeyOutput    = "--output";

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
            result.Invoke();
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

            var oInput = new Option<string>(OptsKeyInputPath)
            {
                Aliases = { "--input", "-i" },
                Description = "Output path for the output file",
                Required    = true
            };

            var oChannel = new Option<AppReleaseChannel>(OptsKeyChannel)
            {
                Aliases = { "-c" },
                Required = true,
                Description = "App release channel"
            };

            command.Options.Add(oInput);
            command.Options.Add(oChannel);
            command.SetAction(SetAction);

            return;

            static void SetAction(ParseResult result)
            {
                Dictionary<string, OptionResult> opts = result.ToOptionDictionary();

                m_arguments.Updater = new ArgumentUpdater
                {
                    AppPath       = opts.GetOptionValueOrDefault<string>(OptsKeyInputPath) ?? throw new NullReferenceException("App path must be defined!"),
                    UpdateChannel = opts.GetOptionValueOrDefault<AppReleaseChannel>(OptsKeyChannel)
                };
            }
        }

        private static void ParseTakeOwnershipArguments()
        {
            var inputOption = new Option<string>(OptsKeyInputPath)
            {
                Required = true,
                Description = "Folder path to claim"
            };

            var command = new Command("takeownership", "Take ownership of the folder");
            command.Options.Add(inputOption);
            command.SetAction(SetAction);
            _rootCommand.Add(command);

            return;

            static void SetAction(ParseResult result)
            {
                Dictionary<string, OptionResult> opts = result.ToOptionDictionary();

                m_arguments.TakeOwnership = new ArgumentReindexer
                {
                    AppPath = opts.GetOptionValueOrDefault<string>(OptsKeyInputPath) ?? throw new NullReferenceException("App path must be defined!")
                };
            }
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
            var inputOption = new Option<string>(OptsKeyInput)
            {
                Aliases     = { "-i" },
                Description = "Installation Source",
                Required    = true
            };

            var outputOption = new Option<string>(OptsKeyOutput)
            {
                Aliases     = { "-o" },
                Description = "Installation Target",
                Required    = true
            };

            command.Add(inputOption);
            command.Add(outputOption);
            if (isBhi3L)
            {
                var gameVerOption = new Option<string>(OptsKeyGamever)
                {
                    Aliases     = { "-g" },
                    Description = "Game version string (Format: x.x.x)",
                    Required    = true
                };

                var regLocOption = new Option<string>(OptsKeyRegloc)
                {
                    Aliases     = { "-r" },
                    Description = "Location of game registry for BetterHI3Launcher keys",
                    Required    = true
                };

                command.Add(gameVerOption);
                command.Add(regLocOption);
            }

            command.SetAction(SetAction);
            return;

            void SetAction(ParseResult result)
            {
                Dictionary<string, OptionResult> opts = result.ToOptionDictionary();

                m_arguments.Migrate = new ArgumentMigrate
                {
                    InputPath  = opts.GetOptionValueOrDefault<string>(OptsKeyInput) ?? throw new NullReferenceException("Input path must be defined!"),
                    OutputPath = opts.GetOptionValueOrDefault<string>(OptsKeyOutput) ?? throw new NullReferenceException("Output path must be defined!"),
                    GameVer    = isBhi3L ? opts.GetOptionValueOrDefault<string>(OptsKeyGamever) ?? throw new NullReferenceException("Game version must be defined!") : null!,
                    RegLoc     = isBhi3L ? opts.GetOptionValueOrDefault<string>(OptsKeyRegloc) ?? throw new NullReferenceException("Registry location must be defined!") : null!,
                    IsBhi3L    = isBhi3L
                };
            }
        }

        private static void ParseMoveSteamArguments()
        {
            var inputOption = new Option<string>(OptsKeyInput)
            {
                Aliases     = { "-i" },
                Required    = true,
                Description = "Steam Game Installation Source Path"
            };

            var outputOption = new Option<string>(OptsKeyOutput)
            {
                Aliases     = { "-o" },
                Required    = true,
                Description = "Steam Game Installation Target"
            };
            
            var keyNameOption = new Option<string>(OptsKeyKeyname)
            {
                Aliases     = { "-k" },
                Required    = true,
                Description = "Registry key name for the game"
            };
            
            var regLocOption = new Option<string>(OptsKeyRegloc)
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
            command.SetAction(SetAction);
            
            _rootCommand.Add(command);

            return;

            static void SetAction(ParseResult result)
            {
                Dictionary<string, OptionResult> opts = result.ToOptionDictionary();

                m_arguments.Migrate = new ArgumentMigrate
                {
                    InputPath = opts.GetOptionValueOrDefault<string>(OptsKeyInput) ?? throw new NullReferenceException("Input path must be defined!"),
                    OutputPath = opts.GetOptionValueOrDefault<string>(OptsKeyOutput) ?? throw new NullReferenceException("Output path must be defined!"),
                    KeyName = opts.GetOptionValueOrDefault<string>(OptsKeyKeyname) ?? throw new NullReferenceException("Game version must be defined!"),
                    RegLoc = opts.GetOptionValueOrDefault<string>(OptsKeyRegloc) ?? throw new NullReferenceException("Registry location must be defined!"),
                    IsBhi3L = false
                };
            }
        }

        private static void AddPublicCommands()
        {
            _rootCommand.Add(new Command("tray", "Start Collapse in system tray"));
            AddOpenCommand();
        }

        private static void AddOpenCommand()
        {
            var gameOption = new Option<string>(OptsKeyGame)
            {
                Aliases = { "-g" },
                Description = "Game number/name\n" +
                              "e.g. 0 or \"Honkai Impact 3rd\"",
                Required = true
            };

            var regionOption = new Option<string>(OptsKeyRegion)
            {
                Aliases = { "-r" },
                Description = "Region number/name\n" +
                              "e.g. For Genshin Impact, 0 or \"Global\" would load the Global region for the game",
                Required = false
            };
            
            var startGameOption = new Option<bool>(OptsKeyPlay)
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
            command.SetAction(SetAction);

            _rootCommand.Add(command);

            return;

            void SetAction(ParseResult result)
            {
                Dictionary<string, OptionResult> opts = result.ToOptionDictionary();

                m_arguments.StartGame = new ArgumentStartGame
                {
                    Game   = opts.GetOptionValueOrDefault<string>(OptsKeyGame) ?? throw new NullReferenceException($"Game name must be defined! Example: {gameOption.Description}"),
                    Region = opts.GetOptionValueOrDefault<string>(OptsKeyRegion) ?? null!,
                    Play   = opts.GetOptionValueOrDefault<bool>(OptsKeyPlay)
                };
            }
        }

        public static void ResetRootCommand()
        {
            _rootCommand = new RootCommand();
        }

        public static Dictionary<string, OptionResult> ToOptionDictionary(this ParseResult context)
        {
            Dictionary<string, OptionResult> opts = context.CommandResult
                                                           .Children
                                                           .OfType<OptionResult>()
                                                           .ToDictionary(x => x.Option.Name);

            return opts;
        }

        public static T? GetOptionValueOrDefault<T>(this Dictionary<string, OptionResult> dictionary, string key)
            => !dictionary.TryGetValue(key, out OptionResult? result)
                ? default
                : result.GetValueOrDefault<T>();
    }

    public class Arguments
    {
        public ArgumentUpdater    Updater       { get; set; }
        public ArgumentReindexer  Reindexer     { get; set; }
        public ArgumentReindexer  TakeOwnership { get; set; }
        public ArgumentMigrate    Migrate       { get; set; }
        public ArgumentStartGame? StartGame     { get; set; }
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
        public string? Game   { get; init; }
        public string? Region { get; init; }
        public bool    Play   { get; set; }
    }
}
