using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Plugins;

internal static partial class PluginImporter
{
    private interface IPluginSource<T>
        where T : IPluginSource
    {
        static abstract Task<T> GetSourceFrom(string sourceFilePath, CancellationToken token = default);
    }
}
