using Hi3Helper.Plugin.Core.Update;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private interface IPluginSource : IDisposable
    {
        PluginManifest Manifest { get; }

        Task<Stream> GetAssetStream(PluginManifestAssetInfo assetInfo, CancellationToken token = default);
    }
}
