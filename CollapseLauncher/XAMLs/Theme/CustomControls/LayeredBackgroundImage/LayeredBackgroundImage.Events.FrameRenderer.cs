using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Win32.ManagedTools;
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
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Playback;
using WinRT;
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

    private CanvasDevice?       _canvasDevice;
    private CanvasRenderTarget? _canvasRenderTarget;
    private nint                _canvasRenderTargetNativePtr;
    private nint                _canvasRenderTargetAsSurfacePtr;


    private int _isBlockVideoFrameDraw = 1;
    private int _isVideoFrameDrawInProgress;

    private CanvasImageSource?  _canvasImageSource;
    private nint                _canvasImageSourceNativePtr = nint.Zero;

    private int  _canvasWidth;
    private int  _canvasHeight;
    private Rect _canvasRenderSize;

    private MediaPlayer              _videoPlayer    = null!;
    private nint                     _videoPlayerPtr = nint.Zero;
    private Timer?                   _videoPlayerDurationTimerThread;
    private CancellationTokenSource? _videoPlayerFadeCts;

    #endregion

    #region Video Frame Drawing

    private unsafe void VideoPlayer_VideoFrameAvailableUnsafe(MediaPlayer sender, object args)
    {
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

            SwapChainPanelHelper.MediaPlayer_CopyFrameToVideoSurfaceUnsafe(_videoPlayerPtr,
                                                                           _canvasRenderTargetAsSurfacePtr,
                                                                           in _canvasRenderSize);
            DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, DrawFrame);
            return;

            void DrawFrame()
            {
                try
                {
                    SwapChainPanelHelper
                       .NativeSurfaceImageSource_BeginDrawEndDrawUnsafe(_canvasImageSourceNativePtr,
                                                                        _canvasRenderTargetNativePtr,
                                                                        _functionTableBeginDraw,
                                                                        _functionTableDrawImage,
                                                                        _functionTableDispose,
                                                                        in _canvasRenderSize);
                }
                // Device lost error. If happened, reinitialize render target
                catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
                {
                    CanvasDevice_OnDeviceLost(null, null);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableUnsafe|UIThread] {ex}",
                                        LogType.Error,
                                        true);
                }
                finally
                {
                    Volatile.Write(ref _isVideoFrameDrawInProgress, 0);
                }
            }
        }
        // Device lost error. If happened, reinitialize render target
        catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
        {
            DispatcherQueue.TryEnqueue(() => CanvasDevice_OnDeviceLost(null, null));
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableUnsafe|OtherThread] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private void VideoPlayer_VideoFrameAvailableSafe(MediaPlayer sender, object args)
    {
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
            DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.High, DrawFrame);
            return;

            void DrawFrame()
            {
                try
                {
                    using CanvasDrawingSession? ds =
                        _canvasImageSource?.CreateDrawingSession(default, _canvasRenderSize);
                    ds?.DrawImage(_canvasRenderTarget);
                }
                // Device lost error. If happened, reinitialize render target
                catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
                {
                    CanvasDevice_OnDeviceLost(null, null);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableSafe|UIThread] {ex}",
                                        LogType.Error,
                                        true);
                }
                finally
                {
                    Volatile.Write(ref _isVideoFrameDrawInProgress, 0);
                }
            }
        }
        // Device lost error. If happened, reinitialize render target
        catch (COMException comEx) when ((uint)comEx.HResult is 0x887A0005u or 0x802B0020u)
        {
            DispatcherQueue.TryEnqueue(() => CanvasDevice_OnDeviceLost(null, null));
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailableSafe|OtherThread] {ex}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region Video Frame Initialization / Disposal

    private void InitializeVideoPlayer()
    {
        try
        {
            if (_videoPlayer != null!)
            {
                return;
            }

            _videoPlayer = new MediaPlayer
            {
                IsLoopingEnabled          = true,
                IsVideoFrameServerEnabled = true,
                Volume                    = AudioVolume.GetClampedVolume(),
                IsMuted                   = !IsAudioEnabled
            };

            ComMarshal<MediaPlayer>.TryGetComInterfaceReference(_videoPlayer,
                                                                in IMediaPlayer5_IID,
                                                                out _videoPlayerPtr,
                                                                out _,
                                                                requireQueryInterface: true);
            _videoPlayer.MediaOpened += InitializeVideoFrameOnMediaOpened;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private void DisposeVideoPlayer()
    {
        try
        {
            if (_videoPlayer == null!)
            {
                return;
            }

            _videoPlayer.VideoFrameAvailable -= !UseSafeFrameRenderer
                ? VideoPlayer_VideoFrameAvailableUnsafe
                : VideoPlayer_VideoFrameAvailableSafe;

            _videoPlayer.MediaOpened -= InitializeVideoFrameOnMediaOpened;
            _videoPlayer.Pause();

            // Save last video player duration for later
            if (_videoPlayer.CanSeek && TryGetSourceHashCode(BackgroundSource, out int sourceHashCode))
            {
                _ = SharedLastMediaPosition.TryAdd(sourceHashCode, _videoPlayer.Position);
            }

            _videoPlayer.Dispose();

            nint videoPlayerPpv = Interlocked.Exchange(ref _videoPlayerPtr, nint.Zero);
            if (videoPlayerPpv != nint.Zero) Marshal.Release(videoPlayerPpv);

            DisposeRenderTarget();
            Interlocked.Exchange(ref _videoPlayer, null!);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeVideoPlayer] {ex}",
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

            _canvasWidth      = (int)(currentCanvasWidth * WindowUtility.CurrentWindowMonitorScaleFactor);
            _canvasHeight     = (int)(currentCanvasHeight * WindowUtility.CurrentWindowMonitorScaleFactor);
            _canvasRenderSize = new Rect(0, 0, _canvasWidth, _canvasHeight);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private void InitializeRenderTarget()
    {
        try
        {
            Interlocked.Exchange(ref _isBlockVideoFrameDraw, 1); // Block frame drawing routine
            DisposeRenderTarget(); // Always ensure the previous render target has been disposed

            _videoPlayerDurationTimerThread = new Timer(UpdateMediaDurationTickView, this, TimeSpan.Zero, TimeSpan.FromSeconds(.5));

            _canvasDevice = new CanvasDevice();
            _canvasRenderTarget = new CanvasRenderTarget(_canvasDevice,
                                                         _canvasWidth,
                                                         _canvasHeight,
                                                         96f);

            _canvasImageSource = new CanvasImageSource(_canvasDevice,
                                                       _canvasWidth,
                                                       _canvasHeight,
                                                       96f);

            _canvasDevice.DeviceLost += CanvasDevice_OnDeviceLost;

            Interlocked.Exchange(ref UseSafeFrameRenderer, false); // Try to use unsafe frame renderer first

            if (!ComMarshal<IDirect3DSurface>
                   .TryGetComInterfaceReference(_canvasRenderTarget,
                                                out _canvasRenderTargetAsSurfacePtr,
                                                out Exception? ex,
                                                requireQueryInterface: true) ||
                !ComMarshal<CanvasImageSource>
                   .TryGetComInterfaceReference(_canvasImageSource,
                                                out _canvasImageSourceNativePtr,
                                                out ex) ||
                !ComMarshal<CanvasRenderTarget>
                   .TryGetComInterfaceReference(_canvasRenderTarget,
                                                out _canvasRenderTargetNativePtr,
                                                out ex))
            {
                throw ex;
            }

            unsafe
            {
                try
                {
                    if (_functionTableBeginDraw == null ||
                        _functionTableDrawImage == null ||
                        _functionTableDispose == null)
                    {
                        SwapChainPanelHelper.GetDirectNativeDelegateForDrawRoutine(_canvasImageSourceNativePtr,
                                                                                   _canvasRenderTargetNativePtr,
                                                                                   out _functionTableBeginDraw,
                                                                                   out _functionTableDrawImage,
                                                                                   out _functionTableDispose,
                                                                                   in _canvasRenderSize);
                    }
                }
                // Use WinRT's COM Invocation instead of using direct call on Windows 10 which
                // sometimes causing AccessViolationException error on getting CanvasDrawingSession.DrawImage's VTable address.
                // 
                // TODO: Find a correct VTable for CanvasDrawingSession.DrawImage on Windows 10.
                catch (Exception e)
                {
                    Interlocked.Exchange(ref UseSafeFrameRenderer, true); // Fallback

                    _functionTableBeginDraw = null;
                    _functionTableDrawImage = null;
                    _functionTableDispose = null;
                    Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTarget] Failed to initialize fast-unsafe method for frame rendering. Fallback to safe renderer.\r\n{e}",
                                        LogType.Error,
                                        true);
                }
            }

            SetRenderImageSource(_canvasImageSource);
            Interlocked.Exchange(ref _isBlockVideoFrameDraw, 0); // Unblock frame drawing routine
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTarget] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    private void DisposeRenderTarget()
    {
        try
        {
            SetRenderImageSource(null);

            if (_canvasDevice != null)
            {
                _canvasDevice.DeviceLost -= CanvasDevice_OnDeviceLost;
            }

            if (_canvasRenderTargetAsSurfacePtr != nint.Zero) Marshal.Release(_canvasRenderTargetAsSurfacePtr);
            if (_canvasRenderTargetNativePtr != nint.Zero) Marshal.Release(_canvasRenderTargetNativePtr);
            if (_canvasImageSourceNativePtr != nint.Zero) Marshal.Release(_canvasImageSourceNativePtr);

            _videoPlayerDurationTimerThread?.Dispose();
            _canvasRenderTarget?.Dispose();
            _canvasDevice?.Dispose();

            // ComMarshal<CanvasBitmap>.TryReleaseComObject(_canvasRenderTarget, out _);
            // ComMarshal<CanvasDevice>.TryReleaseComObject(_canvasDevice, out _);

            Interlocked.Exchange(ref _canvasImageSourceNativePtr, nint.Zero);
            Interlocked.Exchange(ref _canvasImageSource,          null);
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeRenderTarget] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    private void CanvasDevice_OnDeviceLost(CanvasDevice? sender, object? args)
    {
        Logger.LogWriteLine("[LayeredBackgroundImage::CanvasDevice_OnDeviceLost] Render device has been lost! Re-initialize render device and textures...",
                            LogType.Warning,
                            true);
        InitializeRenderTarget();

        // Try to unlock video draw progress (if a throw happened inside frame drawing routine)
        Volatile.Write(ref _isVideoFrameDrawInProgress, 0);
    }

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
                    TimeSpan pos = TimeSpan.Zero;
                    if (thisInstance._videoPlayer != null!)
                    {
                        pos = thisInstance._videoPlayer.Position;
                    }
                    thisInstance.SetValue(MediaDurationPositionProperty, pos);
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

    private static void MediaDurationPosition_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            LayeredBackgroundImage instance = (LayeredBackgroundImage)d;

            if (instance._videoPlayer != null! &&
                e.NewValue is TimeSpan value)
            {
                instance._videoPlayer.Position = value;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::MediaDurationPosition_OnChanged] {ex}",
                                LogType.Error,
                                true);
        }
    }

    public void Play()
    {
        try
        {
            if (_videoPlayer != null!)
            {
                _videoPlayer.Volume = 0;
                _videoPlayer.Play();

                StartVideoPlayerVolumeFade(Math.Max(0, _videoPlayer.Volume * 100d), AudioVolume, 1000d, 10d);
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
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::Play] FATAL: {e}",
                                LogType.Error,
                                true);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    public void Pause(bool unloadElement = true)
    {
        try
        {
            if (unloadElement)
            {
                Interlocked.Exchange(ref _isBlockVideoFrameDraw, 1); // Blocks early
                StartVideoPlayerVolumeFade(Math.Min(AudioVolume, _videoPlayer.Volume * 100d), 0, 500d, 10d, true, onAfterAction: Impl);
            }
            else
            {

            }
            return;

            void Impl()
            {
                try
                {
                    if (_videoPlayer != null!)
                    {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, DisposeVideoPlayer);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::Pause|UIThread] FATAL: {e}",
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
            Logger.LogWriteLine($"[LayeredBackgroundImage::Pause|OtherThread] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    private void StartVideoPlayerVolumeFade(double fromValue, double toValue, double durationMs, double resolutionMs, bool fadeOut = false, Action? onAfterAction = null)
    {
        new Thread(() => StartVideoPlayerVolumeFadeCore(fromValue, toValue, durationMs, resolutionMs, fadeOut, onAfterAction))
        {
            IsBackground = true
        }.Start();
    }

    private void StartVideoPlayerVolumeFadeCore(double fromValue, double toValue, double durationMs, double resolutionMs, bool fadeOut, Action? onAfterAction)
    {
        try
        {
            CancellationTokenSource? prevCts = Interlocked.Exchange(ref _videoPlayerFadeCts, new CancellationTokenSource());
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