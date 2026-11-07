using Hi3Helper.Plugin.Core.Update;
using SharpHPatchZ.IO.Compression.BZip2;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo

#if !NET11_0_OR_GREATER
using ZstandardStream = ZstdNet.DecompressionStream;
#pragma warning disable IDE0130
#endif

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private abstract class PluginSource(PluginManifest manifest) : IPluginSource
    {
        protected static readonly Dictionary<string, Func<Stream, Stream>> DecompressorStreamFactoryDict =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { ".br", stream => new BrotliStream(stream, CompressionMode.Decompress) },
#if NET11_0_OR_GREATER
                { ".zstd", stream => new ZstandardStream(stream, CompressionMode.Decompress) },
#else
                { ".zstd", stream => new ZstandardStream(stream) },
#endif
                { ".bz2", stream => new BZip2InputStream(stream, false) },
                { ".pbz2", stream => new BZip2InputStream(stream, true) },
                { ".gzip", stream => new GZipStream(stream, CompressionMode.Decompress) },
                { ".gz", stream => new GZipStream(stream,   CompressionMode.Decompress) }
            };

        private static readonly Dictionary<string, Func<Stream, Stream>>.AlternateLookup<ReadOnlySpan<char>>
            DecompressorStreamFactoryDictLookup = DecompressorStreamFactoryDict.GetAlternateLookup<ReadOnlySpan<char>>();

        protected static readonly SearchValues<string> CompressionExtensions =
            SearchValues.Create([.. DecompressorStreamFactoryDict.Keys], StringComparison.OrdinalIgnoreCase);

        public PluginManifest Manifest { get; } = manifest;

        public abstract ValueTask<Stream> GetAssetStream(PluginManifestAssetInfo assetInfo, CancellationToken token);

        protected static Stream GetStreamWithTypeFromName(string fileName, Stream stream)
        {
            ReadOnlySpan<char> fileExtension = Path.GetExtension(fileName);
            if (!fileExtension.IsEmpty &&
                DecompressorStreamFactoryDictLookup.TryGetValue(fileExtension, out Func<Stream, Stream>? factory))
            {
                return factory(stream);
            }

            return stream;
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
