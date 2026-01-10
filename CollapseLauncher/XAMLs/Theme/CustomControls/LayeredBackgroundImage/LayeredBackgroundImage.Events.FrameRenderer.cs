using Hi3Helper.Data;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Interfaces.DXGI;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.WinRT.SwapChainPanelHelper;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
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

    private volatile int      _isBlockVideoFrameDraw = 1;
    private          int      _isVideoFrameDrawInProgress;
    private          TimeSpan _lastVideoPlayerPosition;

    #endregion

    #region Video Frame Drawing

    private void VideoPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
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

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, DrawFrame);
        return;

        void DrawFrame()
        {
            if (_canvasSurfaceImageSourceNativePtr == nint.Zero)
            {
                return;
            }

            try
            {
                SwapChainPanelHelper.NativeSurfaceImageSource_BeginDrawUnsafe(_canvasSurfaceImageSourceNativePtr, in _canvasRenderArea, out nint surfacePpv);
                SwapChainPanelHelper.MediaPlayer_CopyFrameToVideoSurfaceUnsafe(_videoPlayerPtr, surfacePpv);
                SwapChainPanelHelper.NativeSurfaceImageSource_EndDrawUnsafe(_canvasSurfaceImageSourceNativePtr);
                Marshal.Release(surfacePpv);
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

        ComMarshal<IWinRTObject>.TryGetComInterfaceReference(_videoPlayer,
                                                             in IMediaPlayer_IID,
                                                             out _videoPlayerPtr,
                                                             out _,
                                                             CreateComInterfaceFlags.None,
                                                             true);
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
        if (_videoPlayer.CanSeek)
        {
            _lastVideoPlayerPosition = _videoPlayer.Position;
        }

        _videoPlayer.Dispose();
        _videoPlayer.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailable;
        _videoPlayer.MediaOpened         -= InitializeVideoFrameOnMediaOpened;
        Interlocked.Exchange(ref _videoPlayer!, null);
        _videoPlayerPtr = nint.Zero;
    }

    private void InitializeRenderTargetSize(MediaPlaybackSession playbackSession)
    {
        double currentCanvasWidth = playbackSession.NaturalVideoWidth;
        double currentCanvasHeight = playbackSession.NaturalVideoHeight;

        _canvasWidth      = (int)(currentCanvasWidth * XamlRoot.RasterizationScale * 2d);
        _canvasHeight     = (int)(currentCanvasHeight * XamlRoot.RasterizationScale * 2d);
        _canvasRenderArea = new Rect(0, 0, _canvasWidth, _canvasHeight);
    }

    private void InitializeRenderTarget()
    {
        DisposeRenderTarget(); // Always ensure the previous render target has been disposed

        _canvasDevice             = CanvasDevice.GetSharedDevice();
        _canvasSurfaceImageSource = new SurfaceImageSource(_canvasWidth, _canvasHeight, true);
        SwapChainPanelHelper.GetNativeSurfaceImageSource(_canvasSurfaceImageSource,
                                                         out _canvasSurfaceImageSourceNative,
                                                         out _canvasD3DDeviceContext);

        ComMarshal<ISurfaceImageSourceNativeWithD2D>.TryGetComInterfaceReference(_canvasSurfaceImageSourceNative!,
                                                                                 out _canvasSurfaceImageSourceNativePtr,
                                                                                 out _,
                                                                                 CreateComInterfaceFlags.None,
                                                                                 true);

        SetRenderImageSource(_canvasSurfaceImageSource);
    }

    private void DisposeRenderTarget()
    {
        // Dispose D3DDeviceContext and its dependencies
        _canvasD3DDeviceContext?.Dispose();
        _canvasSurfaceImageSourceNative?.SetDevice(nint.Zero);
        ComMarshal<ISurfaceImageSourceNativeWithD2D>.TryReleaseComObject(_canvasSurfaceImageSourceNative, out _);

        _canvasDevice?.Dispose();
        SetRenderImageSource(null);

        _canvasSurfaceImageSourceNativePtr = nint.Zero;

        Interlocked.Exchange(ref _canvasDevice, null);
        Interlocked.Exchange(ref _canvasD3DDeviceContext, null);
        Interlocked.Exchange(ref _canvasSurfaceImageSourceNative, null);
        Interlocked.Exchange(ref _canvasSurfaceImageSource, null);
    }

    private void SetRenderImageSource(ImageSource? renderSource)
    {
        try
        {
            _backgroundGrid?
               .DispatcherQueue?
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
