using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CollapseLauncher.Plugins;

#nullable enable
internal static class PluginManager
{
    public static readonly ConcurrentDictionary<string, PluginInfo> PluginInstances = new(StringComparer.OrdinalIgnoreCase);

    internal static async Task LoadPlugins(Dictionary<string, Dictionary<string, PresetConfig>?> launcherMetadataConfig,
                                           Dictionary<string, List<string>?> launcherGameNameRegionCollection)
    {
        string pluginDir = Path.Combine(LauncherConfig.AppExecutableDir, "Lib\\Plugins");
        if (!Directory.Exists(pluginDir))
        {
            return;
        }

        foreach (string pluginFile in Directory.EnumerateFiles(pluginDir, "Hi3Helper.Plugin.*.dll", SearchOption.TopDirectoryOnly))
        {
            if (PluginInstances.ContainsKey(pluginFile))
            {
                // Plugin already loaded, skip it.
                continue;
            }

            PluginInfo? pluginInfo = null;
            try
            {
                pluginInfo = new PluginInfo(pluginFile);
                await pluginInfo.Initialize(CancellationToken.None);

                foreach (var currentConfig in pluginInfo.PresetConfigs)
                {
                    string gameName   = currentConfig.GameName;
                    string gameRegion = currentConfig.ZoneName;

                    ref Dictionary<string, PresetConfig>? dict = ref CollectionsMarshal.GetValueRefOrAddDefault(launcherMetadataConfig, gameName, out _);
                    if (Unsafe.IsNullRef(ref dict))
                    {
                        dict = new Dictionary<string, PresetConfig>(StringComparer.OrdinalIgnoreCase);
                    }

                    dict?.TryAdd(gameRegion, currentConfig);

                    ref List<string>? gameRegions = ref CollectionsMarshal.GetValueRefOrAddDefault(launcherGameNameRegionCollection, gameName, out _);
                    if (Unsafe.IsNullRef(ref gameRegions))
                    {
                        gameRegions = [];
                    }

                    gameRegions?.Add(gameRegion);
                }

                _ = PluginInstances.TryAdd(pluginFile, pluginInfo);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[LauncherMetadataHelper] Failed to load plugin from file: {pluginFile} due to error: {ex}", LogType.Error, true);
                pluginInfo?.Dispose();
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
}
