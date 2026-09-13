using CollapseLauncher.Extension;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.TaskScheduler;
using Hi3Helper.Win32.ShellLinkCOM;
using System;
using System.Diagnostics;
using System.IO;

// ReSharper disable CommentTypo
// ReSharper disable StringLiteralTypo
// ReSharper disable GrammarMistakeInComment

#nullable enable
namespace CollapseLauncher.Helper;

internal static class TaskSchedulerHelper
{
    private static readonly string StubLocation            = VelopackLocatorExtension.FindCollapseStubPath();
    private const           string CollapseStartupTaskName = "CollapseLauncherStartupTask";

    private static bool _cachedIsOnTrayEnabled;
    private static bool _cachedIsEnabled;

    internal static bool IsOnTrayEnabled()
    {
        IsEnabled();
        return _cachedIsOnTrayEnabled;
    }

    internal static bool IsEnabled()
    {
        int    returnCode = TaskSchedulerUtil.IsEnabled(CollapseStartupTaskName, StubLocation);

        (_cachedIsEnabled, _cachedIsOnTrayEnabled) = returnCode switch
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
            _  => (false, false)
        };

        return _cachedIsEnabled;
    }

    internal static void ToggleTrayEnabled(bool isEnabled)
    {
        _cachedIsOnTrayEnabled = isEnabled;
        TaskSchedulerUtil.ToggleTask(_cachedIsEnabled, _cachedIsOnTrayEnabled, CollapseStartupTaskName, StubLocation);
    }

    internal static void ToggleEnabled(bool isEnabled)
    {
        _cachedIsEnabled = isEnabled;
        TaskSchedulerUtil.ToggleTask(_cachedIsEnabled, _cachedIsOnTrayEnabled, CollapseStartupTaskName, StubLocation);
    }

    internal static void RecreateIconShortcuts()
    {
        // Get icons paths
        (string iconLocationStartMenu, string iconLocationDesktop)
            = GetIconLocationPaths(
                out _,
                out string? appDescription,
                out string? executablePath,
                out string? workingDirPath);

        // Create shell link instance and save the shortcut under Desktop and User's Start menu
        CreateShortcut(iconLocationStartMenu, appDescription, executablePath, workingDirPath);
        CreateShortcut(iconLocationDesktop, appDescription, executablePath, workingDirPath);
    }

    private static void CreateShortcut(
        string iconLocation,
        string? appDescription,
        string? executablePath,
        string? workingDirPath)
    {
        // Try create icon location directory
        string iconLocationDir = Path.GetDirectoryName(iconLocation) ?? "";

        // Try create directory
        Directory.CreateDirectory(iconLocationDir);
        
        // Create ShellLink instance
        ShellLink shellLink = new();

        // If existing icon exist, try open it
        try
        {
            if (File.Exists(iconLocation))
                shellLink.Open(iconLocation);
        }
        catch (Exception ex)
        {
            string msg = $"An error occurred while opening existing icon file at: {iconLocation}";
            SentryHelper.ExceptionHandler(new Exception(msg, ex));
            Logger.LogWriteLine(msg + $"\r\n{ex}", LogType.Error, true);
        }
        
        // Set params on the shortcut instance
        shellLink.IconIndex         = 0;
        shellLink.IconPath          = executablePath ?? "";
        shellLink.DisplayMode       = LinkDisplayMode.edmNormal;
        shellLink.WorkingDirectory  = workingDirPath ?? "";
        shellLink.Target            = executablePath ?? "";
        shellLink.Description       = appDescription ?? "";

        // Save the icons
        shellLink.Save(iconLocation);
    }

    internal static (string IconStartMenu, string IconDesktop) GetIconLocationPaths(
        out string? appProductName,
        out string? appDescription,
        out string? executablePath,
        out string? workingDirPath)
    {
        // Get current executable path as its target.
        executablePath = LauncherConfig.AppExecutablePath;
        workingDirPath = Path.GetDirectoryName(executablePath);

        // Get exe's description
        FileVersionInfo currentExecVersionInfo = FileVersionInfo.GetVersionInfo(executablePath);
        appDescription = currentExecVersionInfo.FileDescription ?? "";

        // Get paths
        appProductName = currentExecVersionInfo.ProductName;
        string shortcutFilename = appProductName + ".lnk";
        string startMenuLocation = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        string desktopLocation = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string iconLocationStartMenu = Path.Combine(
            startMenuLocation,
            "Programs",
            currentExecVersionInfo.CompanyName ?? "",
            shortcutFilename);
        string iconLocationDesktop = Path.Combine(
            desktopLocation,
            shortcutFilename);

        return (iconLocationStartMenu, iconLocationDesktop);
    }
}
