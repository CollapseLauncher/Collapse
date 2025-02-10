using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
// ReSharper disable once IdentifierTypo
// ReSharper disable StringLiteralTypo

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
        private static int PrintUsage()
        {
            ProcessModule? processModule = Process.GetCurrentProcess().MainModule;
            if (processModule == null)
            {
                return int.MaxValue;
            }

            string executableName = Path.GetFileNameWithoutExtension(processModule.FileName);
            Console.WriteLine($"Usage:\r\n{executableName} [IsEnabled] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [Enable] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [EnableToTray] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [Disable] \"Scheduler name\" \"Executable path\"");
            Console.WriteLine($"{executableName} [DisableToTray] \"Scheduler name\" \"Executable path\"");

            return int.MaxValue;
        }

        internal static int Main(string[] args)
        {
            try
            {
                if (args.Length < 3)
                    return PrintUsage().ReturnValAsConsole();

                string action = args[0].ToLower();
                // ReSharper disable once IdentifierTypo
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

        private static TaskSched Create(TaskService taskService, string scheduleName, string execPath)
        {
            using TaskDefinition taskDefinition = TaskService.Instance.NewTask();
            taskDefinition.RegistrationInfo.Author      = "CollapseLauncher";
            taskDefinition.RegistrationInfo.Description = "Run Collapse Launcher automatically when computer starts";
            taskDefinition.Principal.LogonType          = TaskLogonType.InteractiveToken;
            taskDefinition.Principal.RunLevel           = TaskRunLevel.Highest;
            taskDefinition.Settings.Enabled             = false;
            taskDefinition.Triggers.Add(new LogonTrigger());
            taskDefinition.Actions.Add(new ExecAction(execPath));

            TaskSched task = taskService.RootFolder.RegisterTaskDefinition(scheduleName, taskDefinition);
            WriteConsole("New task schedule has been created!");
            return task;
        }

        private static void TryDelete(TaskService taskService, string scheduleName)
        {
            // Try to get the tasks
            TaskSched[] tasks = taskService.FindAllTasks(new Regex(scheduleName, RegexOptions.Compiled, TimeSpan.FromSeconds(5)), false);

            // If null, then ignore
            if (tasks == null || tasks.Length == 0)
            {
                WriteConsole($"None of the existing task: {scheduleName} exist but trying to delete, ignoring!");
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

        private static TaskSched? GetExistingTask(TaskService taskService, string scheduleName, string execPath)
        {
            // Try to get the tasks
            TaskSched[] tasks = taskService.FindAllTasks(new Regex(scheduleName, RegexOptions.Compiled, TimeSpan.FromSeconds(5)), false);

            // Try to get the first task
            TaskSched? task = tasks?
                .FirstOrDefault(x =>
                    x.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase));

            // Return null as empty
            if (task == null)
            {
                return null;
            }

            // Get actionPath
            string? actionPath = task.Definition.Actions.FirstOrDefault()?.ToString();

            // If actionPath is null, then return null as empty
            if (string.IsNullOrEmpty(actionPath))
            {
                return null;
            }

            // if actionPath isn't matched, then replace with current executable path
            if (!(!actionPath?.StartsWith(execPath, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return task;
            }

            // Check if the last action path runs on tray
            bool isLastHasTray = actionPath.EndsWith("tray", StringComparison.OrdinalIgnoreCase);

            // Register changes
            task.Definition.Actions.Clear();
            task.Definition.Actions.Add(new ExecAction(execPath, isLastHasTray ? "tray" : null));
            task.RegisterChanges();

            // If the task matches, then return the task
            return task;
        }

        private static int IsEnabled(string scheduleName, string execPath)
        {
            using TaskService taskService = new TaskService();
            // Get the task
            TaskSched? task = GetExistingTask(taskService, scheduleName, execPath);

            // If the task is null, return 0
            if (task == null)
            {
                return 0;
            }

            // If the task is not null, then do further check

            // Check if it's enabled with tray
            bool isOnTray = task.Definition.Actions.FirstOrDefault()?.ToString().EndsWith("tray", StringComparison.OrdinalIgnoreCase) ?? false;

            // If the task definition is enabled, then return 1 (true) or 2 (true with tray)
            if (task.Definition.Settings.Enabled)
                return isOnTray ? 2 : 1;

            // Otherwise, if the task exist but not enabled, then return 0 (false) or -1 (false with tray)
            return isOnTray ? -1 : 0;
        }

        private static void ToggleTask(bool isEnabled, bool isStartupToTray, string scheduleName, string execPath)
        {
            using TaskService taskService = new TaskService();
            // Try get existing task
            TaskSched? task = GetExistingTask(taskService, scheduleName, execPath);

            try
            {
                // If the task is null due to its non-existence or
                // there are some unmatched tasks, then try to recreate the task
                // by try deleting and create a new one.
                if (task == null)
                {
                    TryDelete(taskService, scheduleName);
                    task = Create(taskService, scheduleName, execPath);
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

        private static void WriteConsole(string message) =>
            Console.WriteLine(message);
    }
}
