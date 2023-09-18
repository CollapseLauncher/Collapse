using System;
using System.CommandLine;
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
            }

            if (rootCommand.Invoke(args) > 0)
            {
                new ArgumentException($"Argument to run this command is invalid! See the information above for more detail.");
                m_appMode = AppMode.Launcher;
                return;
            }
        }

        public static void ParseHi3CacheUpdaterArguments(params string[] args)
        {
            rootCommand.AddArgument(new Argument<string>("hi3cacheupdate", "Update the app or change the Release Channel of the app") { HelpName = null });
            AddHi3CacheUpdaterOptions();
        }

        public static void ParseUpdaterArguments(params string[] args)
        {
            rootCommand.AddArgument(new Argument<string>("update", "Update the app or change the Release Channel of the app") { HelpName = null });
            AddUpdaterOptions();
        }

        public static void ParseElevateUpdaterArguments(params string[] args)
        {
            rootCommand.AddArgument(new Argument<string>("elevateupdate", "Elevate updater to run as administrator") { HelpName = null });
            AddUpdaterOptions();
        }

        public static void AddHi3CacheUpdaterOptions()
        {
            rootCommand.SetHandler(() =>
            {
            });
        }

        //public static void AddUpdaterOptions()
        //{
        //    Option o_Input, o_Channel;
        //    rootCommand.AddOption(o_Input = new Option<string>(new string[] { "--input", "-i" }, "Path of the app") { IsRequired = true });
        //    rootCommand.AddOption(o_Channel = new Option<AppReleaseChannel>(new string[] { "--channel", "-c" }, "Release channel of the app") { IsRequired = true }.FromAmong());
        //    rootCommand.SetHandler((string Input, AppReleaseChannel ReleaseChannel) =>
        //    {
        //        m_arguments.Updater = new ArgumentUpdater
        //        {
        //            AppPath = Input,
        //            UpdateChannel = ReleaseChannel
        //        };
        //    }, o_Input, o_Channel);
        //}

        public static void AddUpdaterOptions()
        {
            Option<string> o_Input = new Option<string>(new string[] { "--input", "-i" }, "Path of the app") { IsRequired = true };
            Option<AppReleaseChannel> o_Channel = new Option<AppReleaseChannel>(new string[] { "--channel", "-c" }, "Release channel of the app") { IsRequired = true }.FromAmong();
            rootCommand.AddOption(o_Input);
            rootCommand.AddOption(o_Channel);
            rootCommand.Handler = CommandHandler.Create((string Input, AppReleaseChannel ReleaseChannel) =>
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
            var inputOption = new Option<string>(new string[] { "--input", "-i" }, description: "Path of the folder to be taken") { IsRequired = true };
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
            if (!isBHI3L)
                rootCommand.AddArgument(new Argument<string>("migrate", "Migrate Game from one installation to another location") { HelpName = null });
            else
                rootCommand.AddArgument(new Argument<string>("migratebhi3l", "Migrate Game from BetterHi3Launcher to another location") { HelpName = null });
            AddMigrateOptions(isBHI3L);
        }

        public static void ParseOOBEArguments(params string[] args)
        {
            rootCommand.AddArgument(new Argument<string>("oobesetup", "Starts Collapse in OOBE mode, to simulate first-time setup") { HelpName = null });
        }

        private static void AddMigrateOptions(bool isBHI3L)
        {
            var inputOption = new Option<string>(new string[] { "--input", "-i" }, description: "Installation Source") { IsRequired = true };
            var outputOption = new Option<string>(new string[] { "--output", "-o" }, description: "Installation Target") { IsRequired = true };
            var rootCommand = new RootCommand();
            rootCommand.AddOption(inputOption);
            rootCommand.AddOption(outputOption);
            if (isBHI3L)
            {
                var gameVerOption = new Option<string>(new string[] { "--gamever", "-g" }, description: "Game version string (in x.x.x format)") { IsRequired = true };
                var regLocOption = new Option<string>(new string[] { "--regloc", "-r" }, description: "Location of game registry in BetterHI3Launcher keys") { IsRequired = true };
                rootCommand.AddOption(gameVerOption);
                rootCommand.AddOption(regLocOption);
                rootCommand.Handler = CommandHandler.Create<string, string, string, string>(
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
            rootCommand.Handler = CommandHandler.Create<string, string>(
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
            var regLocOption = new Option<string>(new string[] { "--regloc", "-r" }, description: "Location of game registry in BetterHI3Launcher keys") { IsRequired = true };
            var command = new Command("movesteam", "Migrate Game from Steam to another location");
            command.AddOption(inputOption);
            command.AddOption(outputOption);
            command.AddOption(keyNameOption);
            command.AddOption(regLocOption);
            command.Handler = CommandHandler.Create<string, string, string, string>(
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
    }
}

    public class Arguments
    {
        public ArgumentUpdater Updater { get; set; }
        public ArgumentReindexer Reindexer { get; set; }
        public ArgumentReindexer TakeOwnership { get; set; }
        public ArgumentMigrate Migrate { get; set; }
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