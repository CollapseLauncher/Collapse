using CollapseLauncher.Extension;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
// ReSharper disable CommentTypo
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable AccessToModifiedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Fields

    private static readonly Dictionary<int, TimeSpan> SharedLastMediaPosition = new();

    private int _isVideoInitialized;

    private int  _canvasWidth;
    private int  _canvasHeight;
    private Rect _canvasRenderSize;

    private MediaPlayer?             _videoPlayer;
    private MediaPlayerElement?      _videoPlayerElement;
    private CancellationTokenSource? _videoPlayerFadeCts;
    private FFmpegMediaSource?       _videoFfmpegMediaSource;

    #endregion

    #region Video Frame Setter

    private void SetRenderImageSource(ImageSource? renderSource)
    {
        if (renderSource != null) return;
        DisposeVideoPlayerElement();
    }

    #endregion

    #region Video Player Events

    private static void IsAudioEnabled_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
            if (instance._videoPlayer is not { } videoPlayer)
            {
                return;
            }

            videoPlayer.IsMuted = !(bool)e.NewValue;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::IsAudioEnabled_OnChange] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private static void AudioVolume_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
            if (instance._videoPlayer is not { } videoPlayer)
            {
                return;
            }

            double volume = e.NewValue.TryGetDouble();
            videoPlayer.Volume = volume.GetClampedVolume();
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::AudioVolume_OnChange] {ex}",
                                LogType.Error,
                                true);
        }
    }

    public void InitializeAndPlayVideoView(Action?           actionOnPlay            = null,
                                            double            volumeFadeDurationMs    = 1000d,
                                            double            volumeFadeResolutionMs  = 10d,
                                            CancellationToken token                   = default)
    {
        try
        {
            MediaPlayer? player = _videoPlayer;
            if (player != null!)
            {
                // Only initialize once.
                if (Interlocked.Exchange(ref _isVideoInitialized, 1) == 0)
                {
                    // Seek to last position if source was the same
                    if (player.CanSeek &&
                        TryGetSourceHashCode(BackgroundSource, out int lastSourceHashCode) &&
                        SharedLastMediaPosition.TryGetValue(lastSourceHashCode, out TimeSpan lastPosition))
                    {
                        player.Position = lastPosition;
                    }

                    player.PlaybackSession.PositionChanged += MediaDurationPosition_OnChangedBridge;
                    player.PlaybackSession.PlaybackStateChanged += NotifyVideoLoadedOnStateChanged;
                }

                PlayVideoView(null,
                              volumeFadeDurationMs,
                              volumeFadeResolutionMs,
                              token);
            }
            else if (BackgroundSource != null)
            {
                // Try loading last media
                BackgroundSource_UseNormal(this);
                _lastBackgroundSource = BackgroundSource;
            }
            actionOnPlay?.Invoke();
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeAndPlayVideoView] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    public void DisposeAndPauseVideoView(Action?           actionOnPause            = null,
                                         Action?           actionAfterPause         = null,
                                         bool              disposeVideoPlayer       = true,
                                         bool              disposeRenderImageSource = true,
                                         double            volumeFadeDurationMs     = 500d,
                                         double            volumeFadeResolutionMs   = 10d,
                                         CancellationToken token                    = default)
    {
        try
        {
            if (this.IsObjectDisposed())
            {
                return;
            }

            // Set events
            DispatcherQueue?.TryEnqueue(() => SetValue(IsVideoPlayProperty, false));
            actionOnPause?.Invoke();

            MediaPlayer? player = _videoPlayer;
            if (player == null!)
            {
                actionAfterPause?.Invoke();
                return;
            }

            if (disposeVideoPlayer && player.PlaybackSession != null)
            {
                player.PlaybackSession.PlaybackStateChanged -= NotifyVideoLoadedOnStateChanged;
                player.PlaybackSession.PositionChanged       -= MediaDurationPosition_OnChangedBridge;
            }

            PauseVideoView(Impl, volumeFadeDurationMs, volumeFadeResolutionMs, token);
            return;

            void Impl()
            {
                try
                {
                    if (_videoPlayer != null! && DispatcherQueue != null!)
                    {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                                                   {
                                                       if (disposeVideoPlayer)
                                                           DisposeVideoPlayer(disposeRenderImageSource);
                                                   });
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeAndPauseVideoView|UIThread] FATAL: {e}",
                                        LogType.Error,
                                        true);
                }
                finally
                {
                    DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, () =>
                                                {
                                                    actionAfterPause?.Invoke();
                                                });
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeAndPauseVideoView|OtherThread] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    private void PlayVideoView(Action?           actionAfterPause       = null,
                               double            volumeFadeDurationMs   = 1000d,
                               double            volumeFadeResolutionMs = 10d,
                               CancellationToken token                  = default)
    {
        _videoPlayer?.Volume = 0;
        _videoPlayer?.Play();
        SetValue(IsVideoPlayProperty, true);

        actionAfterPause?.Invoke();

        FadeInAudio(volumeFadeDurationMs, volumeFadeResolutionMs, token);
    }

    private void PauseVideoView(Action?           actionAfterPause       = null,
                                double            volumeFadeDurationMs   = 1000d,
                                double            volumeFadeResolutionMs = 10d,
                                CancellationToken token                  = default)
    {
        FadeOutAudio(ActionAfterPauseInject, volumeFadeDurationMs, volumeFadeResolutionMs, token);
        return;

        void ActionAfterPauseInject()
        {
            _videoPlayer?.Pause();
            DispatcherQueueExtensions.TryEnqueue(() => SetValue(IsVideoPlayProperty, false));
            actionAfterPause?.Invoke();
        }
    }

    #endregion

    #region Video Loaded Notification

    private void NotifyVideoLoadedOnStateChanged(MediaPlaybackSession sender, object args)
    {
        if (sender.PlaybackState != MediaPlaybackState.Playing)
        {
            return;
        }

        sender.PlaybackStateChanged -= NotifyVideoLoadedOnStateChanged;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (!this.IsObjectDisposed())
            {
                NotifyImageLoaded();
            }
        });
    }

    #endregion

    #region Video Frame Capture

    public async Task<CanvasRenderTarget?> CaptureCurrentVideoFrame()
    {
        MediaPlayerElement? element = _videoPlayerElement;
        if (element == null || _canvasWidth <= 0 || _canvasHeight <= 0)
        {
            return null;
        }

        try
        {
            RenderTargetBitmap rtb = new();
            await rtb.RenderAsync(element, _canvasWidth, _canvasHeight);

            IBuffer pixelBuffer = await rtb.GetPixelsAsync();
            byte[] pixels = pixelBuffer.ToArray();

            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget renderTarget = new(device, _canvasWidth, _canvasHeight, 96f);
            renderTarget.SetPixelBytes(pixels);
            return renderTarget;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::CaptureCurrentVideoFrame] {ex}",
                                LogType.Error,
                                true);
            return null;
        }
    }

    #endregion
}
