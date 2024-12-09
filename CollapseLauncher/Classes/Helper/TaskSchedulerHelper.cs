using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ShellLinkCOM;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper
{
    internal static class TaskSchedulerHelper
    {
        private const string CollapseStartupTaskName = "CollapseLauncherStartupTask";

        internal static bool IsInitialized;
        internal static bool CachedIsOnTrayEnabled;
        internal static bool CachedIsEnabled;

        internal static bool IsOnTrayEnabled()
        {
            if (!IsInitialized)
                InvokeGetStatusCommand().GetAwaiter().GetResult();

            return CachedIsOnTrayEnabled;
        }

        internal static bool IsEnabled()
        {
            if (!IsInitialized)
                InvokeGetStatusCommand().GetAwaiter().GetResult();

            return CachedIsEnabled;
        }

        private static async Task InvokeGetStatusCommand()
        {
            // Build the argument and mode to set
            var argumentBuilder = new StringBuilder();
            argumentBuilder.Append("IsEnabled");

            // Append task name and stub path
            AppendTaskNameAndPathArgument(argumentBuilder);

            // Store argument builder as string
            var argumentString = argumentBuilder.ToString();

            // Invoke command and get return code
            var returnCode = await GetInvokeCommandReturnCode(argumentString);

            (CachedIsEnabled, CachedIsOnTrayEnabled) = returnCode switch
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
            if (returnCode is > -2 and < 3)
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
            CachedIsOnTrayEnabled = isEnabled;
            InvokeToggleCommand().GetAwaiter().GetResult();
        }

        internal static void ToggleEnabled(bool isEnabled)
        {
            CachedIsEnabled = isEnabled;
            InvokeToggleCommand().GetAwaiter().GetResult();
        }

        private static async Task InvokeToggleCommand()
        {
            // Build the argument and mode to set
            StringBuilder argumentBuilder = new StringBuilder();
            argumentBuilder.Append(CachedIsEnabled ? "Enable" : "Disable");

            // Append argument whether to toggle the tray or not
            if (CachedIsOnTrayEnabled)
                argumentBuilder.Append("ToTray");

            // Append task name and stub path
            AppendTaskNameAndPathArgument(argumentBuilder);

            // Store argument builder as string
            string argumentString = argumentBuilder.ToString();

            // Invoke applet
            int returnCode = await GetInvokeCommandReturnCode(argumentString);

            // Print init determination
            CheckInitDetermination(returnCode);
        }

        private static void AppendTaskNameAndPathArgument(StringBuilder argumentBuilder)
        {
            // Get current stub or main executable path
            string currentExecPath = MainEntryPoint.FindCollapseStubPath();

            // Build argument to the task name
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(CollapseStartupTaskName);
            argumentBuilder.Append('"');

            // Build argument to the executable path
            argumentBuilder.Append(" \"");
            argumentBuilder.Append(currentExecPath);
            argumentBuilder.Append('"');
        }

        internal static void RecreateIconShortcuts()
        {
            /* Invocation from Hi3Helper.TaskScheduler is no longer being user.
             * Moving to Main App's ShellLink implementation instead!
            
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
            int returnCode = await GetInvokeCommandReturnCode(argumentString);

            // Print init determination
            CheckInitDetermination(returnCode);
            */

            // Get current executable path as its target.
            string currentExecPath = LauncherConfig.AppExecutablePath;
            string workingDirPath  = Path.GetDirectoryName(currentExecPath);

            // Get exe's description
            FileVersionInfo currentExecVersionInfo = FileVersionInfo.GetVersionInfo(currentExecPath);
            string          currentExecDescription = currentExecVersionInfo.FileDescription ?? "";

            // Get paths
            string shortcutFilename = currentExecVersionInfo.ProductName + ".lnk";
            string startMenuLocation = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            string desktopLocation = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
            string iconLocationStartMenu = Path.Combine(
                startMenuLocation,
                "Programs",
                currentExecVersionInfo.CompanyName ?? "",
                shortcutFilename);
            string iconLocationDesktop = Path.Combine(
                desktopLocation,
                shortcutFilename);

            // Get icon location directory
            string iconLocationStartMenuDir = Path.GetDirectoryName(iconLocationStartMenu);
            string iconLocationDesktopDir = Path.GetDirectoryName(iconLocationDesktop);

            // Try create directory
            Directory.CreateDirectory(iconLocationStartMenuDir!);
            Directory.CreateDirectory(iconLocationDesktopDir!);

            // Create shell link instance and save the shortcut under Desktop and User's Start menu
            using ShellLink shellLink = new ShellLink();
            shellLink.IconIndex        = 0;
            shellLink.IconPath         = currentExecPath;
            shellLink.DisplayMode      = LinkDisplayMode.edmNormal;
            shellLink.WorkingDirectory = workingDirPath ?? "";
            shellLink.Target           = currentExecPath;
            shellLink.Description      = currentExecDescription;
            
            // Save the icons, do not recreate if equal
            SaveIcon(iconLocationStartMenu);
            SaveIcon(iconLocationDesktop);
            return;
            
            void SaveIcon(string iconLocation)
            {
                if (string.IsNullOrEmpty(iconLocation)) return;
                if (File.Exists(iconLocation))
                {
                    var curStartIcon = new ShellLink(iconLocation);
                    if (curStartIcon.Target.Equals(currentExecPath, StringComparison.OrdinalIgnoreCase))
                    {
                        shellLink.Save(iconLocation);
                    }
                }
                else
                {
                    shellLink.Save(iconLocation);
                }
            }
        }

        private static async Task<int> GetInvokeCommandReturnCode(string argument)
        {
            const string retValMark = "RETURNVAL_";

            // Get the applet path and check if the file exist
            string appletPath = Path.Combine(LauncherConfig.AppFolder, "Lib", "win-x64", "Hi3Helper.TaskScheduler.exe");
            if (!File.Exists(appletPath))
            {
                Logger.LogWriteLine($"Task Scheduler Applet does not exist in this path: {appletPath}", LogType.Error, true);
                return short.MinValue;
            }

            // Try to make process instance for the applet
            using Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName               = appletPath,
                Arguments              = argument,
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                CreateNoWindow         = true
            };

#if DEBUG
            Logger.LogWriteLine("[TaskSchedulerHelper] Running TaskSchedulerHelper with command:\r\n" + appletPath + " " + argument, LogType.Debug, true);
#endif

            int lastErrCode = short.MaxValue;
            try
            {
                // Start the applet and wait until it exit.
                process.Start();
                while (!process.StandardOutput.EndOfStream)
                {
                    string consoleStdOut = await process.StandardOutput.ReadLineAsync();
                    Logger.LogWriteLine("[TaskScheduler] " + consoleStdOut, LogType.Debug, true);

                    // Parse if it has RETURNVAL_
                    if (consoleStdOut == null || !consoleStdOut.StartsWith(retValMark))
                    {
                        continue;
                    }

                    ReadOnlySpan<char> span = consoleStdOut.AsSpan(retValMark.Length);
                    if (int.TryParse(span, null, out int resultReturnCode))
                    {
                        lastErrCode = resultReturnCode;
                    }
                }
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                // If error happened, then return.
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"An error has occurred while invoking Task Scheduler applet!\r\n{ex}", LogType.Error, true);
                return short.MaxValue;
            }

            // Get return code
            return lastErrCode;
        }
    }
}
