using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    internal interface IBackgroundImageLoader
    {
        ValueTask LoadAsync(string filePath, bool isForceRecreateCache = false, bool isRequestInit = false, CancellationToken token = default);
        ValueTask DimmAsync(CancellationToken token = default);
        ValueTask UndimmAsync(CancellationToken token = default);
        ValueTask ShowAsync(CancellationToken token = default);
        ValueTask HideAsync(CancellationToken token = default);
    }
}
