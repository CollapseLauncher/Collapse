using Hi3Helper.Plugin.Core.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private partial class ZipPluginSource : PluginSource, IPluginSourceFactory
    {
        private const string PathSeparators = "\\/";

        private ZipArchive                          Archive                      { get; }
        private FileStream                          ArchiveStream                { get; }
        private Dictionary<string, ZipArchiveEntry> ManifestAssetToZipArchiveMap { get; }

        private ZipPluginSource(ZipArchive     archive,
                                FileStream     archiveStream,
                                string?        eliminatedPrefix,
                                PluginManifest manifest) : base(manifest)
        {
            Archive       = archive;
            ArchiveStream = archiveStream;

            ReadOnlySpan<char> eliminatedPrefixSpan = eliminatedPrefix;

            ManifestAssetToZipArchiveMap = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                ReadOnlySpan<char> assetPathTrimmed = entry.FullName
                                                           .AsSpan(eliminatedPrefixSpan.Length)
                                                           .TrimStart(PathSeparators);
                ReadOnlySpan<char> assetExtension = Path.GetExtension(assetPathTrimmed);

                if (assetExtension.ContainsAny(CompressionExtensions))
                {
                    assetPathTrimmed = assetPathTrimmed[..^assetExtension.Length];
                }

                ManifestAssetToZipArchiveMap.TryAdd(assetPathTrimmed.ToString(), entry);
            }
        }

        public static async ValueTask<IPluginSource> GetSourceFrom(string sourceFilePath, CancellationToken token = default)
        {
            bool        isFail     = false;
            FileStream? fileStream = null;
            ZipArchive? archive    = null;

            try
            {
                fileStream = File.OpenRead(sourceFilePath);
                archive    = new ZipArchive(fileStream, ZipArchiveMode.Read);

                ZipArchiveEntry? manifestEntry = archive
                                                .Entries
                                                .FirstOrDefault(x => x.Name.StartsWith(PluginManager.ManifestPrefix,
                                                                    StringComparison.OrdinalIgnoreCase));

                if (manifestEntry == null)
                    throw new FileNotFoundException("Cannot find manifest.json file on the root of the .zip package!", PluginManager.ManifestPrefix);

                bool    isManifestCompressed = Path.GetExtension(manifestEntry.Name).ContainsAny(CompressionExtensions);
                string? eliminatedPrefix     = Path.GetDirectoryName(manifestEntry.FullName);

                await using Stream manifestStream    = await manifestEntry.OpenAsync(token);
                await using Stream manifestDecStream = GetStreamWithTypeFromName(manifestEntry.Name, manifestStream);

                PluginManifest? manifest = await manifestDecStream.DeserializeAsync(PluginManifestContext.Default.PluginManifest, token: token);
                if (manifest == null)
                    throw new InvalidDataException($"manifest.json data is not valid! (IsCompressed? {isManifestCompressed})");

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
                    await (archive?.DisposeAsync() ?? ValueTask.CompletedTask);
                    await (fileStream?.DisposeAsync() ?? ValueTask.CompletedTask);
                }
            }
        }

        public override async ValueTask<Stream> GetAssetStream(PluginManifestAssetInfo assetInfo, CancellationToken token)
        {
            if (!ManifestAssetToZipArchiveMap.TryGetValue(assetInfo.FilePath, out ZipArchiveEntry? entry))
            {
                throw new FileNotFoundException($"File: {assetInfo.FilePath} does not exist inside the .zip archive!");
            }

            Stream entryStream = await entry.OpenAsync(token);
            return GetStreamWithTypeFromName(entry.Name, entryStream);
        }

        public override void Dispose()
        {
            Archive.Dispose();
            ArchiveStream.Dispose();
            ManifestAssetToZipArchiveMap.Clear();
            base.Dispose();
        }
    }
}
