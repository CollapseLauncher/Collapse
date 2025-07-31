using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Shared.Region;
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
internal static partial class PluginManager
{
    private const string PluginDirPrefix = "Hi3Helper.Plugin.*";
    private const string ManifestPrefix  = "manifest.json";

    public static readonly Dictionary<string, PluginInfo> PluginInstances = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task LoadPlugins(
        Dictionary<string, Dictionary<string, PresetConfig>?> launcherMetadataConfig,
        Dictionary<string, List<string>?>                     launcherGameNameRegionCollection,
        Dictionary<string, Stamp>                             launcherMetadataStampDictionary)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(LauncherConfig.AppPluginFolder);
        if (!directoryInfo.Exists)
        {
            directoryInfo.Create();
        }

        ApplyPendingRoutines(directoryInfo);

        foreach (DirectoryInfo pluginDirInfo in directoryInfo.EnumerateDirectories(PluginDirPrefix, SearchOption.TopDirectoryOnly))
        {
            PluginInfo? pluginInfo = null;
            FileInfo?   pluginFile = null;
            FileInfo? manifestFile = pluginDirInfo
                                    .EnumerateFiles(ManifestPrefix, SearchOption.TopDirectoryOnly)
                                    .FirstOrDefault();

            FileInfo? markDisabled = pluginDirInfo
                                    .EnumerateFiles(PluginInfo.MarkDisabledFileName, SearchOption.TopDirectoryOnly)
                                    .FirstOrDefault();

            try
            {
                if (manifestFile == null)
                {
                    continue;
                }

                await using FileStream manifestFileStream = manifestFile.OpenRead();
                PluginManifest? pluginManifest =
                    await manifestFileStream.DeserializeAsync(PluginManifestContext.Default.PluginManifest);

                if (pluginManifest == null)
                {
                    continue;
                }

                pluginFile = new FileInfo(Path.Combine(pluginDirInfo.FullName, pluginManifest.MainLibraryName));
                if (!pluginFile.Exists)
                {
                    Logger.LogWriteLine($"The {pluginManifest.MainLibraryName} doesn't exist in this plugin directory: {pluginDirInfo.FullName}. Skipping!", LogType.Warning, true);
                    continue;
                }

                AssertIfUpxPacked(pluginFile);

                string pluginDirName = pluginDirInfo.Name;
                if (PluginInstances.TryGetValue(pluginDirName, out pluginInfo))
                {
                    // Plugin already loaded, skip it.
                    continue;
                }

                string pluginRelName = Path.Combine(pluginDirInfo.Name, pluginFile.Name);
                pluginInfo = new PluginInfo(pluginFile.FullName,
                                            pluginRelName,
                                            pluginManifest,
                                            markDisabled is not
                                            {
                                                Exists: true
                                            });

                await pluginInfo.Initialize(CancellationToken.None);

                _ = PluginInstances.TryAdd(pluginDirName, pluginInfo);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[PluginManager] Failed to load plugin from file: {pluginFile?.FullName} due to error: {ex}", LogType.Error, true);
                pluginInfo?.Dispose();
            }
            finally
            {
                if (pluginInfo is { IsLoaded: true })
                {
                    foreach (PluginPresetConfigWrapper currentConfig in pluginInfo.PresetConfigs)
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
        foreach (PluginInfo pluginInfo in PluginInstances.Values)
        {
            pluginInfo.Dispose();
        }

        // Clear the plugin instances.
        PluginInstances.Clear();
    }

    internal static void ApplyPendingRoutines(DirectoryInfo pluginRootDir)
    {
        foreach (DirectoryInfo pluginDir in pluginRootDir
                    .EnumerateDirectories(PluginDirPrefix, SearchOption.TopDirectoryOnly))
        {
            FileInfo markPendingUninstall =
                new FileInfo(Path.Combine(pluginDir.FullName, PluginInfo.MarkPendingDeletionFileName));
            FileInfo markPendingApplyUpdate =
                new FileInfo(Path.Combine(pluginDir.FullName, PluginInfo.MarkPendingUpdateFileName, PluginInfo.MarkPendingUpdateApplyFileName));

            if (markPendingUninstall.Exists)
            {
                ApplyPendingUninstallRoutine(pluginDir, markPendingUninstall);
            }

            markPendingApplyUpdate.Refresh();
            if (!markPendingApplyUpdate.Exists)
            {
                continue;
            }

            ApplyPendingUpdateRoutine(pluginDir);
        }
    }

    internal static void SetPluginLocaleId(string localeId)
    {
        foreach (PluginInfo pluginInfo in PluginInstances.Values)
        {
            pluginInfo.SetPluginLocaleId(localeId);
        }
    }

    private static void AssertIfUpxPacked(FileInfo fileInfo)
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
}
