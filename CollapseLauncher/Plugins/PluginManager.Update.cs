using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Pages;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginManager
{
    internal static async Task<(List<(string, PluginManifest)>, bool)> StartUpdateBackgroundRoutine(bool isForUpdateCheckOnly = false)
    {
        return await PluginManagerPage.Context.StartBackgroundUpdateTask();
    }

    private static void ApplyPendingUpdateRoutine(DirectoryInfo pluginDir)
    {
        DirectoryInfo tempUpdateDir = new DirectoryInfo(Path.Combine(pluginDir.FullName, PluginInfo.MarkPendingUpdateFileName));
        DirectoryInfo? targetUpdateDir = tempUpdateDir.Parent;

        FileInfo stampCompletedFileInfo = new FileInfo(Path.Combine(tempUpdateDir.FullName, PluginInfo.MarkPendingUpdateApplyFileName));

        try
        {
            if (targetUpdateDir == null)
            {
                return;
            }

            if (!tempUpdateDir.Exists)
            {
                return;
            }

            if (!stampCompletedFileInfo.Exists)
            {
                return;
            }

            stampCompletedFileInfo.TryDeleteFile();

            // Cleanup old files
            foreach (FileInfo oldFileInfo in targetUpdateDir
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(x => x.FullName.IndexOf(PluginInfo.MarkPendingUpdateFileName, StringComparison.OrdinalIgnoreCase) < 0))
            {
                oldFileInfo.IsReadOnly = false;
                oldFileInfo.TryDeleteFile();

                string? currentOldFileDir = oldFileInfo.DirectoryName;
                if (currentOldFileDir == null)
                {
                    return;
                }

                oldFileInfo.Refresh();
                oldFileInfo.Delete();
            }

            string tempUpdateDirPath = tempUpdateDir.FullName;
            string targetUpdateDirPath = targetUpdateDir.FullName;

            foreach (FileInfo tempFileInfo in tempUpdateDir
                        .EnumerateFiles("*", SearchOption.AllDirectories))
            {
                ReadOnlySpan<char> baseName = tempFileInfo.FullName.AsSpan(tempUpdateDirPath.Length).TrimStart("/\\");
                string newFilePath = Path.Combine(targetUpdateDirPath, baseName.ToString());
                FileInfo newFileInfo = new FileInfo(newFilePath);

                newFileInfo.Directory?.Create();
                tempFileInfo.TryMoveTo(newFilePath);
            }

            pluginDir.DeleteEmptyDirectory();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"Cannot apply the update to plugin: {pluginDir.Name} due to an error: {ex}", LogType.Error, true);
        }
        finally
        {
            if (tempUpdateDir.Exists)
            {
                tempUpdateDir.TryDeleteDirectory(true);
            }
        }
    }
}
