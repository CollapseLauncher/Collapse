﻿using System;
using System.Linq;
using System.CommandLine;
using System.Text.RegularExpressions;
using System.CommandLine.NamingConventionBinder;
using static CollapseLauncher.InnerLauncherConfig;

namespace CollapseLauncher
{
    public static partial class ArgumentParser
    {
        static RootCommand rootCommand = new RootCommand();
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

                    args = Regex.Matches(convertedArg, @"[\""].+?[\""]|[^ ]+")
                                    .Cast<Match>()
                                    .Select(x => x.Value.Trim('"')).ToArray();
                } 
                
                args = args.Select(x => x.Trim('/')).Where(x => x != "").ToArray();
            }

            switch (args[0].ToLower())
            {
                case "hi3cacheupdate":
                    m_appMode = AppMode.Hi3CacheUpdater;
                    ParseHi3CacheUpdaterArguments(args);
                    break;
                case "update":
                    m_appMode = AppMode.Updater;
                    ParseUpdaterArguments(args);
                    break;
                case "elevateupdate":
                    m_appMode = AppMode.ElevateUpdater;
                    ParseElevateUpdaterArguments(args);
                    break;
                case "takeownership":
                    m_appMode = AppMode.InvokerTakeOwnership;
                    ParseTakeOwnershipArguments(args);
                    break;
                case "migrate":
                    m_appMode = AppMode.InvokerMigrate;
                    ParseMigrateArguments(false, args);
                    break;
                case "migratebhi3l":
                    m_appMode = AppMode.InvokerMigrate;
                    ParseMigrateArguments(true, args);
                    break;
                case "movesteam":
                    m_appMode = AppMode.InvokerMoveSteam;
                    ParseMoveSteamArguments(args);
                    break;
                case "oobesetup":
                    m_appMode = AppMode.OOBEState;
                    ParseOOBEArguments(args);
                    break;
                case "tray":
                    m_appMode = AppMode.StartOnTray;
                    break;
                case "open":
                    m_appMode = AppMode.Launcher;
                    break;
            }

            AddPublicCommands();
            rootCommand.Description = "Collapse Launcher is a game client for all currently released miHoYo/Hoyoverse games.\n" +
                                      "It supports installing games, repairing game files and much more!";

            if (rootCommand.Invoke(args) > 0)
            {
                new ArgumentException($"Argument to run this command is invalid! See the information above for more detail.");
                m_appMode = AppMode.Launcher;
                return;
            }
        }

        public static void ParseHi3CacheUpdaterArguments(params string[] args)
        {
            Command hi3cacheupdate = new Command("hi3cacheupdate", "Update the app or change the Release Channel of the app");
            rootCommand.AddCommand(hi3cacheupdate);
            AddHi3CacheUpdaterOptions(hi3cacheupdate);
        }

        public static void ParseUpdaterArguments(params string[] args)
        {
            Command updater = new Command("update", "Update the app or change the Release Channel of the app");
            rootCommand.AddCommand(updater);
            AddUpdaterOptions(updater);
        }

        public static void ParseElevateUpdaterArguments(params string[] args)
        {
            Command elevateUpdater = new Command("elevateupdate", "Elevate updater to run as administrator");
            rootCommand.AddCommand(elevateUpdater);
            AddUpdaterOptions(elevateUpdater);
        }

        public static void AddHi3CacheUpdaterOptions(Command command)
        {
            command.SetHandler(() =>
            {
            });
        }

        public static void AddUpdaterOptions(Command command)
        {
            Option<string> o_Input = new Option<string>(new string[] { "--input", "-i" }, "App path") { IsRequired = true };
            Option<AppReleaseChannel> o_Channel = new Option<AppReleaseChannel>(new string[] { "--channel", "-c" }, "App release channel") { IsRequired = true }.FromAmong();
            command.AddOption(o_Input);
            command.AddOption(o_Channel);
            command.Handler = CommandHandler.Create((string Input, AppReleaseChannel ReleaseChannel) =>
            {
                m_arguments.Updater = new ArgumentUpdater
                {
                    AppPath = Input,
                    UpdateChannel = ReleaseChannel
                };
            });
        }

        public static void ParseTakeOwnershipArguments(params string[] args)
        {
            var inputOption = new Option<string>(new string[] { "--input", "-i" }, description: "Folder path to claim") { IsRequired = true };
            var command = new Command("takeownership", "Take ownership of the folder");
            command.AddOption(inputOption);
            command.Handler = CommandHandler.Create<string>((string Input) =>
            {
                m_arguments.TakeOwnership = new ArgumentReindexer
                {
                    AppPath = Input
                };
            });
            var rootCommand = new RootCommand();
            rootCommand.AddCommand(command);
        }

        public static void ParseMigrateArguments(bool isBHI3L = false, params string[] args)
        {
            Command migrate;
            if (!isBHI3L)
                migrate = new Command("migrate", "Migrate Game from one installation to another location");
            else
                migrate = new Command("migratebhi3l", "Migrate Game from BetterHi3Launcher to another location");
            AddMigrateOptions(isBHI3L, migrate);
            rootCommand.AddCommand(migrate);
        }

        public static void ParseOOBEArguments(params string[] args)
        {
            rootCommand.AddCommand(new Command("oobesetup", "Starts Collapse in OOBE mode, to simulate first-time setup"));
        }

        private static void AddMigrateOptions(bool isBHI3L, Command command)
        {
            var inputOption = new Option<string>(new string[] { "--input", "-i" }, description: "Installation Source") { IsRequired = true };
            var outputOption = new Option<string>(new string[] { "--output", "-o" }, description: "Installation Target") { IsRequired = true };
            var rootCommand = new RootCommand();
            command.AddOption(inputOption);
            command.AddOption(outputOption);
            if (isBHI3L)
            {
                var gameVerOption = new Option<string>(new string[] { "--gamever", "-g" }, description: "Game version string (Format: x.x.x)") { IsRequired = true };
                var regLocOption = new Option<string>(new string[] { "--regloc", "-r" }, description: "Location of game registry for BetterHI3Launcher keys") { IsRequired = true };
                command.AddOption(gameVerOption);
                command.AddOption(regLocOption);
                command.Handler = CommandHandler.Create(
                    (string Input, string Output, string GameVer, string RegLoc) =>
                    {
                        m_arguments.Migrate = new ArgumentMigrate
                        {
                            InputPath = Input,
                            OutputPath = Output,
                            GameVer = GameVer,
                            RegLoc = RegLoc,
                            IsBHI3L = true
                        };
                    });
                return;
            }
            command.Handler = CommandHandler.Create(
                (string Input, string Output) =>
                {
                    m_arguments.Migrate = new ArgumentMigrate
                    {
                        InputPath = Input,
                        OutputPath = Output,
                        GameVer = null,
                        RegLoc = null,
                        IsBHI3L = false
                    };
                });
        }

        public static void ParseMoveSteamArguments(params string[] args)
        {
            var inputOption = new Option<string>(new string[] { "--input", "-i" }, description: "Installation Source") { IsRequired = true };
            var outputOption = new Option<string>(new string[] { "--output", "-o" }, description: "Installation Target") { IsRequired = true };
            var keyNameOption = new Option<string>(new string[] { "--keyname", "-k" }, description: "Registry key name") { IsRequired = true };
            var regLocOption = new Option<string>(new string[] { "--regloc", "-r" }, description: "Location of game registry for BetterHI3Launcher keys") { IsRequired = true };
            var command = new Command("movesteam", "Migrate Game from Steam to another location");
            command.AddOption(inputOption);
            command.AddOption(outputOption);
            command.AddOption(keyNameOption);
            command.AddOption(regLocOption);
            command.Handler = CommandHandler.Create(
                (string Input, string Output, string KeyName, string RegLoc) =>
                {
                    m_arguments.Migrate = new ArgumentMigrate
                    {
                        InputPath = Input,
                        OutputPath = Output,
                        KeyName = KeyName,
                        RegLoc = RegLoc,
                        IsBHI3L = false
                    };
                });
            var rootCommand = new RootCommand();
            rootCommand.AddCommand(command);
        }

        private static void AddPublicCommands()
        {
            rootCommand.AddCommand(new Command("tray", "Start Collapse in system tray"));
            AddOpenCommand();
        }

        private static void AddOpenCommand()
        {
            var gameOption = new Option<string>(new string[] { "--game", "-g" },
                description: "Game number/name\n" +
                             "e.g. 0 or \"Honkai Impact 3rd\""){ IsRequired = true };
            var regionOption = new Option<string>(new string[] { "--region", "-r" }, 
                description: "Region number/name\n" +
                             "e.g. For Genshin Impact, 0 or \"Global\" would load the Global region for the game") { IsRequired = false };
            var startGameOption = new Option<bool>(new string[] { "--play", "-p" }, description: "Start Game after loading the Game/Region") { IsRequired = false };
            var command = new Command("open", "Open the Launcher in a specific Game and Region (if specified).\n" +
                                "Note that game/regions provided will be ignored if invalid.\n" +
                                "Quotes are required if the game/region name has spaces.");
            command.AddOption(gameOption);
            command.AddOption(regionOption);
            command.AddOption(startGameOption);
            command.Handler = CommandHandler.Create(
                (string Game, string Region, bool Play) =>
                {
                    m_arguments.StartGame = new ArgumentStartGame
                    {
                        Game = Game,
                        Region = Region,
                        Play = Play
                    };
                });
            rootCommand.AddCommand(command);
        }

        public static void ResetRootCommand()
        {
            rootCommand = new RootCommand();
        }
    }

    public class Arguments
    {
        public ArgumentUpdater Updater { get; set; }
        public ArgumentReindexer Reindexer { get; set; }
        public ArgumentReindexer TakeOwnership { get; set; }
        public ArgumentMigrate Migrate { get; set; }
        public ArgumentStartGame StartGame { get; set; }
    }

    public class ArgumentUpdater
    {
        public string AppPath { get; set; }
        public AppReleaseChannel UpdateChannel { get; set; }
    }

    public class ArgumentReindexer
    {
        public string AppPath { get; set; }
        public string Version { get; set; }
    }

    public class ArgumentMigrate
    {
        public string InputPath { get; set; }
        public string OutputPath { get; set; }
        public string GameVer { get; set; }
        public string RegLoc { get; set; }
        public string KeyName { get; set; }
        public bool IsBHI3L { get; set; }
    }

    public class ArgumentStartGame
    {
        public string Game { get; set; }
        public string Region { get; set; }
        public bool Play { get; set; }
    }
}