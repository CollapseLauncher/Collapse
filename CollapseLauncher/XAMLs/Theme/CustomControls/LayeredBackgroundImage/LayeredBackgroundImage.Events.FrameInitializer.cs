using CollapseLauncher.Helper;
using Hi3Helper;
using Hi3Helper.Win32.WinRT.SwapChainPanelHelper;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Playback;
using WinRT;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

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

            _videoPlayer = new MediaPlayer
            {
                IsLoopingEnabled          = true,
                IsVideoFrameServerEnabled = true,
                Volume                    = AudioVolume.GetClampedVolume(),
                IsMuted                   = !IsAudioEnabled
            };

            _videoPlayer.MediaOpened += InitializeVideoFrameOnMediaOpened;

            if (!UseSafeFrameRenderer)
            {
                ((IWinRTObject)_videoPlayer).NativeObject.TryAs(IMediaPlayer5_IID, out _videoPlayerPtr);
            }
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
            double currentCanvasWidth = playbackSession.NaturalVideoWidth;
            double currentCanvasHeight = playbackSession.NaturalVideoHeight;

            _canvasWidth = (int)(currentCanvasWidth * WindowUtility.CurrentWindowMonitorScaleFactor);
            _canvasHeight = (int)(currentCanvasHeight * WindowUtility.CurrentWindowMonitorScaleFactor);
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
            DisposeRenderTarget(_canvasImageSource == null); // Always ensure the previous render target has been disposed

            _videoPlayerDurationTimerThread =
                new Timer(UpdateMediaDurationTickView, this, TimeSpan.Zero, TimeSpan.FromSeconds(.5));

            _canvasImageSource ??= new CanvasVirtualImageSource(CanvasDevice.GetSharedDevice(),
                                                                _canvasWidth,
                                                                _canvasHeight,
                                                                96f);

            _canvasRenderTarget ??= new CanvasRenderTarget(_canvasImageSource,
                                                         _canvasWidth,
                                                         _canvasHeight,
                                                         96f);

            Interlocked.Exchange(ref UseSafeFrameRenderer, false);
            if (UseSafeFrameRenderer)
            {
                return;
            }

            _canvasRenderTargetNativePtr = ((IWinRTObject)_canvasRenderTarget).NativeObject.ThisPtr;
            _canvasImageSourceNativePtr  = ((IWinRTObject)_canvasImageSource).NativeObject.ThisPtr;

            ((IWinRTObject)_canvasRenderTarget).NativeObject.TryAs(typeof(IDirect3DSurface).GUID, out _canvasRenderTargetAsSurfacePtr);

            unsafe
            {
                try
                {
                    if (_functionTableBeginDraw == null ||
                        _functionTableDrawImage == null ||
                        _functionTableDispose == null)
                    {
                        SwapChainPanelHelper.GetDirectNativeDelegateForDrawRoutine(
                         _canvasImageSourceNativePtr,
                         _canvasRenderTargetNativePtr,
                         out _functionTableBeginDraw,
                         out _functionTableDrawImage,
                         out _functionTableDispose,
                         in _canvasRenderSize);
                    }
                }
                catch (Exception e)
                {
                    Interlocked.Exchange(ref UseSafeFrameRenderer, true); // Fallback

                    _functionTableBeginDraw = null;
                    _functionTableDrawImage = null;
                    _functionTableDispose   = null;
                    Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTarget] Failed to initialize fast-unsafe method for frame rendering. Fallback to safe renderer.\r\n{e}",
                                        LogType.Error,
                                        true);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTarget] FATAL: {e}",
                                LogType.Error,
                                true);
        }
        finally
        {
            if (_canvasImageSource != null)
            {
                SetRenderImageSource(_canvasImageSource.Source);
            }
            Interlocked.Exchange(ref _isBlockVideoFrameDraw, 0); // Unblock frame drawing routine
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

            _videoPlayer.VideoFrameAvailable -= !UseSafeFrameRenderer
                ? VideoPlayer_VideoFrameAvailableUnsafe
                : VideoPlayer_VideoFrameAvailableSafe;

            _videoPlayer.MediaOpened -= InitializeVideoFrameOnMediaOpened;
            _videoPlayer.Pause();

            // Save last video player duration for later
            if (_videoPlayer.CanSeek && TryGetSourceHashCode(BackgroundSource, out int sourceHashCode))
            {
                TimeSpan pos = _videoPlayer.Position;
                _ = SharedLastMediaPosition.AddOrUpdate(sourceHashCode, _ => pos, (_, _) => pos);
            }

            if (!UseSafeFrameRenderer)
            {
                NullifyMediaPlayerNativePointers();
            }

            // ReSharper disable once ConstantConditionalAccessQualifier
            Interlocked.Exchange(ref _videoPlayer,            null!)?.Dispose();
            Interlocked.Exchange(ref _videoFfmpegMediaSource, null)?.Dispose();

            DisposeRenderTarget(disposeRenderImageSource);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
        finally
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    private void DisposeRenderTarget(bool disposeRenderImageSource = true)
    {
        try
        {
            if (disposeRenderImageSource)
            {
                SetRenderImageSource(null);
                Interlocked.Exchange(ref _canvasImageSource,  null);
                Interlocked.Exchange(ref _canvasRenderTarget, null)?.Dispose();
            }

            _videoPlayerDurationTimerThread?.Dispose();

            if (!UseSafeFrameRenderer)
            {
                NullifyRenderTargetNativePointers();
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::DisposeRenderTarget] FATAL: {e}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region On Device Lost Handler

    private void CanvasDevice_OnDeviceLost()
    {
        Logger.LogWriteLine("[LayeredBackgroundImage::CanvasDevice_OnDeviceLost] Render device has been lost! Re-initialize render device and textures...",
                            LogType.Warning,
                            true);

        // -- Nullify _canvasImageSource so CanvasDevice and other dependencies are reinitialized too
        Interlocked.Exchange(ref _canvasImageSource, null!);
        InitializeRenderTarget();

        // Try to unlock video draw progress (if a throw happened inside frame drawing routine)
        Interlocked.Exchange(ref _isVideoFrameDrawInProgress, 0);
    }

    #endregion

    #region COM Native Pointer Nullifiers

    private void NullifyMediaPlayerNativePointers()
    {
        // -- Note to myself @neon-nyan:
        //    Release IMediaPlayer5 reference first, then dispose the whole MediaPlayer.
        //    This is necessary as we just cast the _videoPlayer object (as IWinRTObject, then took its direct pointer) into IMediaPlayer5.
        //    If not released, the reference on the IWinRTObject will not be zeroed, causing leak.
        if (_videoPlayerPtr != nint.Zero) Marshal.Release(Interlocked.Exchange(ref _videoPlayerPtr, nint.Zero));
    }

    private void NullifyRenderTargetNativePointers()
    {
        // -- Note to myself @neon-nyan:
        //    Release IDirect3DSurface reference first, then dispose the whole CanvasRenderTarget.
        //    This is necessary as we just cast the _canvasRenderTargetNativePtr (which is obtained from IWinRTObject's direct pointer) into IDirect3DSurface.
        //    If not released, the reference on the IWinRTObject will not be zeroed, causing leak.
        if (_canvasRenderTargetAsSurfacePtr != nint.Zero) Marshal.Release(Interlocked.Exchange(ref _canvasRenderTargetAsSurfacePtr, nint.Zero));

        // -- Nullify IWinRTObject direct pointers.
        Interlocked.Exchange(ref _canvasRenderTargetNativePtr, nint.Zero);
        Interlocked.Exchange(ref _canvasImageSourceNativePtr,  nint.Zero);
    }

    #endregion
}
