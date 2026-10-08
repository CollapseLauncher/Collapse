using CollapseLauncher.Extension;
using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using Hi3Helper;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Media.Playback;
using WinRT;

// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Initializers

    private void InitializeVideoPlayer()
    {
        try
        {
            if (_videoPlayer != null!)
            {
                return;
            }

            MediaPlayer player = new()
            {
                IsLoopingEnabled          = true,
                IsVideoFrameServerEnabled = true,
                Volume                    = AudioVolume.GetClampedVolume(),
                IsMuted                   = !IsAudioEnabled,
                CommandManager            = { IsEnabled = false }
            };
            Interlocked.Exchange(ref _videoPlayer, player);

            player.MediaOpened += InitializeVideoFrameOnMediaOpened;
            ((IWinRTObject)player).NativeObject.TryAs(IMediaPlayer5_IID, out _videoPlayerPtr);
            _videoPresenter = new MediaFoundationPresenter();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private void InitializeRenderTargetSize(MediaPlaybackSession playbackSession)
    {
        try
        {
            double currentCanvasWidth  = playbackSession.NaturalVideoWidth;
            double currentCanvasHeight = playbackSession.NaturalVideoHeight;

            _canvasWidth  = (int)currentCanvasWidth;
            _canvasHeight = (int)currentCanvasHeight;

            // In some occasion, MediaPlayer reportedly 0x0px size which causes E_INVALIDARG while rendering frame
            // if FFmpeg source is used. So, use size reported by FFmpeg instead.
            if ((_canvasWidth == 0 && _canvasHeight == 0) || _videoFfmpegMediaSource == null) return;

            _canvasWidth      = _videoFfmpegMediaSource.CurrentVideoStream.PixelWidth;
            _canvasHeight     = _videoFfmpegMediaSource.CurrentVideoStream.PixelHeight;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region Disposers

    private void DetachVideoPlayerEvents(MediaPlayer player)
    {
        player.MediaOpened                     -= InitializeVideoFrameOnMediaOpened;
        player.VideoFrameAvailable             -= NotifyVideoLoaded;
        player.VideoFrameAvailable             -= VideoPlayer_VideoFrameAvailableUnsafe;
        player.PlaybackSession.PositionChanged -= MediaDurationPosition_OnChangedBridge;
    }

    private void DisposeVideoPlayer(bool disposeRenderImageSource = true)
    {
        try
        {
            MediaPlayer? player = _videoPlayer;
            if (player is null || player.IsObjectDisposed())
            {
                return;
            }

            DetachVideoPlayerEvents(player);
            player.Pause();

            // Save last video player duration for later
            if (player.CanSeek)
            {
                _ = SaveMediaPosition(BackgroundSource, player.Position);
            }

            NullifyMediaPlayerNativePointers();
            if (disposeRenderImageSource)
                Interlocked.Exchange(ref _videoPresenter, null)?.Dispose();

            Interlocked.Exchange(ref _videoFfmpegMediaSource, null)?.Dispose();
            Interlocked.Exchange(ref _videoPlayer,            null)?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
        finally
        {
            Interlocked.Exchange(ref _isVideoInitialized, 0);
        }
    }

    #endregion

    #region COM Native Pointer Nullifiers

    private void NullifyMediaPlayerNativePointers()
    {
        // -- Note to myself @neon-nyan:
        //    Release IMediaPlayer5 reference first, then dispose the whole MediaPlayer.
        //    This is necessary as we just cast/QueryInterface the _videoPlayer object
        //    (as IWinRTObject, then took its direct pointer) into IMediaPlayer5. If not
        //    released, the reference on the IWinRTObject will not be zeroed, causing leak.
        if (_videoPlayerPtr != nint.Zero) Marshal.Release(Interlocked.Exchange(ref _videoPlayerPtr, nint.Zero));
    }

    #endregion
}
