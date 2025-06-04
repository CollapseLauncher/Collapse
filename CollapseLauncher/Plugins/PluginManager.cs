using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Plugins;

#nullable enable
internal static class PluginManager
{
    public static readonly Dictionary<string, PluginInfo> PluginInstances = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task LoadPlugins(Dictionary<string, Dictionary<string, PresetConfig>?> launcherMetadataConfig,
                                           Dictionary<string, List<string>?> launcherGameNameRegionCollection,
                                           Dictionary<string, Stamp> launcherMetadataStampDictionary)
    {
        string pluginDir = Path.Combine(LauncherConfig.AppExecutableDir, "Lib\\Plugins");
        DirectoryInfo directoryInfo = new DirectoryInfo(pluginDir);
        if (!directoryInfo.Exists)
        {
            return;
        }

        foreach (FileInfo pluginFile in directoryInfo.EnumerateFiles("Hi3Helper.Plugin.*.dll", SearchOption.TopDirectoryOnly))
        {
            if (pluginFile.Length < 16 << 10)
            {
                continue;
            }

            AssertIfUpxPacked(pluginFile);

            PluginInfo? pluginInfo = null;
            try
            {
                if (PluginInstances.TryGetValue(pluginFile.FullName, out pluginInfo))
                {
                    // Plugin already loaded, skip it.
                    continue;
                }

                pluginInfo = new PluginInfo(pluginFile.FullName);
                await pluginInfo.Initialize(CancellationToken.None);

                _ = PluginInstances.TryAdd(pluginFile.FullName, pluginInfo);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[PluginManager] Failed to load plugin from file: {pluginFile} due to error: {ex}", LogType.Error, true);
                pluginInfo?.Dispose();
            }
            finally
            {
                if (pluginInfo != null)
                {
                    foreach (var currentConfig in pluginInfo.PresetConfigs)
                    {
                        string gameName   = currentConfig.GameName;
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

                        long stampTimestamp = long.Parse(pluginInfo.CreationDate.ToString("yyyyMMddHHmmss"));
                        _ = launcherMetadataStampDictionary.TryAdd($"{gameName} - {gameRegion}", new Stamp
                        {
                            GameName            = gameName,
                            GameRegion          = gameRegion,
                            LastModifiedTimeUtc = pluginInfo.CreationDate,
                            LastUpdated         = stampTimestamp,
                            MetadataType        = MetadataType.PresetConfigPlugin,
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
