using Microsoft.Win32.TaskScheduler;
using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Velopack;
using Velopack.Locators;
using Velopack.NuGet;
using Velopack.Windows;
using TaskSched = Microsoft.Win32.TaskScheduler.Task;

namespace Hi3Helper.TaskScheduler
{
    public static class ReturnExtension
    {
        public static int ReturnValAsConsole(this int returnVal)
        {
            Console.WriteLine($"RETURNVAL_{returnVal}");
            return returnVal;
        }
    }

    public class Program
    {
        static int PrintUsage()
        {
            string executableName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);
            Console.WriteLine($"Usage:\r\n{executableName} [IsEnabled] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [Enable] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [EnableToTray] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [Disable] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [DisableToTray] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [RecreateIcons] \"Executable path\"");
            return int.MaxValue;
        }

        static int Main(string[] args)
        {
            try
            {
                if (args.Length == 2 && args[0].ToLower() == "recreateicons")
                    return RecreateIcons(args[1]);

                if (args.Length < 3)
                    return PrintUsage().ReturnValAsConsole();

                string action = args[0].ToLower();
                string schedName = args[1];
                string execPath = args[2];

                switch (action)
                {
                    case "isenabled":
                        return IsEnabled(schedName, execPath).ReturnValAsConsole();
                    case "enable":
                        ToggleTask(true, false, schedName, execPath);
                        break;
                    case "enabletotray":
                        ToggleTask(true, true, schedName, execPath);
                        break;
                    case "disable":
                        ToggleTask(false, false, schedName, execPath);
                        break;
                    case "disabletotray":
                        ToggleTask(false, true, schedName, execPath);
                        break;
                    default:
                        return PrintUsage().ReturnValAsConsole();
                }

                WriteConsole($"Operation: {action} \"{schedName}\" \"{execPath}\" has been executed!");
            }
            catch (Exception ex)
            {
                WriteConsole($"An unexpected error has occurred!\r\n{ex}");
                return int.MinValue.ReturnValAsConsole();
            }

            return 0.ReturnValAsConsole();
        }

        static int RecreateIcons(string executablePath)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            new Shortcuts(null, new CollapseVelopackLocator(executablePath)).CreateShortcut(executablePath, ShortcutLocation.Desktop | ShortcutLocation.StartMenuRoot, false, null);
#pragma warning restore CS0618 // Type or member is obsolete
            return 0;
        }

        static TaskSched Create(TaskService taskService, string schedName, string execPath)
        {
            using (TaskDefinition taskDefinition = TaskService.Instance.NewTask())
            {
                taskDefinition.RegistrationInfo.Author = "CollapseLauncher";
                taskDefinition.RegistrationInfo.Description = "Run Collapse Launcher automatically when computer starts";
                taskDefinition.Principal.LogonType = TaskLogonType.InteractiveToken;
                taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;
                taskDefinition.Settings.Enabled = false;
                taskDefinition.Triggers.Add(new LogonTrigger());
                taskDefinition.Actions.Add(new ExecAction(execPath));

                TaskSched task = taskService.RootFolder.RegisterTaskDefinition(schedName, taskDefinition);
                WriteConsole($"New task schedule has been created!");
                return task;
            }
        }

        static void TryDelete(TaskService taskService, string schedName)
        {
            // Try get the tasks
            TaskSched[] tasks = taskService.FindAllTasks(new System.Text.RegularExpressions.Regex(schedName), false);

            // If null, then ignore
            if (tasks == null || tasks.Length == 0)
            {
                WriteConsole($"None of the existing task: {schedName} exist but trying to delete, ignoring!");
                return;
            }

            // Remove possible tasks
            foreach (TaskSched task in tasks)
            {
                using (task)
                {
                    WriteConsole($"Deleting redundant task: {task.Name}");
                    taskService.RootFolder.DeleteTask(task.Name);
                }
            }
        }

        static TaskSched GetExistingTask(TaskService taskService, string schedName, string execPath)
        {
            // Try get the tasks
            TaskSched[] tasks = taskService.FindAllTasks(new System.Text.RegularExpressions.Regex(schedName), false);

            // Try get the first task
            TaskSched task = tasks?
                .FirstOrDefault(x =>
                    x.Name.Equals(schedName, StringComparison.OrdinalIgnoreCase));

            // Return null as empty
            if (task == null)
            {
                return null;
            }

            // Get actionPath
            string actionPath = task.Definition.Actions?.FirstOrDefault()?.ToString();

            // If actionPath is null, then return null as empty
            if (string.IsNullOrEmpty(actionPath))
            {
                return null;
            }

            // if actionPath isn't matched, then replace with current executable path
            if (!actionPath.StartsWith(execPath, StringComparison.OrdinalIgnoreCase))
            {
                // Check if the last action path runs on tray
                bool isLastHasTray = actionPath.EndsWith("tray", StringComparison.OrdinalIgnoreCase);

                // Register changes
                task.Definition.Actions.Clear();
                task.Definition.Actions.Add(new ExecAction(execPath, isLastHasTray ? "tray" : null));
                task.RegisterChanges();
            }

            // If the task matches, then return the task
            return task;
        }

        static int IsEnabled(string schedName, string execPath)
        {
            using (TaskService taskService = new TaskService())
            {
                // Get the task
                TaskSched task = GetExistingTask(taskService, schedName, execPath);

                // If the task is not null, then do further check
                if (task != null)
                {
                    // Check if it's enabled with tray
                    bool isOnTray = task.Definition?.Actions?.FirstOrDefault()?.ToString()?.EndsWith("tray", StringComparison.OrdinalIgnoreCase) ?? false;

                    // If the task definition is enabled, then return 1 (true) or 2 (true with tray)
                    if (task.Definition.Settings.Enabled)
                        return isOnTray ? 2 : 1;

                    // Otherwise, if the task exist but not enabled, then return 0 (false) or -1 (false with tray)
                    return isOnTray ? -1 : 0;
                }
            }

            // Otherwise, return 0
            return 0;
        }

        static void ToggleTask(bool isEnabled, bool isStartupToTray, string schedName, string execPath)
        {
            using (TaskService taskService = new TaskService())
            {
                // Try get existing task
                TaskSched task = GetExistingTask(taskService, schedName, execPath);

                try
                {
                    // If the task is null due to its' non existence or
                    // there are some unmatched tasks, then try recreate the task
                    // by try deleting and create a new one.
                    if (task == null)
                    {
                        TryDelete(taskService, schedName);
                        task = Create(taskService, schedName, execPath);
                    }

                    // Try clear the existing actions and set the new one
                    task.Definition.Actions.Clear();
                    task.Definition.Actions.Add(new ExecAction(execPath, isStartupToTray ? "tray" : null));
                    task.Definition.Settings.Enabled = isEnabled;
                }
                finally
                {
                    // Register the changes if it's not null.
                    if (task != null)
                    {
                        task.RegisterChanges();
                        task.Dispose();
                        WriteConsole($"ToggledStatus: isEnabled -> {isEnabled} & isStartupToTray -> {isStartupToTray}");
                    }
                }
            }
        }

        static void WriteConsole(string message) =>
            Console.WriteLine(message);
    }

    internal class CollapseVelopackLocator : VelopackLocator
    {
        const string SpecVersionFileName = "sq.version";

        /// <inheritdoc />
        public override string AppId { get; }

        /// <inheritdoc />
        public override string RootAppDir { get; }

        /// <inheritdoc />
        public override string UpdateExePath { get; }

        /// <inheritdoc />
        public override string AppContentDir { get; }

        /// <inheritdoc />
        public override SemanticVersion CurrentlyInstalledVersion { get; }

        /// <inheritdoc />
        public override string PackagesDir => CreateSubDirIfDoesNotExist(RootAppDir, "packages");

        /// <inheritdoc />
        public override bool IsPortable =>
            RootAppDir != null ? File.Exists(Path.Combine(RootAppDir, ".portable")) : false;

        /// <inheritdoc />
        public override string Channel { get; }

        /// <summary>
        /// Internal use only. Auto detect app details from the specified EXE path.
        /// </summary>
        internal CollapseVelopackLocator(string ourExePath)
            : base(null)
        {
            if (!VelopackRuntimeInfo.IsWindows)
                throw new NotSupportedException("Cannot instantiate WindowsLocator on a non-Windows system.");

            // We try various approaches here. Firstly, if Update.exe is in the parent directory,
            // we use that. If it's not present, we search for a parent "current" or "app-{ver}" directory,
            // which could designate that this executable is running in a nested sub-directory.
            // There is some legacy code here, because it's possible that we're running in an "app-{ver}" 
            // directory which is NOT containing a sq.version, in which case we need to infer a lot of info.

            ourExePath = Path.GetFullPath(ourExePath);
            string myDirPath = Path.GetDirectoryName(ourExePath);
            var myDirName = Path.GetFileName(myDirPath);
            var possibleUpdateExe = Path.GetFullPath(Path.Combine(myDirPath, "..", "Update.exe"));
            var ixCurrent = ourExePath.LastIndexOf("/current/", StringComparison.InvariantCultureIgnoreCase);

            Console.WriteLine($"Initializing {nameof(CollapseVelopackLocator)}");

            if (File.Exists(possibleUpdateExe))
            {
                Console.WriteLine("Update.exe found in parent directory");
                // we're running in a directory with an Update.exe in the parent directory
                var manifestFile = Path.Combine(myDirPath, SpecVersionFileName);
                if (PackageManifest.TryParseFromFile(manifestFile, out var manifest))
                {
                    // ideal, the info we need is in a manifest file.
                    Console.WriteLine("Located valid manifest file at: " + manifestFile);
                    AppId = manifest.Id;
                    CurrentlyInstalledVersion = manifest.Version;
                    RootAppDir = Path.GetDirectoryName(possibleUpdateExe);
                    UpdateExePath = possibleUpdateExe;
                    AppContentDir = myDirPath;
                    Channel = manifest.Channel;
                }
                else if (myDirName.StartsWith("app-", StringComparison.OrdinalIgnoreCase) && NuGetVersion.TryParse(myDirName.Substring(4), out var version))
                {
                    // this is a legacy case, where we're running in an 'root/app-*/' directory, and there is no manifest.
                    Console.WriteLine("Legacy app-* directory detected, sq.version not found. Using directory name for AppId and Version.");
                    AppId = Path.GetFileName(Path.GetDirectoryName(possibleUpdateExe));
                    CurrentlyInstalledVersion = version;
                    RootAppDir = Path.GetDirectoryName(possibleUpdateExe);
                    UpdateExePath = possibleUpdateExe;
                    AppContentDir = myDirPath;
                }
            }
            else if (ixCurrent > 0)
            {
                // this is an attempt to handle the case where we are running in a nested current directory.
                var rootDir = ourExePath.Substring(0, ixCurrent);
                var currentDir = Path.Combine(rootDir, "current");
                var manifestFile = Path.Combine(currentDir, SpecVersionFileName);
                possibleUpdateExe = Path.GetFullPath(Path.Combine(rootDir, "Update.exe"));
                // we only support parsing a manifest when we're in a nested current directory. no legacy fallback.
                if (File.Exists(possibleUpdateExe) && PackageManifest.TryParseFromFile(manifestFile, out var manifest))
                {
                    Console.WriteLine("Running in deeply nested directory. This is not an advised use-case.");
                    Console.WriteLine("Located valid manifest file at: " + manifestFile);
                    RootAppDir = Path.GetDirectoryName(possibleUpdateExe);
                    UpdateExePath = possibleUpdateExe;
                    AppId = manifest.Id;
                    CurrentlyInstalledVersion = manifest.Version;
                    AppContentDir = currentDir;
                    Channel = manifest.Channel;
                }
            }
        }
    }
}
