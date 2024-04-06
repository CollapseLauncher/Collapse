using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal interface IBackgroundMediaLoader
    {
        ValueTask LoadAsync(string filePath, bool isForceRecreateCache = false, bool isRequestInit = false, CancellationToken token = default);
        ValueTask DimmAsync(CancellationToken   token = default);
        ValueTask UndimmAsync(CancellationToken token = default);
        ValueTask ShowAsync(CancellationToken   token = default);
        ValueTask HideAsync(CancellationToken   token = default);
        void Mute();
        void Unmute();
        void SetVolume(double value);
        void WindowFocused();
        void WindowUnfocused();
        void Play();
        void Pause();
    }
}
