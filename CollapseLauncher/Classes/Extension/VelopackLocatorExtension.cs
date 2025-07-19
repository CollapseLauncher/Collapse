using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Update;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ShellLinkCOM;
using NuGet.Versioning;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Velopack;
using Velopack.Locators;

// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.Extension
{
    internal static class VelopackLocatorExtension
    {
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "<AppId>k__BackingField")]
        internal static extern ref string? GetLocatorAumidField(this WindowsVelopackLocator locator);

        internal static void StartUpdaterHook(string aumid)
        {
#if !USEVELOPACK
        // Add Squirrel Hooks
        SquirrelAwareApp.HandleEvents(
                                      // Add shortcut and uninstaller entry on first start-up
                                      // ReSharper disable UnusedParameter.Local
                                      (_, sqr) =>
                                      {
                                          Console
                                             .WriteLine("Please do not close this console window while Collapse is preparing the installation via Squirrel...");
                                      },
                                      (_, sqr) =>
                                      {
                                          Console
                                             .WriteLine("Please do not close this console window while Collapse is updating via Squirrel...");
                                      },
                                      onAppUninstall: (_, sqr) =>
                                      {
                                          Console
                                             .WriteLine("Uninstalling Collapse via Squirrel...\r\n" +
                                                        "Please do not close this console window while action is being performed!");
                                      },
                                      // ReSharper restore UnusedParameter.Local
                                      onEveryRun: (_, _, _) => { }
                                     );
#else
            // Allocate the Velopack locator manually to avoid Velopack from re-assigning
            // its custom AUMID
            var logger = ILoggerHelper.GetILogger("Velopack").ToVelopackLogger();

            var currentProcess = Process.GetCurrentProcess();

            var locator =
                new WindowsVelopackLocator(currentProcess.MainModule!.FileName,
                                           (uint)currentProcess.Id,
                                           logger);
            // HACK: Always ensure to set the AUMID field null so it won't
            //       set the AUMID to its own.
            locator.GetLocatorAumidField() = null;

            var velopackBuilder = VelopackApp.Build()
                                             .OnRestarted(TryCleanupFallbackUpdate)
                                             .OnAfterUpdateFastCallback(TryCleanupFallbackUpdate)
                                             .OnFirstRun(TryCleanupFallbackUpdate)
                                             .SetLocator(locator)
                                             .SetLogger(logger);

            velopackBuilder.Run();

            _ = Task.Run(DeleteVelopackLock);

            GenerateVelopackMetadata(aumid);

            void DeleteVelopackLock()
            {
                // Get the current application directory
                string currentAppDir = AppDomain.CurrentDomain.BaseDirectory;

                // Construct the path to the .velopack_lock file
                string velopackLockPath = Path.Combine(currentAppDir, "..", "packages", ".velopack_lock");

                // Normalize the path
                velopackLockPath = Path.GetFullPath(velopackLockPath);

                // Check if the file exists
                if (!File.Exists(velopackLockPath))
                {
                    return;
                }

                // Delete the file
                File.Delete(velopackLockPath);
                Logger.LogWriteLine(".velopack_lock file deleted successfully.");
            }
#endif
        }

#if USEVELOPACK
        public static void TryCleanupFallbackUpdate(SemanticVersion newVersion)
        {
            string currentExecutedAppFolder = LauncherConfig.AppExecutableDir.TrimEnd('\\');
            string currentExecutedPath = LauncherConfig.AppExecutablePath;

            // If the path is not actually running under "current" velopack folder, then return
#if !DEBUG
        if (!currentExecutedAppFolder.EndsWith("current", StringComparison.OrdinalIgnoreCase)) // Expecting "current"
        {
            Logger.LogWriteLine("[TryCleanupFallbackUpdate] The launcher does not run from \"current\" folder");
            return;
        }
#endif

            try
            {
                // Otherwise, start cleaning-up process
                string? currentExecutedParentFolder = Path.GetDirectoryName(currentExecutedAppFolder);
                if (currentExecutedParentFolder != null)
                {
                    DirectoryInfo directoryInfo = new DirectoryInfo(currentExecutedParentFolder);
                    foreach (DirectoryInfo childLegacyAppSemVerFolder in
                             directoryInfo.EnumerateDirectories("app-*", SearchOption.TopDirectoryOnly))
                    {
                        // Removing the "app-*" folder
                        childLegacyAppSemVerFolder.Delete(true);
                        Logger.LogWriteLine($"[TryCleanupFallbackUpdate] Removed {childLegacyAppSemVerFolder.FullName} folder!",
                                     LogType.Default, true);
                    }

                    // Try to remove squirrel temp clowd folder
                    string squirrelTempPackagesFolder = Path.Combine(currentExecutedParentFolder, "SquirrelClowdTemp");
                    DirectoryInfo squirrelTempPackagesFolderInfo = new DirectoryInfo(squirrelTempPackagesFolder);
                    if (squirrelTempPackagesFolderInfo.Exists)
                    {
                        squirrelTempPackagesFolderInfo.Delete(true);
                        Logger.LogWriteLine($"[TryCleanupFallbackUpdate] Removed package temp folder: {squirrelTempPackagesFolder}!",
                                     LogType.Default, true);
                    }

                    // Try to remove stub executable
                    string squirrelLegacyStubPath = Path.Combine(currentExecutedParentFolder, "CollapseLauncher.exe");
                    RemoveSquirrelFilePath(squirrelLegacyStubPath);

                    // Try to remove createdump executable
                    string squirrelLegacyDumpPath = Path.Combine(currentExecutedParentFolder, "createdump.exe");
                    RemoveSquirrelFilePath(squirrelLegacyDumpPath);

                    // Try to remove RestartAgent executable
                    string squirrelLegacyRestartAgentPath =
                        Path.Combine(currentExecutedParentFolder, "RestartAgent.exe");
                    RemoveSquirrelFilePath(squirrelLegacyRestartAgentPath);
                }

                // Try to remove legacy shortcuts
                string? currentWindowsPathDrive = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrEmpty(currentWindowsPathDrive))
                {
                    string squirrelLegacyStartMenuGlobal =
                        Path.Combine(currentWindowsPathDrive,
                                     @"ProgramData\Microsoft\Windows\Start Menu\Programs\Collapse\Collapse Launcher");
                    string? squirrelLegacyStartMenuGlobalParent = Path.GetDirectoryName(squirrelLegacyStartMenuGlobal);
                    if (Directory.Exists(squirrelLegacyStartMenuGlobalParent) &&
                        Directory.Exists(squirrelLegacyStartMenuGlobal))
                    {
                        Directory.Delete(squirrelLegacyStartMenuGlobalParent, true);
                    }
                }

                // Try to delete all possible shortcuts on any users (since the shortcut used will be the global one)
                // Only do this if shortcut path is not same as current path tho... It pain to re-pin the shortcut again...
                string currentUsersDirPath = Path.Combine(currentWindowsPathDrive!, "Users");
                foreach (string userDirInfoPath in Directory
                                                  .EnumerateDirectories(currentUsersDirPath, "*",
                                                                        SearchOption.TopDirectoryOnly)
                                                  .Where(ConverterTool.IsUserHasPermission))
                {
                    // Get the shortcut file
                    string thisUserStartMenuShortcut = Path.Combine(userDirInfoPath,
                                                                    @"AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Collapse.lnk");
                    if (!File.Exists(thisUserStartMenuShortcut))
                    {
                        continue;
                    }

                    // Try open the shortcut and check whether this shortcut is actually pointing to
                    // CollapseLauncher.exe file
                    using ShellLink shellLink = new ShellLink(thisUserStartMenuShortcut);
                    // Try to get the target path and its filename
                    string shortcutTargetPath = shellLink.Target;
                    if (!shortcutTargetPath.Equals(currentExecutedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Compare if the filename is equal, then delete it.
                    File.Delete(thisUserStartMenuShortcut);
                    Logger.LogWriteLine($"[TryCleanupFallbackUpdate] Deleted old shortcut located at: " +
                                 $"{thisUserStartMenuShortcut} -> {shortcutTargetPath}",
                                 LogType.Default, true);
                }

                // Try to recreate shortcuts
                TaskSchedulerHelper.RecreateIconShortcuts();
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"[TryCleanupFallbackUpdate] Failed while operating clean-up routines...\r\n{ex}");
            }

            return;

            void RemoveSquirrelFilePath(string filePath)
            {
                if (!File.Exists(filePath))
                {
                    return;
                }

                File.Delete(filePath);
                Logger.LogWriteLine($"[TryCleanupFallbackUpdate] Removed old squirrel executables: {filePath}!",
                                    LogType.Default, true);
            }
        }
#endif

        public static string FindCollapseStubPath()
        {
            var collapseMainPath = LauncherConfig.AppExecutablePath;

        #if USEVELOPACK
            const string collapseExecName = "CollapseLauncher.exe";
            var collapseStubPath = Path.Combine(Directory.GetParent(Path.GetDirectoryName(collapseMainPath)!)!.FullName,
                                                collapseExecName);
            if (File.Exists(collapseStubPath))
            {
                Logger.LogWriteLine($"Found stub at {collapseStubPath}", LogType.Default, true);
                return collapseStubPath;
            }
        #endif

            Logger.LogWriteLine($"Collapse stub is not used anymore, returning current executable path!\r\n\t{collapseMainPath}",
                                LogType.Default, true);
            return collapseMainPath;
        }

        internal static void GenerateVelopackMetadata(string aumid)
        {
            const string xmlTemplate = """
                                       <?xml version="1.0" encoding="utf-8"?>
                                       <package xmlns="http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd">
                                       <metadata>
                                       <id>CollapseLauncher</id>
                                       <title>Collapse</title>
                                       <description>Collapse</description>
                                       <authors>Collapse Project Team</authors>
                                       <version>{0}</version>
                                       <channel>{1}</channel>
                                       <mainExe>CollapseLauncher.exe</mainExe>
                                       <os>win</os>
                                       <rid>win</rid>
                                       <shortcutLocations>Desktop,StartMenuRoot</shortcutLocations>
                                       <shortcutAmuid>{2}</shortcutAmuid>
                                       <shortcutAumid>{2}</shortcutAumid>
                                       </metadata>
                                       </package>
                                       """; // Adding shortcutAumid for future use, since they typo-ed the XML tag LMAO
            var currentVersion = LauncherUpdateHelper.LauncherCurrentVersionString;
            var xmlPath        = Path.Combine(LauncherConfig.AppExecutableDir, "sq.version");
            var xmlContent = string.Format(xmlTemplate, currentVersion, LauncherConfig.IsPreview ? "preview" : "stable",
                                           aumid).ReplaceLineEndings("\n");
            
            // Check if file exist
            if (File.Exists(xmlPath))
            {
                // Check if the content is the same
                var existingContent = File.ReadAllText(xmlPath);
                if (existingContent.ReplaceLineEndings("\n").Equals(xmlContent, StringComparison.Ordinal))
                {
                    Logger.LogWriteLine("Velopack metadata is already up-to-date, skipping write operation.", LogType.Default, true);
                    return;
                }
            }
            File.WriteAllText(xmlPath, xmlContent);
            Logger.LogWriteLine($"Velopack metadata has been successfully written!\r\n{xmlContent}", LogType.Default, true);
        }
    }
}
