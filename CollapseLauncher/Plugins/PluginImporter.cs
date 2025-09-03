using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Shared.Region;
using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    public static async Task<PluginInfo> AutoGetImportFromPath(string filePath, CancellationToken token)
    {
        bool isFromPackage = filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        using IPluginSource pluginSource = isFromPackage ?
            await ZipPluginSource.GetSourceFrom(filePath, token) :
            await ManifestPluginSource.GetSourceFrom(filePath, token);

        string pluginExecNameNoExt = Path.GetFileNameWithoutExtension(pluginSource.Manifest.MainLibraryName);
        string pluginDirName       = $"Hi3Helper.Plugin.{pluginExecNameNoExt}";
        string pluginBaseDir       = Path.Combine(LauncherConfig.AppPluginFolder, pluginDirName);
        string pluginRelName       = Path.Combine(pluginDirName, pluginSource.Manifest.MainLibraryName);

        if (PluginManager.PluginInstances.ContainsKey(pluginBaseDir) ||
            PluginManager.PluginInstances.Values
                         .FirstOrDefault(x => x.PluginKey == pluginRelName) != null)
        {
            throw new DuplicateNameException($"Plugin: {pluginRelName} has already been imported!");
        }

        if (pluginSource.Manifest
                        .Assets
                        .FirstOrDefault(x => x.FilePath
                                              .EndsWith(PluginManager.ManifestPrefix)) == null)
        {
            pluginSource.Manifest.Assets.Add(new PluginManifestAssetInfo
            {
                FileHash = [],
                FilePath = PluginManager.ManifestPrefix
            });
        }

        string pluginEntryPath = Path.Combine(pluginBaseDir, pluginSource.Manifest.MainLibraryName);
        foreach (PluginManifestAssetInfo asset in pluginSource.Manifest.Assets)
        {
            string  assetFullPath = Path.Combine(pluginBaseDir, asset.FilePath);
            string? assetFullDir  = Path.GetDirectoryName(assetFullPath);

            if (!string.IsNullOrEmpty(assetFullDir))
            {
                Directory.CreateDirectory(assetFullDir);
            }

            await using FileStream assetStream             = File.Create(assetFullPath);
            await using Stream     assetPluginSourceStream = await pluginSource.GetAssetStream(asset, token);

            await assetPluginSourceStream.CopyToAsync(assetStream, token);
        }

        return new PluginInfo(pluginEntryPath,
                              pluginRelName,
                              pluginSource.Manifest);
    }
}
