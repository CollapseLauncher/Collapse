using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Structs;
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
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Playback;
using WinRT;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Properties

    // ReSharper disable once InconsistentNaming
    private static ref readonly Guid IMediaPlayer_IID
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

    private static readonly ConcurrentDictionary<int, TimeSpan> SharedLastMediaPosition = new();

    private CanvasDevice?       _canvasDevice;
    private CanvasRenderTarget? _canvasRenderTarget;
    private nint                _canvasRenderTargetNativePtr;
    private nint                _canvasRenderTargetAsSurfacePtr;

    private volatile int _isBlockVideoFrameDraw = 1;
    private          int _isVideoFrameDrawInProgress;

    private CanvasImageSource?  _canvasImageSource;
    private nint                _canvasImageSourceNativePtr = nint.Zero;

    private int  _canvasWidth;
    private int  _canvasHeight;
    private Rect _canvasRenderSize;

    private MediaPlayer _videoPlayer    = null!;
    private nint        _videoPlayerPtr = nint.Zero;
    private Timer?      _videoPlayerDurationTimerThread;

    #endregion

    #region Video Frame Drawing

    private void VideoPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
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
                                                                           _canvasRenderTargetAsSurfacePtr);
            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, DrawFrame);
            return;

            void DrawFrame()
            {
                try
                {
                    SwapChainPanelHelper
                       .NativeSurfaceImageSource_BeginDrawEndDrawUnsafe(_canvasImageSourceNativePtr,
                                                                        _canvasRenderTargetNativePtr,
                                                                        in _canvasRenderSize);
                }
                // Device lost error. If happened, reinitialize render target
                catch (COMException comEx) when ((uint)comEx.HResult == 0x887A0005u)
                {
                    CanvasDevice_OnDeviceLost(null, null);
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailable|UIThread] {ex}",
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
        catch (COMException comEx) when ((uint)comEx.HResult == 0x887A0005u)
        {
            DispatcherQueue.TryEnqueue(() => CanvasDevice_OnDeviceLost(null, null));
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::VideoPlayer_VideoFrameAvailable|OtherThread] {ex}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region Video Frame Initialization / Disposal

    private void InitializeVideoPlayer()
    {
        if (_videoPlayer != null!)
        {
            return;
        }

        _videoPlayer = new MediaPlayer
        {
            AutoPlay                  = true,
            IsLoopingEnabled          = true,
            IsVideoFrameServerEnabled = true,
            Volume                    = AudioVolume.GetClampedVolume(),
            IsMuted                   = !IsAudioEnabled
        };


        ComMarshal<MediaPlayer>.TryGetComInterfaceReference(_videoPlayer,
                                                            in IMediaPlayer_IID,
                                                            out _videoPlayerPtr,
                                                            out _,
                                                            requireQueryInterface: true);
        _videoPlayer.MediaOpened += InitializeVideoFrameOnMediaOpened;
    }

    private void DisposeVideoPlayer()
    {
        if (_videoPlayer == null!)
        {
            return;
        }

        _videoPlayer.Pause();

        // Save last video player duration for later
        if (_videoPlayer.CanSeek && TryGetSourceHashCode(BackgroundSource, out int sourceHashCode))
        {
            _ = SharedLastMediaPosition.TryAdd(sourceHashCode, _videoPlayer.Position);
        }

        nint videoPlayerPpv = Interlocked.Exchange(ref _videoPlayerPtr, nint.Zero);
        if (videoPlayerPpv != nint.Zero) Marshal.Release(videoPlayerPpv);

        _videoPlayer.Dispose();
        _videoPlayer.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailable;
        _videoPlayer.MediaOpened         -= InitializeVideoFrameOnMediaOpened;

        MediaPlayer oldObj = Interlocked.Exchange(ref _videoPlayer!, null);
        ComMarshal<MediaPlayer>.TryReleaseComObject(oldObj, out _);

        DisposeRenderTarget();
    }

    private void InitializeRenderTargetSize(MediaPlaybackSession playbackSession)
    {
        double currentCanvasWidth  = playbackSession.NaturalVideoWidth;
        double currentCanvasHeight = playbackSession.NaturalVideoHeight;

        _canvasWidth      = (int)(currentCanvasWidth * WindowUtility.CurrentWindowMonitorScaleFactor);
        _canvasHeight     = (int)(currentCanvasHeight * WindowUtility.CurrentWindowMonitorScaleFactor);
        _canvasRenderSize = new Rect(0, 0, _canvasWidth, _canvasHeight);
    }

    private void InitializeRenderTarget()
    {
        _isBlockVideoFrameDraw = 1; // Block frame drawing routine
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

        _canvasRenderTargetAsSurfacePtr = MarshalInterface<IDirect3DSurface>.FromManaged(_canvasRenderTarget);
        _canvasRenderTargetNativePtr    = MarshalInterface<ICanvasImage>.FromManaged(_canvasRenderTarget);

        if (!ComMarshal<CanvasImageSource>
               .TryGetComInterfaceReference(_canvasImageSource,
                                            out nint ppvICanvasImageSource,
                                            out Exception? ex))
        {
            throw ex;
        }

        // Cast (query) explicitly from pointer into ICanvasImageSource (since the interface is protected).
        Marshal.QueryInterface(ppvICanvasImageSource, new Guid("3C35E87A-E881-4F44-B0D1-551413AEC66D"), out _canvasImageSourceNativePtr);
        Marshal.Release(ppvICanvasImageSource);

        SetRenderImageSource(_canvasImageSource);
        _isBlockVideoFrameDraw = 0; // Unblock frame drawing routine
    }

    private void DisposeRenderTarget()
    {
        SetRenderImageSource(null);

        if (_canvasDevice != null)
        {
            _canvasDevice.DeviceLost -= CanvasDevice_OnDeviceLost;
        }

        _videoPlayerDurationTimerThread?.Dispose();
        _canvasRenderTarget?.Dispose();
        _canvasDevice?.Dispose();

        ComMarshal<CanvasBitmap>.TryReleaseComObject(_canvasRenderTarget, out _);
        ComMarshal<CanvasDevice>.TryReleaseComObject(_canvasDevice, out _);

        if (_canvasRenderTargetAsSurfacePtr != nint.Zero) Marshal.Release(_canvasRenderTargetAsSurfacePtr);
        if (_canvasRenderTargetNativePtr != nint.Zero) Marshal.Release(_canvasRenderTargetNativePtr);
        if (_canvasImageSourceNativePtr != nint.Zero) Marshal.Release(_canvasImageSourceNativePtr);

        Interlocked.Exchange(ref _canvasImageSourceNativePtr, nint.Zero);
        Interlocked.Exchange(ref _canvasImageSource,          null);
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
        LayeredBackgroundImage thisInstance = (LayeredBackgroundImage)state!;
        if (thisInstance._videoPlayer == null!)
        {
            return;
        }

        if (thisInstance.DispatcherQueue.HasThreadAccess)
        {
            Impl();
            return;
        }

        thisInstance.DispatcherQueue.TryEnqueue(Impl);
        return;

        void Impl()
        {
            thisInstance.SetValue(MediaDurationPositionProperty, thisInstance._videoPlayer.Position);
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
        catch
        {
            // ignored
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

        _isBlockVideoFrameDraw = 1;
        return _canvasRenderTarget;
    }

    public void UnlockCanvasRenderTarget()
    {
        _isBlockVideoFrameDraw = 0;
    }

    private static void IsAudioEnabled_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
        if (instance._videoPlayer is not { } videoPlayer)
        {
            return;
        }

        videoPlayer.IsMuted = !(bool)e.NewValue;
    }

    private static void AudioVolume_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
        if (instance._videoPlayer is not { } videoPlayer)
        {
            return;
        }

        double volume = e.NewValue.TryGetDouble();
        videoPlayer.Volume = volume.GetClampedVolume();
    }

    private static void MediaDurationPosition_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)d;

        if (instance._videoPlayer != null! &&
            e.NewValue is TimeSpan value)
        {
            instance._videoPlayer.Position = value;
        }
    }

    public void Play()
    {
        try
        {
            if (_videoPlayer != null!)
            {
                InitializeRenderTarget();
                _videoPlayer.VideoFrameAvailable += VideoPlayer_VideoFrameAvailable;
                _videoPlayer.Play();
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
            Console.WriteLine(e);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    public void Pause()
    {
        try
        {
            if (_videoPlayer != null!)
            {
                _isBlockVideoFrameDraw = 1;
                _videoPlayer.Pause();
                _videoPlayer.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailable;
                DisposeVideoPlayer();

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    #endregion
}