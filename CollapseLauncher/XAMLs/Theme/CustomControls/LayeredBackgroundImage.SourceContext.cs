using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Media.Playback;
using WinRT;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private partial class SourceContext : IDisposable
    {
        private static ConcurrentDictionary<SourceContext, bool> LoadedContexts = [];

        private static readonly Guid IID_IMediaPlayer5 = new("cfe537fd-f86a-4446-bf4d-c8e792b7b4b3");

        public object? Source          { get; set; }
        public bool    IsDisposeSource { get; set; }

        private nint _playerAbi;
        public MediaPlayer? Player
        {
            get;
            set
            {
                nint oldPlayerAbi = Interlocked.Exchange(ref _playerAbi, nint.Zero);
                if (oldPlayerAbi != nint.Zero) Marshal.Release(oldPlayerAbi);

                if (value == null)
                {
                    field?.MediaOpened -= PlayerOnUpdateCanvasSize;
                }
                else
                {
                    value.MediaOpened += PlayerOnUpdateCanvasSize;
                }

                (value as IWinRTObject)?.NativeObject?.TryAs(IID_IMediaPlayer5, out _playerAbi);
                field = value;
            }
        }

        public object? MediaSource { get; set; }

        public MediaFoundationPresenter? PlayerPresenter
        {
            get;
            set
            {
                if (value == null)
                {
                    Player?.VideoFrameAvailable -= PlayerOnVideoFrameAvailable;
                }
                else
                {
                    Player?.VideoFrameAvailable += PlayerOnVideoFrameAvailable;
                }

                field = value;
            }
        }

        public CancellationTokenSource SourceLoadingCts = new();

        private CancellationTokenSource? _volumeChangedCts;
        private bool                     _disposed;

        private int _cachedPlayerWidth;
        private int _cachedPlayerHeight;

        public SourceContext()
        {
            LoadedContexts.TryAdd(this, true);
        }

        private void PlayerOnUpdateCanvasSize(MediaPlayer sender, object args)
        {
            _cachedPlayerWidth  = (int)sender.PlaybackSession.NaturalVideoWidth;
            _cachedPlayerHeight = (int)sender.PlaybackSession.NaturalVideoHeight;
        }

        private void PlayerOnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            PlayerPresenter?.DrawUnsafe(DrawConsumer, _cachedPlayerWidth, _cachedPlayerHeight);
            return;

            unsafe void DrawConsumer(nint surfaceAbi)
                => ((delegate* unmanaged[MemberFunction]<nint, nint, int>)(*(*(void***)_playerAbi + 10)))(_playerAbi, surfaceAbi);
        }

        public void SetVolume(double volume, Action? onCompleted = null)
        {
            if (Player is null)
                return;

            if (Interlocked.Exchange(ref _volumeChangedCts, new CancellationTokenSource()) is { } lastCts)
            {
                lastCts.Cancel();
                lastCts.Dispose();
                Source = null;
            }

            Player.SetVolumeFadeCore(Player.Volume, volume, onCompleted: onCompleted, coopToken: _volumeChangedCts.Token);
            LoadedContexts.TryRemove(this, out _);
            Console.WriteLine($"Total Remained SourceContext instances: {LoadedContexts.Count} instances");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true))
                return;

            _volumeChangedCts?.Cancel();
            _volumeChangedCts?.Dispose();
            _volumeChangedCts = null;

            SourceLoadingCts.Cancel();
            SourceLoadingCts.Dispose();
            SourceLoadingCts = null!;

            Player?.MediaOpened         -= PlayerOnUpdateCanvasSize;
            Player?.VideoFrameAvailable -= PlayerOnVideoFrameAvailable;
            Player?.Pause();
            Player?.Dispose();
            Player = null;
            nint oldPlayerAbi = Interlocked.Exchange(ref _playerAbi, nint.Zero);
            if (oldPlayerAbi != nint.Zero) Marshal.Release(oldPlayerAbi);

            (MediaSource as IDisposable)?.Dispose();
            MediaSource = null;

            MediaFoundationPresenter? lastPlayerPresenter = PlayerPresenter;
            lastPlayerPresenter?.DispatcherQueue.TryEnqueue(lastPlayerPresenter.Dispose);
            PlayerPresenter = null;

            if (IsDisposeSource)
            {
                (Source as IDisposable)?.Dispose();
                Source = null;
            }

            LoadedContexts.TryRemove(this, out _);
            Console.WriteLine($"Total Remained SourceContext instances: {LoadedContexts.Count} instances");
        }
    }
}
