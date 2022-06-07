using System;
using System.CommandLine;

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
                case "update":
                    m_appMode = AppMode.Updater;
                    ParseUpdaterArguments(args);
                    break;
                case "elevateupdate":
                    m_appMode = AppMode.ElevateUpdater;
                    ParseElevateUpdaterArguments(args);
                    break;
                case "reindex":
                    m_appMode = AppMode.Reindex;
                    ParseReindexerArguments(args);
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
            }

            if (rootCommand.Invoke(args) > 0)
                throw new ArgumentException($"Argument to run this command is invalid! See the information above for more detail.");
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

        public static void AddUpdaterOptions()
        {
            Option o_Input, o_Channel;
            rootCommand.AddOption(o_Input = new Option<string>(new string[] { "--input", "-i" }, "Path of the app") { IsRequired = true });
            rootCommand.AddOption(o_Channel = new Option<AppReleaseChannel>(new string[] { "--channel", "-c" }, "Release channel of the app") { IsRequired = true }.FromAmong());
            rootCommand.SetHandler((string Input, AppReleaseChannel ReleaseChannel) =>
            {
                m_arguments.Updater = new ArgumentUpdater
                {
                    AppPath = Input,
                    UpdateChannel = ReleaseChannel
                };
            }, o_Input, o_Channel);
        }

        public static void ParseReindexerArguments(params string[] args)
        {
            Option o_Input, o_Ver;
            rootCommand.AddArgument(new Argument<string>("reindex", "Reindex the update files") { HelpName = null });
            rootCommand.AddOption(o_Input = new Option<string>(new string[] { "--input", "-i" }, "Path of the app") { IsRequired = true });
            rootCommand.AddOption(o_Ver = new Option<string>(new string[] { "--upver", "-u" }, "Update version of the app") { IsRequired = true });
            rootCommand.SetHandler((string Input, string Version) =>
            {
                m_arguments.Reindexer = new ArgumentReindexer
                {
                    AppPath = Input,
                    Version = Version
                };
            }, o_Input, o_Ver);
        }

        public static void ParseTakeOwnershipArguments(params string[] args)
        {
            Option o_Input;
            rootCommand.AddArgument(new Argument<string>("takeownership", "Take ownership of the folder") { HelpName = null });
            rootCommand.AddOption(o_Input = new Option<string>(new string[] { "--input", "-i" }, "Path of the folder to be taken") { IsRequired = true });
            rootCommand.SetHandler((string Input) =>
            {
                m_arguments.TakeOwnership = new ArgumentReindexer
                {
                    AppPath = Input
                };
            }, o_Input);
        }

        public static void ParseMigrateArguments(bool isBHI3L = false, params string[] args)
        {
            if (!isBHI3L)
                rootCommand.AddArgument(new Argument<string>("migrate", "Migrate Game from one installation to another location") { HelpName = null });
            else
                rootCommand.AddArgument(new Argument<string>("migratebhi3l", "Migrate Game from BetterHi3Launcher to another location") { HelpName = null });
            AddMigrateOptions(isBHI3L);
        }

        private static void AddMigrateOptions(bool isBHI3L)
        {
            Option o_Input, o_Output, o_GameVer = null, o_RegLoc = null;
            rootCommand.AddOption(o_Input = new Option<string>(new string[] { "--input", "-i" }, "Installation Source") { IsRequired = true });
            rootCommand.AddOption(o_Output = new Option<string>(new string[] { "--output", "-o" }, "Installation Target") { IsRequired = true });
            if (isBHI3L)
            {
                rootCommand.AddOption(o_GameVer = new Option<string>(new string[] { "--gamever", "-g" }, "Game version string (in x.x.x format)") { IsRequired = true });
                rootCommand.AddOption(o_RegLoc = new Option<string>(new string[] { "--regloc", "-r" }, "Location of game registry in BetterHI3Launcher keys") { IsRequired = true });
                rootCommand.SetHandler((string Input, string Output, string GameVer, string RegLoc) =>
                {
                    m_arguments.Migrate = new ArgumentMigrate
                    {
                        InputPath = Input,
                        OutputPath = Output,
                        GameVer = GameVer,
                        RegLoc = RegLoc,
                        IsBHI3L = true
                    };
                }, o_Input, o_Output, o_GameVer, o_RegLoc);

                return;
            }

            rootCommand.SetHandler((string Input, string Output) =>
            {
                m_arguments.Migrate = new ArgumentMigrate
                {
                    InputPath = Input,
                    OutputPath = Output,
                    GameVer = null,
                    RegLoc = null,
                    IsBHI3L = false
                };
            }, o_Input, o_Output);
        }

        public static void ParseMoveSteamArguments(params string[] args)
        {
            Option o_Input, o_Output, o_KeyName = null, o_RegLoc = null;
            rootCommand.AddArgument(new Argument<string>("movesteam", "Migrate Game from Steam to another location") { HelpName = null });
            rootCommand.AddOption(o_Input = new Option<string>(new string[] { "--input", "-i" }, "Installation Source") { IsRequired = true });
            rootCommand.AddOption(o_Output = new Option<string>(new string[] { "--output", "-o" }, "Installation Target") { IsRequired = true });
            rootCommand.AddOption(o_KeyName = new Option<string>(new string[] { "--keyname", "-k" }, "Registry key name") { IsRequired = true });
            rootCommand.AddOption(o_RegLoc = new Option<string>(new string[] { "--regloc", "-r" }, "Location of game registry in BetterHI3Launcher keys") { IsRequired = true });
            rootCommand.SetHandler((string Input, string Output, string KeyName, string RegLoc) =>
            {
                m_arguments.Migrate = new ArgumentMigrate
                {
                    InputPath = Input,
                    OutputPath = Output,
                    KeyName = KeyName,
                    RegLoc = RegLoc,
                    IsBHI3L = false
                };
            }, o_Input, o_Output, o_KeyName, o_RegLoc);
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
}
