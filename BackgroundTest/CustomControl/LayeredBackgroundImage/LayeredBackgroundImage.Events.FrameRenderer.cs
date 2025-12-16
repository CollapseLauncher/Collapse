using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Interfaces.DXGI;
using Hi3Helper.Win32.WinRT.SwapChainPanelHelper;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Media.Playback;
using WinRT;
using WinRT.Interop;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Properties

    private static ref readonly Guid MediaPlayer_IID
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

    private volatile int _isBlockVideoFrameDraw = 1;
    private int _isVideoFrameDrawInProgress;
    private TimeSpan _lastVideoPlayerPosition;
    
    #endregion

    #region Video Frame Drawing

    private unsafe void VideoPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
    {
        if (_canvasSurfaceImageSourceNative == null)
        {
            return;
        }

        if (_isBlockVideoFrameDraw == 1 ||
            Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                SwapChainPanelHelper.NativeSurfaceImageSource_BeginDrawUnsafe(_canvasSurfaceImageSourceNativePtr, in _canvasRenderArea, out nint surfacePpv);
                SwapChainPanelHelper.MediaPlayer_CopyFrameToVideoSurfaceUnsafe(_videoPlayerPtr, surfacePpv);
                SwapChainPanelHelper.NativeSurfaceImageSource_EndDrawUnsafe(_canvasSurfaceImageSourceNativePtr);
                Marshal.Release(surfacePpv);
            });
            // sender.CopyFrameToVideoSurface(_canvasRenderTarget);
            // DrawVideoFrame(_canvasImageSource);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Volatile.Write(ref _isVideoFrameDrawInProgress, 0);
        }
    }

    #endregion

    #region Video Frame Initialization / Disposal

    private void InitializeVideoPlayer()
    {
        if (_videoPlayer == null)
        {
            _videoPlayer = new MediaPlayer
            {
                AutoPlay = false,
                IsLoopingEnabled = true,
                IsVideoFrameServerEnabled = true,
                Volume = AudioVolume.GetClampedVolume(),
                IsMuted = !IsAudioEnabled
            };
            _videoPlayerPtr = ((IWinRTObject)_videoPlayer).NativeObject.As<IUnknownVftbl>(MediaPlayer_IID).ThisPtr;
            _videoPlayer.MediaOpened += InitializeVideoFrameOnMediaOpened;
        }
    }

    private void DisposeVideoPlayer()
    {
        if (_videoPlayer != null!)
        {
            // Save last video player duration for later
            if (_videoPlayer.CanSeek)
            {
                _lastVideoPlayerPosition = _videoPlayer.Position;
            }

            _videoPlayer.Dispose();
            _videoPlayer.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailable;
            _videoPlayer.MediaOpened -= InitializeVideoFrameOnMediaOpened;
            Interlocked.Exchange(ref _videoPlayer!, null);
            _videoPlayerPtr = nint.Zero;
        }
    }

    private void InitializeRenderTargetSize(MediaPlaybackSession playbackSession)
    {
        double currentCanvasWidth = playbackSession.NaturalVideoWidth;
        double currentCanvasHeight = playbackSession.NaturalVideoHeight;

        // Scale by 1.5x size is required for XAML (well, actually 2x, but to reduce BitmapSize as well. Might look a bit blurry.)
        _canvasWidth = (int)(currentCanvasWidth * XamlRoot.RasterizationScale * 1.5d);
        _canvasHeight = (int)(currentCanvasHeight * XamlRoot.RasterizationScale * 1.5d);
        _canvasRenderArea = new(0, 0, _canvasWidth, _canvasHeight);
    }

    private void InitializeRenderTarget()
    {
        DisposeRenderTarget(); // Always ensure the previous render target has been disposed

        _canvasDevice = CanvasDevice.GetSharedDevice();
        _canvasSurfaceImageSource = new(_canvasWidth, _canvasHeight, true);
        SwapChainPanelHelper.GetNativeSurfaceImageSource(_canvasSurfaceImageSource,
                                                         out _canvasSurfaceImageSourceNative,
                                                         out _canvasD3DDeviceContext);

        ComMarshal<ISurfaceImageSourceNativeWithD2D>.TryGetComInterfaceReference(_canvasSurfaceImageSourceNative,
                                                                                 out _canvasSurfaceImageSourceNativePtr,
                                                                                 out _,
                                                                                 CreateComInterfaceFlags.None,
                                                                                 true);

        if (FindRenderImage() is Image image)
        {
            image.Source = _canvasSurfaceImageSource;
        }
    }

    private void DisposeRenderTarget()
    {
        // Dispose D3DDeviceContext and its dependencies
        _canvasD3DDeviceContext?.Dispose();
        _canvasSurfaceImageSourceNative?.SetDevice(nint.Zero);
        ComMarshal<ISurfaceImageSourceNativeWithD2D>.TryReleaseComObject(_canvasSurfaceImageSourceNative, out _);

        _canvasDevice?.Dispose();
        if (FindRenderImage() is Image image)
        {
            image.Source = null;
        }

        _canvasSurfaceImageSourceNativePtr = nint.Zero;

        Interlocked.Exchange(ref _canvasDevice, null);
        Interlocked.Exchange(ref _canvasD3DDeviceContext, null);
        Interlocked.Exchange(ref _canvasSurfaceImageSourceNative, null);
        Interlocked.Exchange(ref _canvasSurfaceImageSource, null);
    }

    private Image? FindRenderImage() => _backgroundGrid.Children
        .OfType<Image>()
        .Where(x => x.Name == "VideoRenderFrame")
        .LastOrDefault();

    #endregion

    #region Video Player Events

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

    public void Play()
    {
        try
        {
            if (_videoPlayer != null!)
            {
                InitializeRenderTarget();
                _videoPlayer.VideoFrameAvailable += VideoPlayer_VideoFrameAvailable;
                _videoPlayer.Play();
                _isBlockVideoFrameDraw = 0;
            }
            else if (_lastBackgroundSourceType == MediaSourceType.Video &&
                     BackgroundSource != null)
            {
                // Try loading last media
                LoadFromSourceAsyncDetached(BackgroundSourceProperty,
                                            _lastBackgroundSource,
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
                DisposeRenderTarget();
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
