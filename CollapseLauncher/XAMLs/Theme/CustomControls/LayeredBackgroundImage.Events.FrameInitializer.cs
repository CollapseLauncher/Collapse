using CollapseLauncher.Extension;
using Hi3Helper;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using Windows.Foundation;
using Windows.Media.Playback;

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
                Volume                    = AudioVolume.GetClampedVolume(),
                IsMuted                   = !IsAudioEnabled,
                CommandManager            = { IsEnabled = false }
            };
            Interlocked.Exchange(ref _videoPlayer, player);

            player.MediaOpened += InitializeVideoFrameOnMediaOpened;
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

            _canvasWidth      = (int)currentCanvasWidth;
            _canvasHeight     = (int)currentCanvasHeight;
            _canvasRenderSize = new Rect(0, 0, _canvasWidth, _canvasHeight);

            // In some occasion, MediaPlayer reportedly 0x0px size which causes E_INVALIDARG while rendering frame
            // if FFmpeg source is used. So, use size reported by FFmpeg instead.
            if (_canvasRenderSize != default || _videoFfmpegMediaSource == null) return;

            _canvasWidth      = _videoFfmpegMediaSource.CurrentVideoStream.PixelWidth;
            _canvasHeight     = _videoFfmpegMediaSource.CurrentVideoStream.PixelHeight;
            _canvasRenderSize = new Rect(0, 0, _canvasWidth, _canvasHeight);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTargetSize] {ex}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region Disposers

    private void DisposeVideoPlayer(bool disposeRenderImageSource = true)
    {
        try
        {
            if (_videoPlayer == null!)
            {
                return;
            }

            if (_videoPlayer.IsObjectDisposed())
            {
                return;
            }

            _videoPlayer.MediaOpened -= InitializeVideoFrameOnMediaOpened;
            _videoPlayer.Pause();

            // Save last video player duration for later
            if (_videoPlayer.CanSeek)
            {
                _ = SaveMediaPosition(BackgroundSource, _videoPlayer.Position);
            }

            DisposeVideoPlayerElement();

            Interlocked.Exchange(ref _videoFfmpegMediaSource, null)?.Dispose();
            // ReSharper disable once ConstantConditionalAccessQualifier
            Interlocked.Exchange(ref _videoPlayer, null!)?.Dispose();

            if (disposeRenderImageSource)
            {
                SetRenderImageSource(null);
            }
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

    private void DisposeVideoPlayerElement()
    {
        MediaPlayerElement? element = Interlocked.Exchange(ref _videoPlayerElement, null);
        if (element == null)
        {
            return;
        }

        try
        {
            element.SetMediaPlayer(null);
            element.Loaded   -= MediaPlayerElement_VideoFrameOnLoaded;
            element.Unloaded -= MediaPlayerElement_VideoFrameOnUnloaded;
            _backgroundGrid?.DispatcherQueue?.TryEnqueue(() =>
            {
                try
                {
                    _backgroundGrid?.Children.Remove(element);
                }
                catch
                {
                    // ignored
                }
            });
        }
        catch
        {
            // ignored
        }
    }

    private void DisposeRenderTarget(bool disposeRenderImageSource = true)
    {
        if (!disposeRenderImageSource) return;
        try
        {
            SetRenderImageSource(null);
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeRenderTarget] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    #endregion
}
