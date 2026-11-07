using System.Threading;
using System.Threading.Tasks;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private interface IPluginSourceFactory
    {
        static abstract ValueTask<IPluginSource> GetSourceFrom(string sourceFilePath, CancellationToken token = default);
    }
}
