using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable UnusedMemberInSuper.Global

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal interface IBackgroundMediaLoader : IDisposable
    {
        bool IsBackgroundDimm { get; set; }

        Task LoadAsync(string filePath, bool isForceRecreateCache = false, bool isRequestInit = false, CancellationToken token = default);
        void Dimm();
        void Undimm();
        void Show(bool isForceShow = false);
        void Hide();
        void Mute();
        void Unmute();
        void SetVolume(double value);
        void WindowFocused();
        void WindowUnfocused();
        void Play();
        void Pause();
    }
}
