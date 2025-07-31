using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Update;
using System;
using System.IO;
using System.Linq;
// ReSharper disable BadControlBracesIndent

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginManager
{
    private static void ApplyPendingUninstallRoutine(DirectoryInfo pluginDir, FileInfo markFile)
    {
        try
        {
#if DEBUG
            const bool isThrowOnFailDelete = true;
#else
            const bool isThrowOnFailDelete = false;
#endif

            FileInfo manifestFileInfo = new FileInfo(Path.Combine(pluginDir.FullName, ManifestPrefix));
            if (!manifestFileInfo.Exists)
            {
                throw new FileNotFoundException($"Cannot uninstall as manifest file is not found in plugin directory: {pluginDir.FullName}", manifestFileInfo.FullName);
            }

            PluginManifest? manifest = manifestFileInfo.DeserializeFromFile(PluginManifestContext.Default.PluginManifest);
            if (manifest == null)
            {
                throw new InvalidDataException($"Cannot uninstall as manifest file is not valid in plugin directory: {pluginDir.FullName}");
            }

            foreach (FileInfo fileInfo in manifest
                                         .Assets
                                         .Select(x => new FileInfo(Path.Combine(pluginDir.FullName,
                                                                                    x.FilePath))))
            {
                fileInfo.TryDeleteFile(isThrowOnFailDelete);
            }

            manifestFileInfo.TryDeleteFile(isThrowOnFailDelete);
            pluginDir.DeleteEmptyDirectory(true);
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"Failed while trying to uninstall plugin at directory: {pluginDir.FullName} due to an error: {e}", LogType.Error, true);
        }
    }
}