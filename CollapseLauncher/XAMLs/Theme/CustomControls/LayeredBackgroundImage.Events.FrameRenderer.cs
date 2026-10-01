using CollapseLauncher.Extension;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Win32.WinRT.SwapChainPanelHelper;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Foundation;
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

    private bool _useSafeFrameRenderer;

    #endregion

    #region Direct Function Table Call Delegates

    private static unsafe delegate* unmanaged[Stdcall]<nint, uint, ref readonly Rect, out nint, int> _functionTableBeginDraw;
    private static unsafe delegate* unmanaged[Stdcall]<nint, nint, ref readonly Rect, int>           _functionTableDrawImage;
    private static unsafe delegate* unmanaged[Stdcall]<nint, nint, int>                              _functionTableCopyFrameToVideoSurface;
    private static unsafe delegate* unmanaged[Stdcall]<nint, int>                                    _functionTableDispose;

    #endregion

    #region Fields
    private const int MaxSharedLastMediaPositionEntries = 32;

    private static readonly ConcurrentDictionary<int, TimeSpan> SharedLastMediaPosition = new();

    private CanvasRenderTarget? _canvasRenderTarget;
    private nint                _canvasRenderTargetNativePtr;
    private nint                _canvasRenderTargetAsSurfacePtr;

    private int _isBlockVideoFrameDraw = 1;
    private int _isVideoFrameDrawInProgress;
    private int _isVideoInitialized;

    private CanvasDevice?             _canvasDevice;
    private CanvasVirtualImageSource? _canvasImageSource;
    private nint                      _canvasImageSourceNativePtr = nint.Zero;

    private int  _canvasWidth;
    private int  _canvasHeight;
    private Rect _canvasRenderSize;

    private MediaPlayer?             _videoPlayer;
    private nint                     _videoPlayerPtr = nint.Zero;
    private CancellationTokenSource? _videoPlayerFadeCts;
    private FFmpegMediaSource?       _videoFfmpegMediaSource;
    private long                     _videoToSkipFrames;

    private int _isFirstInitSkipFrame = 1;

    #endregion

    #region Video Frame Drawing

    private unsafe void VideoPlayerUnsafe_OnVideoFrameAvailable(MediaPlayer sender, object args)
    {
        if (_isBlockVideoFrameDraw == 1)
            return;

        if (Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 1) > 0)
        {
#if DEBUG
            Logger.LogWriteLine($@"Skipping frame at: {sender.Position:hh\:mm\:ss\.ffffff}");
#endif
            return;
        }

        _functionTableCopyFrameToVideoSurface(_videoPlayerPtr, _canvasRenderTargetAsSurfacePtr);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, Draw);

        return;

        void Draw()
        {
        // StartDraw:
            try
            {
                nint drawingSessionPpv = SwapChainPanelHelper
                   .CanvasSessionDrawUnsafe(_canvasImageSourceNativePtr,
                                            _canvasRenderTargetNativePtr,
                                            _functionTableBeginDraw,
                                            _functionTableDrawImage,
                                            in _canvasRenderSize);
                if (drawingSessionPpv != nint.Zero)
                    SwapChainPanelHelper.DrawingDisposeUnsafe(drawingSessionPpv, _functionTableDispose);
            }
            // Ignore DCOMPOSITION_ERROR_SURFACE_BEING_RENDERED
            catch (COMException comEx) when (unchecked((uint)comEx.HResult) == 0x88980801u)
            {
                // ignored
            }
            // Trying to recreate the context instead of re-creating the entire canvas
            catch (COMException comEx) when (unchecked((uint)comEx.HResult) == 0x802B0020u)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::UnsafeOnVideoFrameAvailable] CanvasImageSource has lost its context. Re-creating the context...\r\n{comEx}",
                                    LogType.Warning,
                                    true);

                /* TODO: Re-create the context and start redrawing.
                 * Use it for CanvasImageSource later. Needs some fixes with how I can re-create the RenderTarget and the ImageSource without
                 * dealing with the lock and stuffs.
                using CanvasLock? canvasLock = _canvasDevice?.Lock();
                _canvasImageSource?.Recreate(_canvasDevice);
                Logger.LogWriteLine("[LayeredBackgroundImage::UnsafeOnVideoFrameAvailable] Trying to re-draw after re-creating the context...",
                                    LogType.Warning,
                                    true);
                goto StartDraw;
                */
                CanvasDevice_OnDeviceLost();
            }
            // Device lost error. If happened, reinitialize render target
            catch (COMException comEx) when (unchecked((uint)comEx.HResult) is 0x887A0005u or 0x8899000Cu)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::UnsafeOnVideoFrameAvailable] Direct3D device is lost! Trying to reinitialize it...\r\n{comEx}",
                                    LogType.Error,
                                    true);
                CanvasDevice_OnDeviceLost();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::UnsafeOnVideoFrameAvailable]" +
                                    $"\r\n_videoPlayerPtr: 0x{_videoPlayerPtr:x8}" +
                                    $"\r\n_canvasRenderTargetAsSurfacePtr: 0x{_canvasRenderTargetAsSurfacePtr:x8}" +
                                    $"\r\n_canvasImageSourceNativePtr: 0x{_canvasImageSourceNativePtr:x8}" +
                                    $"\r\n_canvasRenderTargetNativePtr: 0x{_canvasRenderTargetNativePtr:x8}" +
                                    $"\r\n{ex}",
                                    LogType.Error,
                                    true);
            }
            finally
            {
                Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
            }
        }
    }

    private void VideoPlayerSafe_OnVideoFrameAvailable(MediaPlayer sender, object args)
    {
        if (_isBlockVideoFrameDraw == 1)
            return;

        if (Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 1) == 1)
        {
#if DEBUG
            Logger.LogWriteLine($@"Skipping frame at: {sender.Position:hh\:mm\:ss\.ffffff}");
#endif
            return;
        }

        _videoPlayer?.CopyFrameToVideoSurface(_canvasRenderTarget);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, Draw);

        return;

        void Draw()
        {
        // StartDraw:
            try
            {
                using CanvasDrawingSession? ds = _canvasImageSource?.CreateDrawingSession(default, _canvasRenderSize);
                ds?.DrawImage(_canvasRenderTarget, _canvasRenderSize);
            }
            // Ignore DCOMPOSITION_ERROR_SURFACE_BEING_RENDERED
            catch (COMException comEx) when (unchecked((uint)comEx.HResult) == 0x88980801u)
            {
                // ignored
            }
            // Trying to recreate the context instead of re-creating the entire canvas
            catch (COMException comEx) when (unchecked((uint)comEx.HResult) == 0x802B0020u)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::SafeOnVideoFrameAvailable] CanvasImageSource has lost its context. Re-creating the context...\r\n{comEx}",
                                    LogType.Warning,
                                    true);

                /* TODO: Re-create the context and start redrawing.
                 * Use it for CanvasImageSource later. Needs some fixes with how I can re-create the RenderTarget and the ImageSource without
                 * dealing with the lock and stuffs.
                using CanvasLock? canvasLock = _canvasDevice?.Lock();
                _canvasImageSource?.Recreate(_canvasDevice);
                Logger.LogWriteLine("[LayeredBackgroundImage::SafeOnVideoFrameAvailable] Trying to re-draw after re-creating the context...",
                                    LogType.Warning,
                                    true);
                goto StartDraw;
                */
                CanvasDevice_OnDeviceLost();
            }
            // Device lost error. If happened, reinitialize render target
            catch (COMException comEx) when (unchecked((uint)comEx.HResult) is 0x887A0005u or 0x8899000Cu)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::SafeOnVideoFrameAvailable] Direct3D device is lost! Trying to reinitialize it...\r\n{comEx}",
                                    LogType.Error,
                                    true);
                CanvasDevice_OnDeviceLost();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::SafeOnVideoFrameAvailable] {ex}",
                                    LogType.Error,
                                    true);
            }
            finally
            {
                Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
            }
        }
    }

    #endregion

    #region Video Frame Setter

    private void SetRenderImageSource(ImageSource? renderSource)
    {
        try
        {
            if (_backgroundGrid == null!)
            {
                return;
            }

            if (_backgroundGrid.DispatcherQueue == null!)
            {
                return;
            }

            _backgroundGrid
               .DispatcherQueue
               .TryEnqueue(() =>
                           {
                               try
                               {
                                   Image? image = _backgroundGrid.Children
                                                                 .OfType<Image>()
                                                                 .LastOrDefault(x => x.Name == "VideoRenderFrame");
                                   if (image != null)
                                   {
                                       image.Source = renderSource;
                                   }
                               }
                               catch
                               {
                                   // ignored
                               }
                           });
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::SetRenderImageSource] {e}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region Video Player Events

    public CanvasRenderTarget? LockCanvasRenderTarget()
    {
        if (_canvasRenderTarget == null)
        {
            return null;
        }

        Interlocked.Exchange(ref _isBlockVideoFrameDraw, 1);
        return _canvasRenderTarget;
    }

    public void UnlockCanvasRenderTarget()
    {
        Interlocked.Exchange(ref _isBlockVideoFrameDraw, 0);
    }

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
                    if (!InitializeRenderTarget())
                    {
                        Interlocked.Exchange(ref _isVideoInitialized, 0);
                        return;
                    }

                    // Seek to last position if source was the same
                    if (_videoPlayer.CanSeek &&
                        TryGetSourceHashCode(BackgroundSource, out int lastSourceHashCode) &&
                        SharedLastMediaPosition.TryGetValue(lastSourceHashCode, out TimeSpan lastPosition))
                    {
                        _videoPlayer.Position = lastPosition;
                    }

                    _videoPlayer.PlaybackSession.PositionChanged += MediaDurationPosition_OnChangedBridge;

                    // INTENTIONAL: Skipping a blank frame after initialization.
                    try
                    {
                        if (UseFfmpegDecoder)
                        {
                            // Use a half second for FFmpeg as it took slightly longer.
                            double ffmpegSessionFrameRate = _videoFfmpegMediaSource?.CurrentVideoStream?.FramesPerSecond ?? 0;
                            ffmpegSessionFrameRate *= .5d;

                            // Avoid longer frame skipping on first init. Override it by just two
                            if (Interlocked.Exchange(ref _isFirstInitSkipFrame, 0) == 1 &&
                                ffmpegSessionFrameRate > 1)
                            {
                                ffmpegSessionFrameRate = 1;
                            }

                            Interlocked.Exchange(ref _videoToSkipFrames, (long)Math.Round(ffmpegSessionFrameRate));
                        }
                        else
                        {
                            Interlocked.Exchange(ref _videoToSkipFrames, 1);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }

                // 2026/04/19: Now the VideoFrameAvailable handle is used to notify NotifyVideoLoaded -> NotifyImageLoaded.
                _videoPlayer.VideoFrameAvailable += NotifyVideoLoaded;

                PlayVideoView(ActionVideoAfterPlay,
                              volumeFadeDurationMs,
                              volumeFadeResolutionMs,
                              token);

                void ActionVideoAfterPlay()
                {
                    _videoPlayer.VideoFrameAvailable += !_useSafeFrameRenderer
                        ? VideoPlayerUnsafe_OnVideoFrameAvailable
                        : VideoPlayerSafe_OnVideoFrameAvailable;
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
