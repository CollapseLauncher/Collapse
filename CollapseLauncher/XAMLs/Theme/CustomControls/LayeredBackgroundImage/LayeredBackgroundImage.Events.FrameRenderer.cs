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
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

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

    public bool UseSafeFrameRenderer;

    #endregion

    #region Direct Function Table Call Delegates

    private static unsafe delegate* unmanaged[Stdcall]<nint, uint, in Rect, out nint, int> _functionTableBeginDraw;
    private static unsafe delegate* unmanaged[Stdcall]<nint, nint, in Rect, int>           _functionTableDrawImage;
    private static unsafe delegate* unmanaged[Stdcall]<nint, int>                          _functionTableDispose;

    #endregion

    #region Fields

    private static readonly ConcurrentDictionary<int, TimeSpan> SharedLastMediaPosition = new();

    private CanvasRenderTarget? _canvasRenderTarget;
    private nint                _canvasRenderTargetNativePtr;
    private nint                _canvasRenderTargetAsSurfacePtr;


    private int _isBlockVideoFrameDraw = 1;
    private int _isVideoFrameDrawInProgress;

    private CanvasVirtualImageSource? _canvasImageSource;
    private nint                      _canvasImageSourceNativePtr = nint.Zero;

    private int  _canvasWidth;
    private int  _canvasHeight;
    private Rect _canvasRenderSize;

    private MediaPlayer              _videoPlayer    = null!;
    private nint                     _videoPlayerPtr = nint.Zero;
    private Timer?                   _videoPlayerDurationTimerThread;
    private CancellationTokenSource? _videoPlayerFadeCts;
    private FFmpegMediaSource?       _videoFfmpegMediaSource;

    #endregion

    #region Video Frame Drawing

    private unsafe void VideoPlayer_VideoFrameAvailableUnsafe(MediaPlayer sender, object args)
    {
        nint drawingSessionPpv = nint.Zero;
        try
        {
            if (_isBlockVideoFrameDraw == 1 ||
                _canvasImageSourceNativePtr == nint.Zero ||
                _canvasRenderTargetNativePtr == nint.Zero ||
                Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 1) == 1)
            {
#if DEBUG
                Logger.LogWriteLine($@"Skipping frame at: {sender.Position:hh\:mm\:ss\.ffffff}");
#endif
                return;
            }

            SwapChainPanelHelper.MediaPlayerCopyFrameUnsafe(_videoPlayerPtr, _canvasRenderTargetAsSurfacePtr,
                                                            in _canvasRenderSize);
            drawingSessionPpv = SwapChainPanelHelper
               .CanvasSessionDrawUnsafe(_canvasImageSourceNativePtr,
                                        _canvasRenderTargetNativePtr,
                                        _functionTableBeginDraw,
                                        _functionTableDrawImage,
                                        in _canvasRenderSize);
        }
        // Device lost error. If happened, reinitialize render target
        catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
        {
            DispatcherQueue.TryEnqueue(CanvasDevice_OnDeviceLost);
        }
        catch (COMException comEx) when ((uint)comEx.HResult is 0x88980801u)
        {
            // Try to unlock if any error caused by DCOMPOSITION_ERROR_SURFACE_NOT_BEING_RENDERED
            Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableUnsafe|OtherThread] {ex}",
                                LogType.Error,
                                true);
        }
        finally
        {
            if (drawingSessionPpv != nint.Zero)
            {
                DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, DrawFrame);
            }
            void DrawFrame()
            {
                try
                {
                    SwapChainPanelHelper.DrawingDisposeUnsafe(drawingSessionPpv, _functionTableDispose);
                }
                // Device lost error. If happened, reinitialize render target
                catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
                {
                    CanvasDevice_OnDeviceLost();
                }
                catch (COMException comEx) when ((uint)comEx.HResult is 0x88980801u)
                {
                    // Try to unlock if any error caused by DCOMPOSITION_ERROR_SURFACE_NOT_BEING_RENDERED
                    Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableUnsafe|UIThread] {ex}",
                                        LogType.Error,
                                        true);
                }
                finally
                {
                    Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
                }
            }
        }
    }

    private void VideoPlayer_VideoFrameAvailableSafe(MediaPlayer sender, object args)
    {
        CanvasDrawingSession? ds = null;

        try
        {
            if (_isBlockVideoFrameDraw == 1 ||
                _canvasImageSource == null! ||
                _canvasRenderTarget == null! ||
                Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 1) == 1)
            {
#if DEBUG
                Logger.LogWriteLine($@"Skipping frame at: {sender.Position:hh\:mm\:ss\.ffffff}");
#endif
                return;
            }

            _videoPlayer.CopyFrameToVideoSurface(_canvasRenderTarget);
            ds = _canvasImageSource?.CreateDrawingSession(default, _canvasRenderSize);
            ds?.DrawImage(_canvasRenderTarget);
        }
        // Device lost error. If happened, reinitialize render target
        catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
        {
            DispatcherQueue.TryEnqueue(CanvasDevice_OnDeviceLost);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableSafe|OtherThread] {ex}",
                                LogType.Error,
                                true);
        }
        finally
        {
            DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, DrawFrame);
        }

        return;

        void DrawFrame()
        {
            try
            {
                ds?.Dispose();
            }
            // Device lost error. If happened, reinitialize render target
            catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
            {
                CanvasDevice_OnDeviceLost();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableSafe|UIThread] {ex}",
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

    #region Video Duration and Frame Setter

    private static void UpdateMediaDurationTickView(object? state)
    {
        try
        {
            LayeredBackgroundImage thisInstance = (LayeredBackgroundImage)state!;
            if (thisInstance._videoPlayer == null!)
            {
                return;
            }

            if (thisInstance.DispatcherQueue?.HasThreadAccess ?? false)
            {
                Impl();
                return;
            }

            thisInstance.DispatcherQueue?.TryEnqueue(Impl);
            return;

            void Impl()
            {
                try
                {
                    if (thisInstance._videoPlayer != null!)
                    {
                        thisInstance.SetValue(MediaDurationPositionProperty, thisInstance._videoPlayer.Position);
                    }
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::UpdateMediaDurationTickView] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

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

    public void InitializeAndPlayVideoView(Action?           actionOnPlay             = null,
                                           bool              blockIfAlreadyPlayed     = false,
                                           bool              reinitializeImageSource  = true,
                                           double            volumeFadeDurationMs   = 1000d,
                                           double            volumeFadeResolutionMs = 10d,
                                           CancellationToken token                    = default)
    {
        try
        {
            if (blockIfAlreadyPlayed &&
                _videoPlayer != null! &&
                _videoPlayer.CurrentState is MediaPlayerState.Playing)
            {
                return;
            }

            if (_videoPlayer != null!)
            {
                // Seek to last position if source was the same
                if (_videoPlayer.CanSeek &&
                    TryGetSourceHashCode(BackgroundSource, out int lastSourceHashCode) &&
                    SharedLastMediaPosition.TryGetValue(lastSourceHashCode, out TimeSpan lastPosition))
                {
                    _videoPlayer.Position = lastPosition;
                }

                _videoPlayer.Volume = 0;
                _videoPlayer.Play();

                SetValue(IsVideoPlayProperty, true);
                StartVideoPlayerVolumeFade(Math.Max(0, _videoPlayer.Volume * 100d),
                                           AudioVolume,
                                           volumeFadeDurationMs,
                                           volumeFadeResolutionMs,
                                           coopToken: token);
                InitializeRenderTarget();

                _videoPlayer.VideoFrameAvailable += !UseSafeFrameRenderer
                    ? VideoPlayer_VideoFrameAvailableUnsafe
                    : VideoPlayer_VideoFrameAvailableSafe;
            }
            else if (_lastBackgroundSourceType == MediaSourceType.Video &&
                     BackgroundSource != null)
            {
                // Try loading last media
                LoadFromSourceAsyncDetached(BackgroundSourceProperty,
                                            nameof(BackgroundStretch),
                                            nameof(BackgroundHorizontalAlignment),
                                            nameof(BackgroundVerticalAlignment),
                                            _backgroundGrid,
                                            true,
                                            ref _lastBackgroundSourceType);
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
                                         bool              blockIfAlreadyPaused     = false,
                                         bool              disposeRenderImageSource = true,
                                         double            volumeFadeDurationMs     = 500d,
                                         double            volumeFadeResolutionMs   = 10d,
                                         CancellationToken token                    = default)
    {
        try
        {
            token.Register(() =>
            {
                Interlocked.Exchange(ref _isBlockVideoFrameDraw, 0); // Make sure to unblock if request is cancelled
            });

            if (blockIfAlreadyPaused &&
                _videoPlayer != null! &&
                _videoPlayer.CurrentState is MediaPlayerState.Paused)
            {
                return;
            }

            if (_videoPlayer == null!)
            {
                return;
            }

            // Set events
            DispatcherQueue.TryEnqueue(() => SetValue(IsVideoPlayProperty, false));
            actionOnPause?.Invoke();

            Interlocked.Exchange(ref _isBlockVideoFrameDraw, 1); // Blocks early
            StartVideoPlayerVolumeFade(Math.Min(AudioVolume, _videoPlayer.Volume * 100d),
                                       0,
                                       volumeFadeDurationMs,
                                       volumeFadeResolutionMs,
                                       true,
                                       onAfterAction: Impl,
                                       coopToken: token);
            return;

            void Impl()
            {
                try
                {
                    if (_videoPlayer != null!)
                    {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                                                   {
                                                       DisposeVideoPlayer(disposeRenderImageSource);
                                                       actionAfterPause?.Invoke();
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

    private void StartVideoPlayerVolumeFade(double            fromValue,
                                            double            toValue,
                                            double            durationMs,
                                            double            resolutionMs,
                                            bool              fadeOut       = false,
                                            Action?           onAfterAction = null,
                                            CancellationToken coopToken     = default)
    {
        new Thread(() => 
                       StartVideoPlayerVolumeFadeCore(fromValue,
                                                      toValue,
                                                      durationMs,
                                                      resolutionMs,
                                                      fadeOut,
                                                      onAfterAction,
                                                      coopToken))
        {
            IsBackground = true
        }.Start();
    }

    private void StartVideoPlayerVolumeFadeCore(double            fromValue,
                                                double            toValue,
                                                double            durationMs,
                                                double            resolutionMs,
                                                bool              fadeOut       = false,
                                                Action?           onAfterAction = null,
                                                CancellationToken coopToken     = default)
    {
        try
        {
            CancellationTokenSource? prevCts = Interlocked.Exchange(ref _videoPlayerFadeCts, CancellationTokenSource.CreateLinkedTokenSource(coopToken));
            prevCts?.Cancel();
            prevCts?.Dispose();

            fromValue /= 100d;
            toValue   /= 100d;

            double timeDelta = resolutionMs / durationMs;
            double step      = Math.Max(fromValue, toValue) * timeDelta;
            step = fadeOut ? -step : step;

            Unsafe.SkipInit(out Timer timer); // HACK: Ignore Error CS0165
            timer = new Timer(Impl, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(resolutionMs));

            return;

            void Impl(object? state)
            {
                try
                {
                    if (timer == null ||
                        _videoPlayerFadeCts.Token.IsCancellationRequested)
                    {
                        return;
                    }

                    durationMs -= resolutionMs;
                    fromValue  += step;
                    if (durationMs < 0)
                    {
                        if (_videoPlayer != null!)
                        {
                            TrySetVolume(toValue);
                        }
                        timer.Dispose();
                        onAfterAction?.Invoke();
                        return;
                    }

                    if (_videoPlayer != null!)
                    {
                        TrySetVolume(fromValue);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::StartVideoPlayerVolumeFadeCore::Impl] {e}",
                                        LogType.Error,
                                        true);
                }
            }

            void TrySetVolume(double value)
            {
                try
                {
                    _videoPlayer.Volume = value;
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::StartVideoPlayerVolumeFadeCore::TrySetVolume] {e}",
                                        LogType.Error,
                                        true);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::StartVideoPlayerVolumeFadeCore] {e}",
                                LogType.Error,
                                true);
        }
    }
    #endregion
}