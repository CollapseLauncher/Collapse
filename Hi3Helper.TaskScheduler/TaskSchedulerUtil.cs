using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums;
using Hi3Helper.Win32.Native.Interfaces.TaskScheduler;
using Hi3Helper.Win32.Native.Interfaces.TaskScheduler.Action;
using Hi3Helper.Win32.Native.Interfaces.TaskScheduler.Trigger;
using Hi3Helper.Win32.Native.Structs;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
// ReSharper disable once IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace Hi3Helper.TaskScheduler;

public static class TaskSchedulerUtil
{
    private const string RootFolder = "\\";

    private static ITaskService? _taskService;

    public static int IsEnabled(string scheduleName, string execPath)
    {
        EnsureTaskServiceConnected();

        IRegisteredTask? existingTask = GetExistingTask(_taskService, scheduleName, execPath, out IExecAction? execAction);
        if (existingTask == null) // If the task is null, return 0
        {
            return 0;
        }

        if (execAction == null) throw new NullReferenceException("Cannot get an existing IExecAction instance");

        existingTask.Definition(out ITaskDefinition? definition);
        if (definition == null) throw new NullReferenceException("Cannot get an existing ITaskDefinition instance");

        definition.GetSettings(out ITaskSettings? settings);
        if (settings == null) throw new NullReferenceException("Cannot get an existing ITaskSettings instance");

        // Get existing argument and enable state
        settings.GetEnabled(out VARIANT_BOOL isEnabled);
        execAction.GetArguments(out string? argument);

        // Check if it's enabled with tray
        bool isOnTray = argument?.Equals("tray", StringComparison.OrdinalIgnoreCase) ?? false;

        // If the task definition is enabled, then return 1 (true) or 2 (true with tray)
        if (isEnabled)
            return isOnTray ? 2 : 1;

        // Otherwise, if the task exist but not enabled, then return 0 (false) or -1 (false with tray)
        return isOnTray ? -1 : 0;
    }

    public static void ToggleTask(bool isEnabled, bool isStartupToTray, string scheduleName, string execPath)
    {
        EnsureTaskServiceConnected();

        IRegisteredTask? registeredTask = GetExistingTask(_taskService, scheduleName, execPath, out IExecAction? execAction);

        // Create new if existing task doesn't exist and remove a redundant one
        if (registeredTask == null)
        {
            TryDeleteRedundant(_taskService, scheduleName);
            registeredTask = Create(_taskService, scheduleName, execPath, out execAction);
        }

        registeredTask.Definition(out ITaskDefinition? definition);
        if (definition == null) throw new NullReferenceException("Cannot get an existing ITaskDefinition instance");

        definition.GetSettings(out ITaskSettings? settings);
        if (settings == null) throw new NullReferenceException("Cannot get an existing ITaskSettings instance");

        definition.GetActions(out IActionCollection? actionCollections);
        if (actionCollections == null) throw new NullReferenceException("Cannot get an existing IActionCollection instance");

        // Clear all collections and re-create the IExecAction
        actionCollections.Clear();
        actionCollections.Create(TASK_ACTION_TYPE.TASK_ACTION_EXEC, out IAction? action);
        if (action == null) throw new NullReferenceException("Cannot re-create an IAction instance");
        if (!ComMarshal<IAction>.TryCastComObjectAs(action,
                                                    out execAction,
                                                    out Exception? ex))
        {
            throw ex;
        }

        // Set IExecAction path and arguments, then set the toggle
        execAction.SetPath(execPath);
        execAction.SetArguments(isStartupToTray ? "tray" : null);
        settings.SetEnabled(isEnabled);

        _taskService.GetFolder(RootFolder, out ITaskFolder? rootFolder);
        if (rootFolder == null) throw new NullReferenceException("Cannot get an existing Root Folder ITaskFolder instance");
        rootFolder.RegisterTaskDefinition(scheduleName, definition, (int)TASK_CREATION.CreateOrUpdate, null, null, TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN, null, out _);
    }

    [MemberNotNull(nameof(_taskService))]
    private static void EnsureTaskServiceConnected()
    {
        if (_taskService != null)
        {
            return;
        }

        if (!ComMarshal<ITaskService>.TryCreateComObject(new Guid(TaskSchedulerIIDConst.CLSID_TaskScheduler),
                                                         CLSCTX.CLSCTX_INPROC_SERVER,
                                                         out ITaskService? taskServiceAot,
                                                         out Exception? ex))
        {
            throw ex;
        }

        // Try to connect to Task Scheduler service.
        taskServiceAot.Connect(null, null, null, null);
        _taskService = taskServiceAot;
    }

    private static IRegisteredTask Create(ITaskService taskService, string scheduleName, string execPath, out IExecAction? execAction, string folder = RootFolder)
    {
        // Create a new ITaskDefinition
        taskService.NewTask(0, out ITaskDefinition? taskDefinition);
        if (taskDefinition == null) throw new NullReferenceException("Cannot create a ITaskDefinition instance");

        // Try to create a new IRegistrationInfo instance
        taskDefinition.GetRegistrationInfo(out IRegistrationInfo? registrationInfo);
        if (registrationInfo == null) throw new NullReferenceException("Cannot create a IRegistrationInfo instance");

        // -- Set author and description
        registrationInfo.SetAuthor("CollapseLauncher");
        registrationInfo.SetDescription("Run Collapse Launcher automatically when computer starts");

        // Try to create a new IPrincipalInfo instance
        taskDefinition.GetPrincipal(out IPrincipal? principal);
        if (principal == null) throw new NullReferenceException("Cannot create a IPrincipal instance");

        // -- Set logon type and run level
        principal.LogonType(TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN);
        principal.RunLevel(TASK_RUNLEVEL_TYPE.TASK_RUNLEVEL_HIGHEST);

        // Try to create a new ITaskSettings instance
        taskDefinition.GetSettings(out ITaskSettings? settings);
        if (settings == null) throw new NullReferenceException("Cannot create a ITaskSettings instance");

        // -- Set the task to enable
        settings.SetEnabled(true);

        // Try to create a TASK_TRIGGER_LOGON
        taskDefinition.GetTriggers(out ITriggerCollection? triggers);
        if (triggers == null) throw new NullReferenceException("Cannot create a ITriggerCollection instance");
        triggers.Create(TASK_TRIGGER_TYPE2.TASK_TRIGGER_LOGON, out _);

        // Try to create a IExecAction
        taskDefinition.GetActions(out IActionCollection? actions);
        if (actions == null) throw new NullReferenceException("Cannot create a IActionCollection instance");
        actions.Create(TASK_ACTION_TYPE.TASK_ACTION_EXEC, out IAction? action);
        if (!ComMarshal<IAction>.TryCastComObjectAs(action ?? throw new NullReferenceException("Cannot create a IAction instance"),
                                                    out execAction,
                                                    out Exception? ex))
        {
            throw ex;
        }

        // Set path and register to the service
        execAction.SetPath(execPath);
        taskService.GetFolder(folder, out ITaskFolder? taskFolder);
        if (taskFolder == null) throw new NullReferenceException($"Cannot open the {folder} instance");

        taskFolder.RegisterTaskDefinition(scheduleName, taskDefinition, (int)TASK_CREATION.CreateOrUpdate, null, null, TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN, null, out IRegisteredTask? task);
        if (task == null) throw new NullReferenceException("Cannot register the IRegisteredTask instance");
        return task;
    }

    private static void TryDeleteRedundant(ITaskService taskService, string scheduleName, ITaskFolder? currentTaskFolder = null)
    {
        if (currentTaskFolder == null)
        {
            taskService.GetFolder(RootFolder, out currentTaskFolder);
            if (currentTaskFolder == null)
            {
                return;
            }
        }

        int tasksCount = 0;
        currentTaskFolder.GetTasks(1, out IRegisteredTaskCollection? tasksCollection);
        tasksCollection?.Count(out tasksCount);
        if (tasksCollection != null && tasksCount > 0)
        {
            for (int i = 0; i < tasksCount; i++)
            {
                tasksCollection.Item(i + 1, out IRegisteredTask? task);
                if (task == null) continue;

                task.Name(out string? name);
                if (!(name?.Equals(scheduleName, StringComparison.OrdinalIgnoreCase) ?? false)) continue;

                currentTaskFolder.DeleteTask(name, 0);
            }
        }

        currentTaskFolder.GetFolders(0, out ITaskFolderCollection? folders);
        if (folders == null) return;

        folders.Count(out int folderCount);
        if (folderCount == 0)
        {
            return;
        }

        for (int i = 0; i < folderCount; i++)
        {
            folders.Item(i + 1, out ITaskFolder? nextFolder);
            if (nextFolder == null) continue;

            TryDeleteRedundant(taskService, scheduleName, nextFolder);
        }
    }

    private static IRegisteredTask? GetExistingTask(ITaskService taskService, string scheduleName, string execPath, out IExecAction? execAction, string folder = RootFolder)
    {
        Unsafe.SkipInit(out ITaskFolder? taskFolder);
        Unsafe.SkipInit(out IRegisteredTaskCollection? tasks);
        Unsafe.SkipInit(out execAction);

        taskService.GetFolder(folder, out taskFolder);
        taskFolder?.GetTasks(1, out tasks);

        int taskCount = 0;
        tasks?.Count(out taskCount);

        for (int i = 0; i < taskCount; i++)
        {
            Unsafe.SkipInit(out IRegisteredTask? task);
            tasks?.Item(i + 1, out task);

            string? taskName = null;
            task?.Name(out taskName);
            if (!scheduleName.Equals(taskName) ||
                !IsExecutableActionEquals(task, execPath, out execAction))
            {
                continue;
            }

            return task;
        }

        return null;

        static bool IsExecutableActionEquals(IRegisteredTask? task, string execPath, out IExecAction? execAction)
        {
            Unsafe.SkipInit(out ITaskDefinition? definition);
            Unsafe.SkipInit(out IActionCollection? actions);
            Unsafe.SkipInit(out execAction);
            task?.Definition(out definition);
            definition?.GetActions(out actions);

            Unsafe.SkipInit(out int actionsCount);
            actions?.Count(out actionsCount);
            for (int i = 0; i < actionsCount; i++)
            {
                Unsafe.SkipInit(out IAction? action);
                actions?.Item(i + 1, out action);

                if (action == null) continue;

                if (!ComMarshal<IAction>.TryCastComObjectAs(action,
                                                            out execAction,
                                                            out Exception? ex))
                {
                    throw ex;
                }

                execAction.GetPath(out string? path);
                if (execPath.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
