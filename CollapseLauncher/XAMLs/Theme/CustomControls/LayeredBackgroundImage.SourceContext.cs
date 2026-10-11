using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using System;
using System.Threading;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private partial class SourceContext : IDisposable
    {
        public object? Source          { get; set; }
        public bool    IsDisposeSource { get; set; }

        public MediaPlayer? Player
        {
            get;
            set
            {
                if (value == null)
                {
                    field?.MediaOpened -= PlayerOnUpdateCanvasSize;
                }
                else
                {
                    value.MediaOpened += PlayerOnUpdateCanvasSize;
                }

                field = value;
            }
        }

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

        private CancellationTokenSource? _eventChangedCts  = null;
        private CancellationTokenSource? _volumeChangedCts;

        private int _cachedPlayerWidth;
        private int _cachedPlayerHeight;

        private void PlayerOnUpdateCanvasSize(MediaPlayer sender, object args)
        {
            _cachedPlayerWidth  = (int)sender.PlaybackSession.NaturalVideoWidth;
            _cachedPlayerHeight = (int)sender.PlaybackSession.NaturalVideoHeight;
        }

        private void PlayerOnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            PlayerPresenter?.Draw(sender.CopyFrameToVideoSurface, _cachedPlayerWidth, _cachedPlayerHeight);
        }

        public void Dispose()
        {
            _volumeChangedCts?.Cancel();
            _volumeChangedCts?.Dispose();

            _eventChangedCts?.Cancel();
            _eventChangedCts?.Dispose();

            MediaFoundationPresenter? lastPlayerPresenter = PlayerPresenter;
            lastPlayerPresenter?.Dispose();
            PlayerPresenter = null;
            Player?.Dispose();

            if (IsDisposeSource)
            {
                (Source as IDisposable)?.Dispose();
            }
        }

        public void SetVolume(double volume, Action? onCompleted = null)
        {
            if (Player is null)
                return;

            if (Interlocked.Exchange(ref _volumeChangedCts, new CancellationTokenSource()) is { } lastCts)
            {
                lastCts.Cancel();
                lastCts.Dispose();
            }

            Player.SetVolumeFadeCore(Player.Volume, volume, onCompleted: onCompleted, coopToken: _volumeChangedCts.Token);
        }
    }
}
