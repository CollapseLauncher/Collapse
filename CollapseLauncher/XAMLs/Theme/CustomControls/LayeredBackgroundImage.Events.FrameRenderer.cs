using CollapseLauncher.Extension;
using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Media.Playback;
// ReSharper disable CommentTypo
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
// ReSharper disable AccessToModifiedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Properties

    // ReSharper disable once InconsistentNaming
    private static ref readonly Guid IMediaPlayer5_IID
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ReadOnlySpan<byte> span = [
                253, 55,  229, 207, 106, 248, 70, 68, 191, 77,
                200, 231, 146, 183, 180, 179
            ];
            return ref Unsafe.As<byte, Guid>(ref MemoryMarshal.GetReference(span));
        }
    }

    #endregion

    #region Fields
    private const int MaxSharedLastMediaPositionEntries = 32;

    private static readonly ConcurrentDictionary<int, TimeSpan> SharedLastMediaPosition = new();

    private int _isVideoFrameDrawInProgress;
    private int _isVideoInitialized;

    private int  _canvasWidth;
    private int  _canvasHeight;

    private MediaPlayer?             _videoPlayer;
    private nint                     _videoPlayerPtr = nint.Zero;
    private CancellationTokenSource? _videoPlayerFadeCts;
    private FFmpegMediaSource?       _videoFfmpegMediaSource;
    private IVideoFramePresenter?    _videoPresenter;

    #endregion

    #region Video Frame Drawing

    private unsafe void VideoPlayer_VideoFrameAvailableUnsafe(MediaPlayer sender, object args)
    {
        try
        {
            if (Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 1) == 1)
            {
#if DEBUG
                Logger.LogWriteLine($@"Skipping frame at: {sender.Position:hh\:mm\:ss\.ffffff}");
#endif
                return;
            }

            _videoPresenter?.DrawUnsafe(DrawConsumer,
                                        _canvasWidth,
                                        _canvasHeight);
        }
        finally
        {
            Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
        }


        unsafe void DrawConsumer(nint surfaceAbi)
            => ((delegate* unmanaged[MemberFunction]<nint, nint, int>)(*(*(void***)_videoPlayerPtr + 10)))(_videoPlayerPtr, surfaceAbi);
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
                                           bool              reinitializeImageSource = true,
                                           double            volumeFadeDurationMs    = 1000d,
                                           double            volumeFadeResolutionMs  = 10d,
                                           CancellationToken token                   = default)
    {
        try
        {
            if (_videoPlayer != null!)
            {
                // Only initialize once.
                if (Interlocked.Exchange(ref _isVideoInitialized, 1) == 0)
                {
                    // Seek to last position if source was the same
                    if (_videoPlayer.CanSeek &&
                        TryGetSourceHashCode(BackgroundSource, out int lastSourceHashCode) &&
                        SharedLastMediaPosition.TryGetValue(lastSourceHashCode, out TimeSpan lastPosition))
                    {
                        _videoPlayer.Position = lastPosition;
                    }

                    _videoPlayer.PlaybackSession.PositionChanged += MediaDurationPosition_OnChangedBridge;
                }

                // 2026/04/19: Now the VideoFrameAvailable handle is used to notify NotifyVideoLoaded -> NotifyImageLoaded.
                _videoPlayer.VideoFrameAvailable += NotifyVideoLoaded;

                PlayVideoView(ActionVideoAfterPlay,
                              volumeFadeDurationMs,
                              volumeFadeResolutionMs,
                              token);

                void ActionVideoAfterPlay()
                {
                    _videoPlayer.VideoFrameAvailable += VideoPlayer_VideoFrameAvailableUnsafe;
                }
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

            token.Register(() =>
            {
                // Interlocked.Exchange(ref _isBlockVideoFrameDraw, 0); // Make sure to unblock if request is cancelled
            });
            // Set events
            DispatcherQueue?.TryEnqueue(() => SetValue(IsVideoPlayProperty, false));
            actionOnPause?.Invoke();

            MediaPlayer? playerAtDisposeStart = _videoPlayer;
            if (playerAtDisposeStart is null)
            {
                actionAfterPause?.Invoke();
                return;
            }

            if (disposeVideoPlayer)
            {
                // Unsubscribe early to avoid wasted skipped frames.
                DetachVideoPlayerEvents(playerAtDisposeStart);
            }

            // Interlocked.Exchange(ref _isBlockVideoFrameDraw, 1); // Blocks early
            // Guard: skip deferred dispose if the player was already swapped out by a
            // concurrent source switch (the old player was disposed synchronously).
            PauseVideoView(Impl, volumeFadeDurationMs, volumeFadeResolutionMs, token);
            return;

            void Impl()
            {
                try
                {
                    if (_videoPlayer != null! &&
                        ReferenceEquals(_videoPlayer, playerAtDisposeStart) &&
                        DispatcherQueue != null!)
                    {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                                                   {
                                                       if (disposeVideoPlayer &&
                                                           ReferenceEquals(_videoPlayer, playerAtDisposeStart))
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
}
