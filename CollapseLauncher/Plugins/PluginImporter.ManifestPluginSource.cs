using Hi3Helper.Plugin.Core.Update;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private class ManifestPluginSource(PluginManifest manifest, string baseDir)
        : IPluginSource<ManifestPluginSource>, IPluginSource
    {
        public  PluginManifest Manifest { get; } = manifest;
        private string         BaseDir  { get; } = baseDir;

        public static async Task<ManifestPluginSource> GetSourceFrom(string sourceFilePath, CancellationToken token = default)
        {
            await using FileStream manifestStream = File.OpenRead(sourceFilePath);
            PluginManifest? manifest =
                await manifestStream.DeserializeAsync(PluginManifestContext.Default.PluginManifest, token: token);

            if (manifest == null)
            {
                throw new InvalidDataException("manifest.json data is not valid!");
            }

            return new ManifestPluginSource(manifest, Path.GetDirectoryName(sourceFilePath) ?? "");
        }

        public Task<Stream> GetAssetStream(PluginManifestAssetInfo assetInfo, CancellationToken token = default)
        {
            return Task.Factory.StartNew(Impl, token);

            Stream Impl()
            {
                string filePath = Path.Combine(BaseDir, assetInfo.FilePath);
                return File.OpenRead(filePath);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
