using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CollapseLauncher.Helper
{
    internal static class TaskSchedulerHelper
    {
        private const string _collapseStartupTaskName = "CollapseLauncherStartupTask";

        internal static bool IsInitialized = false;
        internal static bool Cached_IsOnTrayEnabled = false;
        internal static bool Cached_IsEnabled = false;

        internal static bool IsOnTrayEnabled()
        {
            if (!IsInitialized)
                InvokeGetStatusCommand();

            return Cached_IsOnTrayEnabled;
        }

        internal static bool IsEnabled()
        {
            if (!IsInitialized)
                InvokeGetStatusCommand();

            return Cached_IsEnabled;
        }

        internal static void InvokeGetStatusCommand()
        {
            // Get current stub or main executable path
            string currentExecPath = MainEntryPoint.FindCollapseStubPath();

            // Build the argument and mode to set
            StringBuilder argumentBuilder = new StringBuilder();
            argumentBuilder.Append("IsEnabled");

            // Append task name and stub path
            AppendTaskNameAndPathArgument(argumentBuilder);

            // Store argument builder as string
            string argumentString = argumentBuilder.ToString();

            // Invoke command and get return code
            int returnCode = GetInvokeCommandReturnCode(argumentString);

            (Cached_IsEnabled, Cached_IsOnTrayEnabled) = returnCode switch
            {
                // -1 means task is disabled with tray enabled
                -1 => (false, true),
                // 0 means task is disabled with tray disabled
                0  => (false, false),
                // 1 means task is enabled with tray disabled
                1  => (true, false),
                // 2 means task is enabled with tray enabled
                2  => (true, true),
                // Otherwise, return both disabled (due to failure)
                _ => (false, false)
            };

            // Print init determination
            CheckInitDetermination(returnCode);
        }

        private static void CheckInitDetermination(int returnCode)
        {
            // If the return code is within range, then set as initialized
            if (returnCode > -2 && returnCode < 3)
            {
                // Set as initialized
                IsInitialized = true;
            }
            // Otherwise, log the return code
            else
            {
                string reason = returnCode switch
                {
                    int.MaxValue => "ARGUMENT_INVALID",
                    int.MinValue => "UNHANDLED_ERROR",
                    short.MaxValue => "INTERNALINVOKE_ERROR",
                    short.MinValue => "APPLET_NOTFOUND",
                    _ => $"UNKNOWN_{returnCode}"
                };
                Logger.LogWriteLine($"Error while getting task status from applet with reason: {reason}", LogType.Error, true);
            }
        }

        internal static void ToggleTrayEnabled(bool isEnabled)
        {
            Cached_IsOnTrayEnabled = isEnabled;
            InvokeToggleCommand();
        }

        internal static void ToggleEnabled(bool isEnabled)
        {
            Cached_IsEnabled = isEnabled;
            InvokeToggleCommand();
        }

        private static void InvokeToggleCommand()
        {
            // Build the argument and mode to set
            StringBuilder argumentBuilder = new StringBuilder();
            argumentBuilder.Append(Cached_IsEnabled ? "Enable" : "Disable");

            // Append argument whether to toggle the tray or not
            if (Cached_IsOnTrayEnabled)
                argumentBuilder.Append("ToTray");

            // Append task name and stub path
            AppendTaskNameAndPathArgument(argumentBuilder);

            // Store argument builder as string
            string argumentString = argumentBuilder.ToString();

            // Invoke applet
            int returnCode = GetInvokeCommandReturnCode(argumentString);

            // Print init determination
            CheckInitDetermination(returnCode);
        }

        private static void AppendTaskNameAndPathArgument(StringBuilder argumentBuilder)
        {
            // Get current stub or main executable path
            string currentExecPath = MainEntryPoint.FindCollapseStubPath();

            // Build argument to the task name
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(_collapseStartupTaskName);
            argumentBuilder.Append('"');

            // Build argument to the executable path
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(currentExecPath);
            argumentBuilder.Append('"');
        }

        internal static void RecreateIconShortcuts()
        {
            // Build the argument and get the current executable path
            StringBuilder argumentBuilder = new StringBuilder();
            argumentBuilder.Append("RecreateIcons");
            string currentExecPath = LauncherConfig.AppExecutablePath;

            // Build argument to the executable path
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(currentExecPath);
            argumentBuilder.Append('"');

            // Store argument builder as string
            string argumentString = argumentBuilder.ToString();

            // Invoke applet
            int returnCode = GetInvokeCommandReturnCode(argumentString);

            // Print init determination
            CheckInitDetermination(returnCode);
        }

        private static int GetInvokeCommandReturnCode(string argument)
        {
            const string RETURNVALMARK = "RETURNVAL_";

            // Get the applet path and check if the file exist
            string appletPath = Path.Combine(LauncherConfig.AppFolder, "Lib", "win-x64", "Hi3Helper.TaskScheduler.exe");
            if (!File.Exists(appletPath))
            {
                Logger.LogWriteLine($"Task Scheduler Applet does not exist in this path: {appletPath}", LogType.Error, true);
                return short.MinValue;
            }

            // Try make process instance for the applet
            using (Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = appletPath,
                    Arguments = argument,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            })
            {
                int lastErrCode = short.MaxValue;
                try
                {
                    // Start the applet and wait until it exit.
                    process.Start();
                    while (!process.StandardOutput.EndOfStream)
                    {
                        string consoleStdOut = process.StandardOutput.ReadLine();
                        Logger.LogWriteLine("[TaskScheduler] " + consoleStdOut, LogType.Debug, true);

                        // Parse if it has RETURNVAL_
                        if (consoleStdOut.StartsWith(RETURNVALMARK))
                        {
                            ReadOnlySpan<char> span = consoleStdOut.AsSpan(RETURNVALMARK.Length);
                            if (int.TryParse(span, null, out int resultReturnCode))
                            {
                                lastErrCode = resultReturnCode;
                            }
                        }
                    }

                    if (lastErrCode != process.ExitCode)
                    {
                        lastErrCode = process.ExitCode;
                    }    
                    process.WaitForExit();
                }
                catch (Exception ex)
                {
                    // If error happened, then return.
                    Logger.LogWriteLine($"An error has occurred while invoking Task Scheduler applet!\r\n{ex}", LogType.Error, true);
                    return short.MaxValue;
                }

                // Get return code
                return lastErrCode;
            }
        }
    }
}
