using Hi3Helper.Plugin.Core.Update;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private partial class ManifestPluginSource : PluginSource
    {
        private string BaseDir { get; }

        private ManifestPluginSource(PluginManifest manifest, string baseDir) : base(manifest)
        {
            BaseDir = baseDir;
        }

        public static async ValueTask<IPluginSource> GetSourceFrom(string sourceFilePath, CancellationToken token = default)
        {
            await using FileStream manifestStream = File.OpenRead(sourceFilePath);
            await using Stream manifestStreamDecompressed = GetStreamWithTypeFromName(sourceFilePath, manifestStream);

            PluginManifest? manifest = await manifestStreamDecompressed.DeserializeAsync(PluginManifestContext.Default.PluginManifest, token: token);
            if (manifest == null)
                throw new InvalidDataException("manifest.json data is not valid!");

            return new ManifestPluginSource(manifest, Path.GetDirectoryName(sourceFilePath) ?? "");
        }

        public override ValueTask<Stream> GetAssetStream(PluginManifestAssetInfo assetInfo, CancellationToken token)
        {
            string filePath = Path.Combine(BaseDir, assetInfo.FilePath);
            if (FindActualFileStreamFromPath(filePath) is not { } stream)
            {
                throw new FileNotFoundException($"File: {assetInfo.FilePath} does not exist within the directory: {BaseDir}", filePath);
            }

            return ValueTask.FromResult(stream);
        }

        private static Stream? FindActualFileStreamFromPath(string filePath)
        {
            Stream? baseStream = null;
            if (File.Exists(filePath))
            {
                baseStream = File.OpenRead(filePath);
            }

            string? extension = null;
            baseStream ??= FindByExtension(filePath, out extension);
            if (baseStream == null)
                return null;

            bool isFail = false;
            try
            {
                return GetStreamWithTypeFromName(extension ?? filePath, baseStream);
            }
            catch
            {
                isFail = true;
                throw;
            }
            finally
            {
                if (isFail) baseStream.Dispose();
            }

            static Stream? FindByExtension(
                string      filePath,
                out string? extension)
            {
                Unsafe.SkipInit(out extension);

                foreach (string thisExt in DecompressorStreamFactoryDict.Keys)
                {
                    string filePathWithExt = filePath + thisExt;
                    if (!File.Exists(filePathWithExt))
                    {
                        continue;
                    }

                    extension = thisExt;
                    return File.OpenRead(filePathWithExt);
                }

                return null;
            }
        }
    }
}
