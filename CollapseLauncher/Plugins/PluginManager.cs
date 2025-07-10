using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using Hi3Helper;
using Hi3Helper.Plugin.Core;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Plugin.Core.Utility;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.ManagedTools;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Plugins;

#nullable enable
internal static class PluginManager
{
    private const string PluginDirPrefix = "Hi3Helper.Plugin.*";


    public static readonly Dictionary<string, PluginInfo> PluginInstances = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task LoadPlugins(Dictionary<string, Dictionary<string, PresetConfig>?> launcherMetadataConfig,
                                           Dictionary<string, List<string>?> launcherGameNameRegionCollection,
                                           Dictionary<string, Stamp> launcherMetadataStampDictionary)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(LauncherConfig.AppPluginFolder);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        ApplyPendingUpdateRoutine(directoryInfo);

        foreach (DirectoryInfo pluginDirInfo in directoryInfo.EnumerateDirectories(PluginDirPrefix, SearchOption.TopDirectoryOnly))
        {
            FileInfo pluginFile = new FileInfo(Path.Combine(pluginDirInfo.FullName, "main.dll"));
            if (!pluginFile.Exists)
            {
                Logger.LogWriteLine($"The main.dll doesn't exist in this plugin directory: {pluginDirInfo.FullName}. Skipping!", LogType.Warning, true);
                continue;
            }

            AssertIfUpxPacked(pluginFile);
            PluginInfo? pluginInfo = null;

            try
            {
                string pluginDirName = pluginDirInfo.Name;
                if (PluginInstances.TryGetValue(pluginDirName, out pluginInfo))
                {
                    // Plugin already loaded, skip it.
                    continue;
                }

                pluginInfo = new PluginInfo(pluginFile.FullName);
                await pluginInfo.Initialize(CancellationToken.None);

                _ = PluginInstances.TryAdd(pluginDirName, pluginInfo);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[PluginManager] Failed to load plugin from file: {pluginFile.FullName} due to error: {ex}", LogType.Error, true);
                pluginInfo?.Dispose();
            }
            finally
            {
                if (pluginInfo != null)
                {
                    foreach (var currentConfig in pluginInfo.PresetConfigs)
                    {
                        string gameName = currentConfig.GameName;
                        string gameRegion = currentConfig.ZoneName;

                        ref Dictionary<string, PresetConfig>? dict = ref CollectionsMarshal.GetValueRefOrAddDefault(launcherMetadataConfig, gameName, out _);
                        if (Unsafe.IsNullRef(ref dict) || dict == null)
                        {
                            dict = new Dictionary<string, PresetConfig>(StringComparer.OrdinalIgnoreCase);
                        }

                        dict.TryAdd(gameRegion, currentConfig);

                        ref List<string>? gameRegions = ref CollectionsMarshal.GetValueRefOrAddDefault(launcherGameNameRegionCollection, gameName, out _);
                        if (Unsafe.IsNullRef(ref gameRegions) || gameRegions == null)
                        {
                            gameRegions = [];
                        }

                        gameRegions.Add(gameRegion);

                        long stampTimestamp = long.Parse(pluginInfo.CreationDate?.ToString("yyyyMMddHHmmss") ?? "0");
                        _ = launcherMetadataStampDictionary.TryAdd($"{gameName} - {gameRegion}", new Stamp
                        {
                            GameName = gameName,
                            GameRegion = gameRegion,
                            LastModifiedTimeUtc = pluginInfo.CreationDate ?? DateTime.MinValue,
                            LastUpdated = stampTimestamp,
                            MetadataType = MetadataType.PresetConfigPlugin,
                            PresetConfigVersion = pluginInfo.StandardVersion.ToString()
                        });
                    }
                }
            }
        }
    }

    internal static void UnloadPlugins()
    {
        // Dispose all plugin instances before freeing the plugin handles.
        foreach (var pluginInfo in PluginInstances.Values)
        {
            pluginInfo.Dispose();
        }

        // Clear the plugin instances.
        PluginInstances.Clear();
    }

    private static bool IsValidReturnCode(SelfUpdateReturnCode retCode)
    {
        uint asUint = (uint)retCode;
        if (retCode.HasFlag(SelfUpdateReturnCode.Ok) &&
            asUint is >= 0b_00000001_00000000_00000000_00000000 and <= 0b_10000000_00000000_00000000_00000000)
        {
            return true;
        }

        if (retCode.HasFlag(SelfUpdateReturnCode.Error) &&
            asUint is >= 0b_00000000_00000000_00000001_00000000 and <= 0b_00000000_10000000_00000000_00000000)
        {
            return true;
        }

        return false;
    }

    private static unsafe SelfUpdateReturnInfo ReplicateFromPtr(nint ptr)
    {
        // Fallback if the result is actually the enum.
        SelfUpdateReturnCode asRetCode = (SelfUpdateReturnCode)(uint)ptr;
        if (IsValidReturnCode(asRetCode))
        {
            return new SelfUpdateReturnInfo
            {
                _retCode = (SelfUpdateReturnCode)(uint)ptr
            };
        }

        // Return the struct if it returns it.
        SelfUpdateReturnInfo* selfUpdateReturnInfo = ptr.AsPointer<SelfUpdateReturnInfo>();
        SelfUpdateReturnInfo  ret                  = *selfUpdateReturnInfo; // basically copy the fields.

        // Free the struct from native memory (but not the pointer inside of it since it's already copied above).
        Mem.Free(selfUpdateReturnInfo);
        return ret;
    }

    internal static async Task<(List<(string, SelfUpdateReturnInfo)>, bool)> StartUpdateBackgroundRoutine()
    {
        int                                  updated              = 0;
        string                               rootPluginDir        = LauncherConfig.AppPluginFolder;
        List<(string, SelfUpdateReturnInfo)> pluginUpdateNameList = [];

        await Parallel.ForEachAsync(PluginInstances, Impl);
        return (pluginUpdateNameList, updated > 0);

        async ValueTask Impl(KeyValuePair<string, PluginInfo> pluginInfo, CancellationToken cancelToken)
        {
            bool isDisposeReturnInfo = true;
            IPlugin pluginInstance = pluginInfo.Value.Instance;
            string pluginUpdateOutputPath = Path.Combine(rootPluginDir, pluginInfo.Key, "pendingUpdate");

            pluginInstance.GetPluginSelfUpdater(out IPluginSelfUpdate? selfUpdateInstance);
            if (selfUpdateInstance == null)
            {
                return;
            }

            selfUpdateInstance.TryPerformUpdateAsync(pluginUpdateOutputPath,
                                                     true,
                                                     null,
                                                     pluginInstance.RegisterCancelToken(cancelToken),
                                                     out nint asyncResult);

            nint                 checkUpdateStatusP   = await asyncResult.WaitFromHandle<nint>();
            SelfUpdateReturnInfo selfUpdateReturnInfo = ReplicateFromPtr(checkUpdateStatusP);
            SelfUpdateReturnCode checkUpdateStatus    = selfUpdateReturnInfo.ReturnCode;

            try
            {
                if (checkUpdateStatus.HasFlag(SelfUpdateReturnCode.Error))
                {
                    Logger.LogWriteLine($"Cannot check update status for plugin: {pluginInfo.Key} due to an error with return code: {checkUpdateStatus}", LogType.Error, true);
                    return;
                }

                if (checkUpdateStatus == SelfUpdateReturnCode.NoAvailableUpdate)
                {
                    return;
                }
                Logger.LogWriteLine($"Update is available for: {pluginInfo.Key}! Starting update routine...", LogType.Default, true);

                selfUpdateInstance.TryPerformUpdateAsync(pluginUpdateOutputPath,
                                                         false,
                                                         null,
                                                         pluginInstance.RegisterCancelToken(cancelToken),
                                                         out asyncResult);

                nint                 updateRoutineStatusP    = await asyncResult.WaitFromHandle<nint>();
                SelfUpdateReturnInfo updateRoutineStatusInfo = ReplicateFromPtr(updateRoutineStatusP);
                SelfUpdateReturnCode updateRoutineStatus     = updateRoutineStatusInfo.ReturnCode;

                // Increase the count if update is successful
                if (updateRoutineStatus is SelfUpdateReturnCode.UpdateSuccess or SelfUpdateReturnCode.RollingBackSuccess)
                {
                    Logger.LogWriteLine($"Update for: {pluginInfo.Key} is success! Return code: {updateRoutineStatus}", LogType.Default, true);
                    string updateCompletedStampPath = Path.Combine(pluginUpdateOutputPath, ".updateCompleted");
                    await File.WriteAllTextAsync(updateCompletedStampPath, "Update Completed!", cancelToken);

                    lock (pluginUpdateNameList)
                    {
                        pluginUpdateNameList.Add((pluginInfo.Key, selfUpdateReturnInfo));
                    }
                    Interlocked.Increment(ref updated);
                    Interlocked.Exchange(ref isDisposeReturnInfo, false);
                    return;
                }

                Logger.LogWriteLine($"Failed while trying to update plugin: {pluginInfo.Key}, Rolling Back! Return code: {updateRoutineStatus}", LogType.Error, true);
                DirectoryInfo dirInfo = new DirectoryInfo(pluginUpdateOutputPath);
                if (dirInfo.Exists)
                {
                    dirInfo.TryDeleteDirectory(true);
                }
            }
            finally
            {
                if (isDisposeReturnInfo)
                {
                    selfUpdateReturnInfo.Dispose();
                }
            }
        }
    }

    internal static void ApplyPendingUpdateRoutine(DirectoryInfo pluginRootDir)
    {
        foreach (DirectoryInfo pluginDir in pluginRootDir.EnumerateDirectories(PluginDirPrefix, SearchOption.TopDirectoryOnly))
        {
            DirectoryInfo  tempUpdateDir   = new DirectoryInfo(Path.Combine(pluginDir.FullName, "pendingUpdate"));
            DirectoryInfo? targetUpdateDir = tempUpdateDir.Parent;

            FileInfo stampCompletedFileInfo = new FileInfo(Path.Combine(tempUpdateDir.FullName, ".updateCompleted"));

            try
            {
                if (targetUpdateDir == null)
                {
                    continue;
                }

                if (!tempUpdateDir.Exists)
                {
                    continue;
                }

                if (!stampCompletedFileInfo.Exists)
                {
                    continue;
                }

                stampCompletedFileInfo.TryDeleteFile();

                // Cleanup old files
                foreach (FileInfo oldFileInfo in targetUpdateDir
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Where(x => x.FullName.IndexOf("pendingUpdate", StringComparison.OrdinalIgnoreCase) < 0))
                {
                    oldFileInfo.IsReadOnly = false;
                    oldFileInfo.TryDeleteFile();

                    string? currentOldFileDir = oldFileInfo.DirectoryName;
                    if (currentOldFileDir == null)
                    {
                        continue;
                    }

                    FindFiles.TryIsDirectoryEmpty(currentOldFileDir, out bool isDirEmpty);
                    if (!isDirEmpty)
                    {
                        continue;
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
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Cannot apply the update to plugin: {pluginDir.Name} due to an error: {ex}", LogType.Error, true);
            }
            finally
            {
                if (tempUpdateDir.Exists)
                {
                    tempUpdateDir.TryDeleteDirectory();
                }
            }
        }
    }

    internal static void AssertIfUpxPacked(FileInfo fileInfo)
    {
        ReadOnlySpan<byte> searchValuesPattern1 = "UPX0\0\0\0\0"u8;
        ReadOnlySpan<byte> searchValuesPattern2 = "UPX1\0\0\0\0"u8;

        byte[] readHeader = ArrayPool<byte>.Shared.Rent(2048);
        try
        {
            using FileStream stream = fileInfo.OpenRead();
            stream.ReadAtLeast(readHeader, readHeader.Length, false);

            int indexOfPattern1 = readHeader.AsSpan().IndexOf(searchValuesPattern1);
            int indexOfPattern2 = readHeader.AsSpan().IndexOf(searchValuesPattern2);

            if (indexOfPattern1 >= 0 &&
                indexOfPattern2 >= 0)
            {
                Logger.LogWriteLine($"[PluginManager] Plugin: {fileInfo.Name} is UPX packed! The plugin might be unstable.", LogType.Warning, true);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readHeader);
        }
    }

    internal static void SetPluginLocaleId(string localeId)
    {
        foreach (var pluginInfo in PluginInstances.Values)
        {
            pluginInfo.SetPluginLocaleId(localeId);
        }
    }
}
