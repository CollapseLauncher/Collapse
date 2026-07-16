using Hi3Helper.Plugin.Core.Update;
using Hi3Helper.Shared.Region;
using CollapseLauncher.Helper.StreamUtility;
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
        bool isFromManifest = string.Equals(Path.GetFileName(filePath),
                                            PluginManager.ManifestPrefix,
                                            StringComparison.OrdinalIgnoreCase);

        if (!isFromPackage && !isFromManifest)
        {
            throw new NotSupportedException($"{Path.GetFileName(filePath)} is not supported. Import a .zip package or a file named manifest.json.");
        }

        using IPluginSource pluginSource = isFromPackage ?
            await ZipPluginSource.GetSourceFrom(filePath, token) :
            await ManifestPluginSource.GetSourceFrom(filePath, token);

        string pluginExecNameNoExt = Path.GetFileNameWithoutExtension(pluginSource.Manifest.MainLibraryName);
        string pluginDirName       = $"Hi3Helper.Plugin.{pluginExecNameNoExt}";
        string pluginBaseDir       = Path.Combine(LauncherConfig.AppPluginFolder, pluginDirName);
        string pluginRelName       = Path.Combine(pluginDirName, pluginSource.Manifest.MainLibraryName);
        string pluginEntryPath     = GetContainedPath(pluginBaseDir, pluginSource.Manifest.MainLibraryName);

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

        Directory.CreateDirectory(LauncherConfig.AppPluginFolder);
        string stagingDir = Path.Combine(LauncherConfig.AppPluginFolder, $".import-{Guid.NewGuid():N}");
        string cleanupDir = stagingDir;

        try
        {
            foreach (PluginManifestAssetInfo asset in pluginSource.Manifest.Assets)
            {
                string  assetFullPath = GetContainedPath(stagingDir, asset.FilePath);
                string? assetFullDir  = Path.GetDirectoryName(assetFullPath);

                if (!string.IsNullOrEmpty(assetFullDir))
                {
                    Directory.CreateDirectory(assetFullDir);
                }

                await using FileStream assetStream             = File.Create(assetFullPath);
                await using Stream     assetPluginSourceStream = await pluginSource.GetAssetStream(asset, token);

                await assetPluginSourceStream.CopyToAsync(assetStream, token);
            }

            Directory.Move(stagingDir, pluginBaseDir);
            cleanupDir = pluginBaseDir;

            PluginInfo pluginInfo = new(pluginEntryPath,
                                         pluginRelName,
                                         pluginSource.Manifest);
            cleanupDir = string.Empty;
            return pluginInfo;
        }
        catch
        {
            if (!string.IsNullOrEmpty(cleanupDir))
            {
                new DirectoryInfo(cleanupDir).TryDeleteDirectory(true);
            }

            throw;
        }
    }

    private static string GetContainedPath(string baseDirectory, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("The plugin manifest contains an empty file path.");
        }

        string baseFullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory));
        string fileFullPath = Path.GetFullPath(relativePath, baseFullPath);
        string basePrefix   = baseFullPath + Path.DirectorySeparatorChar;

        if (!fileFullPath.StartsWith(basePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The plugin manifest path '{relativePath}' points outside the plugin directory.");
        }

        return fileFullPath;
    }
}
