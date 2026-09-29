using CollapseLauncher.Extension;
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

// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private MediaPlayer? _videoFramePlayer;

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
                IsVideoFrameServerEnabled = true,
                Volume                    = AudioVolume.GetClampedVolume(),
                IsMuted                   = !IsAudioEnabled,
                CommandManager            = { IsEnabled = false }
            };
            Interlocked.Exchange(ref _videoPlayer, player);

            player.MediaOpened += InitializeVideoFrameOnMediaOpened;
            player.MediaFailed += VideoPlayer_OnMediaFailed;

            if (!_useSafeFrameRenderer)
            {
                ((IWinRTObject)player).NativeObject.TryAs(IMediaPlayer5_IID, out _videoPlayerPtr);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeVideoPlayer] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private void VideoPlayer_OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Logger.LogWriteLine($"[LayeredBackgroundImage::MediaFailed] Decoder: {(UseFfmpegDecoder ? "FFmpeg" : "Windows")}; {args.Error}: {args.ErrorMessage}\r\n{args.ExtendedErrorCode}",
                            LogType.Error,
                            true);
    }

    private bool InitializeRenderTargetSize(MediaPlaybackSession playbackSession)
    {
        try
        {
            int width  = (int)playbackSession.NaturalVideoWidth;
            int height = (int)playbackSession.NaturalVideoHeight;

            // In some occasion, MediaPlayer reportedly 0x0px size which causes E_INVALIDARG while rendering frame
            // if FFmpeg source is used. So, use size reported by FFmpeg instead.
            if ((width <= 0 || height <= 0) && _videoFfmpegMediaSource?.CurrentVideoStream is { } videoStream)
            {
                width  = videoStream.PixelWidth;
                height = videoStream.PixelHeight;
            }

            if (width <= 0 || height <= 0) return false;

            _canvasWidth      = width;
            _canvasHeight     = height;
            _canvasRenderSize = new Rect(0, 0, _canvasWidth, _canvasHeight);
            return true;
        }
        catch (COMException ex) when ((uint)ex.HResult == 0xC00D3E85u)
        {
            // A queued MediaOpened callback can outlive the playback session.
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTargetSize] {ex}",
                                LogType.Error,
                                true);
            return false;
        }
    }

    private unsafe bool InitializeRenderTarget()
    {
        try
        {
            // Play() can arrive before MediaOpened. Read this player's size before creating any surfaces.
            if (_videoPlayer == null || !InitializeRenderTargetSize(_videoPlayer.PlaybackSession)) return false;

            Interlocked.Exchange(ref _isBlockVideoFrameDraw, 1); // Block frame drawing routine
            DisposeRenderTarget(_canvasImageSource == null); // Always ensure the previous render target has been disposed

            _canvasDevice ??= CanvasDevice.GetSharedDevice();
            _canvasImageSource ??= new CanvasVirtualImageSource(_canvasDevice,
                                                                _canvasWidth,
                                                                _canvasHeight,
                                                                96f);

            _canvasRenderTarget ??= new CanvasRenderTarget(_canvasDevice,
                                                           _canvasWidth,
                                                           _canvasHeight,
                                                           96f);

            Interlocked.Exchange(ref _useSafeFrameRenderer, false);
            if (_useSafeFrameRenderer)
            {
                return true;
            }

            _canvasImageSourceNativePtr  = ((IWinRTObject)_canvasImageSource).NativeObject.ThisPtr;

            try
            {
                // DrawImageToRect requires ICanvasBitmap, not the render target's default ICanvasRenderTarget interface.
                Guid canvasBitmapIid = new("C57532ED-709E-4AC2-86BE-A1EC3A7FA8FE");
                Marshal.ThrowExceptionForHR(((IWinRTObject)_canvasRenderTarget).NativeObject.TryAs(canvasBitmapIid, out _canvasRenderTargetNativePtr));
                Marshal.ThrowExceptionForHR(((IWinRTObject)_canvasRenderTarget).NativeObject.TryAs(typeof(IDirect3DSurface).GUID, out _canvasRenderTargetAsSurfacePtr));

                if (_functionTableBeginDraw == null! ||
                    _functionTableDrawImage == null! ||
                    _functionTableCopyFrameToVideoSurface == null! ||
                    _functionTableDispose == null!)
                {
                    SwapChainPanelHelper.GetDirectNativeDelegateForDrawRoutine(_canvasImageSourceNativePtr,
                                                                               _canvasRenderTargetNativePtr,
                                                                               _videoPlayerPtr,
                                                                               out _functionTableBeginDraw,
                                                                               out _functionTableDrawImage,
                                                                               out _functionTableCopyFrameToVideoSurface,
                                                                               out _functionTableDispose,
                                                                               in _canvasRenderSize);
                }
            }
            catch (Exception e)
            {
                NullifyRenderTargetNativePointers();
                Interlocked.Exchange(ref _useSafeFrameRenderer, true); // Fallback

                _functionTableBeginDraw               = null;
                _functionTableDrawImage               = null;
                _functionTableCopyFrameToVideoSurface = null;
                _functionTableDispose                 = null;
                Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTarget] Failed to initialize fast-unsafe method for frame rendering. Fallback to safe renderer.\r\n{e}",
                                    LogType.Error,
                                    true);
            }
            return true;
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::InitializeRenderTarget] FATAL: {e}",
                                LogType.Error,
                                true);
            return false;
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

    private void DetachVideoPlayerEvents(MediaPlayer player)
    {
        player.MediaOpened -= InitializeVideoFrameOnMediaOpened;
        player.MediaFailed -= VideoPlayer_OnMediaFailed;
        player.PlaybackSession.NaturalVideoSizeChanged -= InitializeVideoFrameOnSizeChanged;
        player.VideoFrameAvailable -= NotifyVideoLoaded;
        player.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailableUnsafe;
        player.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailableSafe;
        player.PlaybackSession.PositionChanged -= MediaDurationPosition_OnChangedBridge;
    }

    private void DisposeVideoPlayer(bool disposeRenderImageSource = true)
    {
        try
        {
            MediaPlayer? player = _videoPlayer;
            if (player is null || player.IsObjectDisposed())
            {
                return;
            }

            DetachVideoPlayerEvents(player);
            player.Pause();

            // Save last video player duration for later
            if (player.CanSeek)
            {
                _ = SaveMediaPosition(BackgroundSource, player.Position);
            }

            if (!_useSafeFrameRenderer)
            {
                NullifyMediaPlayerNativePointers();
            }

            Interlocked.Exchange(ref _videoFfmpegMediaSource, null)?.Dispose();
            Interlocked.Exchange(ref _videoPlayer, null)?.Dispose();

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
            _videoFramePlayer = null;
            Interlocked.Exchange(ref _isVideoInitialized, 0);
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

            if (!_useSafeFrameRenderer)
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
        //    This is necessary as we just cast/QueryInterface the _videoPlayer object
        //    (as IWinRTObject, then took its direct pointer) into IMediaPlayer5. If not
        //    released, the reference on the IWinRTObject will not be zeroed, causing leak.
        if (_videoPlayerPtr != nint.Zero) Marshal.Release(Interlocked.Exchange(ref _videoPlayerPtr, nint.Zero));
    }

    private void NullifyRenderTargetNativePointers()
    {
        // -- Note to myself @neon-nyan:
        //    Release IDirect3DSurface reference first, then dispose the whole CanvasRenderTarget.
        //    This is necessary as we queried the render target's native object for
        //    IDirect3DSurface. If not released,
        //    the reference on the IWinRTObject will not be zeroed, causing leak.
        if (_canvasRenderTargetAsSurfacePtr != nint.Zero) Marshal.Release(Interlocked.Exchange(ref _canvasRenderTargetAsSurfacePtr, nint.Zero));

        // -- Release the ICanvasBitmap reference acquired for DrawImageToRect.
        if (_canvasRenderTargetNativePtr != nint.Zero) Marshal.Release(Interlocked.Exchange(ref _canvasRenderTargetNativePtr, nint.Zero));

        // -- Nullify the borrowed IWinRTObject pointer.
        Interlocked.Exchange(ref _canvasImageSourceNativePtr,  nint.Zero);
    }

    #endregion
}
