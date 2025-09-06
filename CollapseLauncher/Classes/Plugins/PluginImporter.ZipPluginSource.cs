using Hi3Helper.Plugin.Core.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private class ZipPluginSource
        : IPluginSource<ZipPluginSource>, IPluginSource
    {
        private const string CompressionExt = ".br";
        private const string PathSeparators = "\\/";

        private ZipArchive                          Archive                      { get; }
        private FileStream                          ArchiveStream                { get; }
        public  PluginManifest                      Manifest                     { get; }
        private Dictionary<string, ZipArchiveEntry> ManifestAssetToZipArchiveMap { get; }

        private ZipPluginSource(ZipArchive     archive,
                                FileStream     archiveStream,
                                string?        eliminatedPrefix,
                                PluginManifest manifest)
        {
            Archive              = archive;
            ArchiveStream        = archiveStream;
            Manifest             = manifest;

            ReadOnlySpan<char> eliminatedPrefixSpan = eliminatedPrefix;

            ManifestAssetToZipArchiveMap = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                ReadOnlySpan<char> assetPathTrimmed = entry.FullName
                                                           .AsSpan(eliminatedPrefixSpan.Length)
                                                           .TrimStart(PathSeparators);

                if (assetPathTrimmed.EndsWith(CompressionExt, StringComparison.OrdinalIgnoreCase))
                {
                    assetPathTrimmed = assetPathTrimmed[..^CompressionExt.Length];
                }

                ManifestAssetToZipArchiveMap.TryAdd(assetPathTrimmed.ToString(), entry);
            }
        }

        public static async Task<ZipPluginSource> GetSourceFrom(string sourceFilePath, CancellationToken token = default)
        {
            bool        isFail     = false;
            FileStream? fileStream = null;
            ZipArchive? archive    = null;

            try
            {
                fileStream = File.OpenRead(sourceFilePath);
                archive    = new ZipArchive(fileStream, ZipArchiveMode.Read);

                ZipArchiveEntry? manifestEntry =
                    archive.Entries.FirstOrDefault(x => x.Name.StartsWith(PluginManager.ManifestPrefix,
                                                                          StringComparison.OrdinalIgnoreCase));

                if (manifestEntry == null)
                {
                    throw new FileNotFoundException("Cannot find manifest.json file on the root of the package path!",
                                                    PluginManager.ManifestPrefix);
                }

                bool    isManifestCompressed = manifestEntry.Name.EndsWith(CompressionExt, StringComparison.OrdinalIgnoreCase);
                string? eliminatedPrefix     = Path.GetDirectoryName(manifestEntry.FullName);

                await using Stream manifestStream = manifestEntry.Open();
                await using Stream manifestDecStream = isManifestCompressed
                    ? new BrotliStream(manifestStream, CompressionMode.Decompress)
                    : manifestStream;

                PluginManifest? manifest =
                    await manifestDecStream.DeserializeAsync(PluginManifestContext.Default.PluginManifest, token: token);

                if (manifest == null)
                {
                    throw new InvalidDataException($"manifest.json data is not valid! (IsCompressed? {isManifestCompressed})");
                }

                return new ZipPluginSource(archive, fileStream, eliminatedPrefix, manifest);
            }
            catch
            {
                isFail = true;
                throw;
            }
            finally
            {
                if (isFail)
                {
                    archive?.Dispose();
                    if (fileStream != null)
                    {
                        await fileStream.DisposeAsync();
                    }
                }
            }
        }

        public Task<Stream> GetAssetStream(PluginManifestAssetInfo assetInfo, CancellationToken token = default)
        {
            return Task.Factory.StartNew(Impl, token);

            Stream Impl()
            {
                if (!ManifestAssetToZipArchiveMap.TryGetValue(assetInfo.FilePath, out ZipArchiveEntry? entry))
                {
                    throw new FileNotFoundException($"Asset with path: {assetInfo.FilePath} is not exist inside of the zip package!", assetInfo.FilePath);
                }

                bool isCompressed = entry.Name.EndsWith(CompressionExt, StringComparison.OrdinalIgnoreCase);
                return isCompressed ? new BrotliStream(entry.Open(), CompressionMode.Decompress) : entry.Open();
            }
        }

        public void Dispose()
        {
            Archive.Dispose();
            ArchiveStream.Dispose();
            ManifestAssetToZipArchiveMap.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
