using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.StreamUtility;
using CommunityToolkit.WinUI.Animations;
#if USEFFMPEGFORVIDEOBG
using FFmpegInteropX;
#endif
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;
using static Hi3Helper.Logger;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal sealed partial class MediaPlayerLoader : IBackgroundMediaLoader
    {
        private readonly Color _currentDefaultColor = Color.FromArgb(0, 0, 0, 0);
        private          bool  _isCanvasCurrentlyDrawing;

        private FrameworkElement ParentUI               { get; }
        private Compositor       CurrentCompositor      { get; }
        private DispatcherQueue  CurrentDispatcherQueue { get; }
        private static bool IsUseVideoBgDynamicColorUpdate { get => LauncherConfig.IsUseVideoBGDynamicColorUpdate && LauncherConfig.EnableAcrylicEffect; }

        private Grid AcrylicMask      { get; }
        private Grid OverlayTitleBar  { get; }
        public  bool IsBackgroundDimm { get; set; }

        private FileStream?        _currentMediaStream;
        private MediaPlayer?       _currentMediaPlayer;
    #if USEFFMPEGFORVIDEOBG
        private FFmpegMediaSource? _currentFFmpegMediaSource;
#endif

        private          CanvasImageSource?  _currentCanvasImageSource;
        private          CanvasBitmap?       _currentCanvasBitmap;
        private          CanvasDevice?       _currentCanvasDevice;
        private readonly int                 _currentCanvasWidth;
        private readonly int                 _currentCanvasHeight;
        private readonly float               _currentCanvasDpi;
        private readonly MediaPlayerElement? _currentMediaPlayerFrame;
        private readonly Grid                _currentMediaPlayerFrameParentGrid;
        private readonly ImageUI             _currentImage;

        internal MediaPlayerLoader(
            FrameworkElement parentUI,
            Grid             acrylicMask,           Grid                overlayTitleBar,
            Grid             mediaPlayerParentGrid, MediaPlayerElement? mediaPlayerCurrent)
        {
            ParentUI               = parentUI;
            CurrentCompositor      = parentUI.GetElementCompositor();
            CurrentDispatcherQueue = parentUI.DispatcherQueue;

            AcrylicMask     = acrylicMask;
            OverlayTitleBar = overlayTitleBar;

            _currentMediaPlayerFrameParentGrid = mediaPlayerParentGrid;
            _currentMediaPlayerFrame           = mediaPlayerCurrent;

            _currentCanvasWidth  = (int)_currentMediaPlayerFrameParentGrid.ActualWidth;
            _currentCanvasHeight = (int)_currentMediaPlayerFrameParentGrid.ActualHeight;
            _currentCanvasDpi    = 96f;

            _currentImage = mediaPlayerParentGrid.AddElementToGridRowColumn(new ImageUI
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Stretch             = Stretch.UniformToFill
            });
        }

        ~MediaPlayerLoader()
        {
            LogWriteLine("[~MediaPlayerLoader()] MediaPlayerLoader Destructor has been called!", LogType.Warning, true);
            Dispose();
        }

        public void Dispose()
        {
            try
            {
                DisposeMediaModules();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error disposing Media Modules: {ex.Message}", LogType.Error, true);
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
            }

            GC.SuppressFinalize(this);
        }

        public async Task LoadAsync(string filePath,      bool              isImageLoadForFirstTime,
                                    bool   isRequestInit, CancellationToken token)
        {
            try
            {
                DisposeMediaModules();
                _currentMediaPlayer ??= new MediaPlayer();

                if (IsUseVideoBgDynamicColorUpdate)
                {
                    _currentCanvasDevice ??= CanvasDevice.GetSharedDevice();
                    _currentCanvasImageSource ??= new CanvasImageSource(_currentCanvasDevice,
                                                                        _currentCanvasWidth,
                                                                        _currentCanvasHeight,
                                                                        _currentCanvasDpi,
                                                                        CanvasAlphaMode.Premultiplied);
                    _currentImage.Source = _currentCanvasImageSource;

                    byte[] temporaryBuffer = ArrayPool<byte>.Shared.Rent(_currentCanvasWidth * _currentCanvasHeight * 4);
                    try
                    {
                        _currentCanvasBitmap ??= CanvasBitmap.CreateFromBytes(_currentCanvasDevice,
                                                                              temporaryBuffer,
                                                                              _currentCanvasWidth,
                                                                              _currentCanvasHeight,
                                                                              Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                                                              _currentCanvasDpi,
                                                                              CanvasAlphaMode.Premultiplied);
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(temporaryBuffer);
                    }

                    _currentImage.Visibility = Visibility.Visible;
                    App.ToggleBlurBackdrop();
                }
                else if (_currentImage != null)
                {
                    _currentImage.Visibility = Visibility.Collapsed;
                }

                await GetPreviewAsColorPalette(filePath);

                _currentMediaStream ??= BackgroundMediaUtility.GetAlternativeFileStream() ?? File.Open(filePath, StreamExtension.FileStreamOpenReadOpt);

#if !USEFFMPEGFORVIDEOBG
                EnsureIfFormatIsDashOrUnsupported(_currentMediaStream);
#endif

                _currentMediaPlayer ??= new MediaPlayer();

                if (WindowUtility.IsCurrentWindowInFocus())
                {
                    _currentMediaPlayer.AutoPlay = true;
                }

                bool   isAudioMute     = LauncherConfig.GetAppConfigValue("BackgroundAudioIsMute").ToBool();
                double lastAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();

                _currentMediaPlayer.IsMuted          = isAudioMute;
                _currentMediaPlayer.Volume           = lastAudioVolume;
                _currentMediaPlayer.IsLoopingEnabled = true;

#if !USEFFMPEGFORVIDEOBG
                _currentMediaPlayer.SetStreamSource(_currentMediaStream.AsRandomAccessStream());
#else

                _currentFFmpegMediaSource ??= await FFmpegMediaSource.CreateFromStreamAsync(CurrentMediaStream.AsRandomAccessStream());

                await _currentFFmpegMediaSource.OpenWithMediaPlayerAsync(CurrentMediaPlayer);
                const string MediaInfoStrFormat = @"Playing background video with FFmpeg!
    Media Duration: {0}
    Video Resolution: {9}x{10} px
    Video Codec: {1}
    Video Codec Decoding Method: {3}
    Video Decoder Engine: {11}
    Video Bitrate: {2} bps
    Video Bitdepth: {11} Bits
    Audio Codec: {4}
    Audio Bitrate: {5} bps
    Audio Channel: {6}
    Audio Sample: {7}Hz
    Audio Bitwide: {8} Bits
";
                Logger.LogWriteLine(
                    string.Format(MediaInfoStrFormat,
                    _currentFFmpegMediaSource.Duration.ToString("c"),                                // 0
                    _currentFFmpegMediaSource.CurrentVideoStream?.CodecName ?? "No Video Stream",    // 1
                    _currentFFmpegMediaSource.CurrentVideoStream?.Bitrate ?? 0,                      // 2
                    _currentFFmpegMediaSource.CurrentVideoStream?.DecoderEngine                      // 3
                    == DecoderEngine.FFmpegD3D11HardwareDecoder ? "Hardware" : "Software",
                    _currentFFmpegMediaSource.CurrentAudioStream?.CodecName ?? "No Audio Stream",    // 4
                    _currentFFmpegMediaSource.CurrentAudioStream?.Bitrate ?? 0,                      // 5
                    _currentFFmpegMediaSource.CurrentAudioStream?.Channels ?? 0,                     // 6
                    _currentFFmpegMediaSource.CurrentAudioStream?.SampleRate ?? 0,                   // 7
                    _currentFFmpegMediaSource.CurrentAudioStream?.BitsPerSample ?? 0,                // 8
                    _currentFFmpegMediaSource.CurrentVideoStream?.PixelWidth ?? 0,                   // 9
                    _currentFFmpegMediaSource.CurrentVideoStream?.PixelHeight ?? 0,                  // 10
                    _currentFFmpegMediaSource.CurrentVideoStream?.BitsPerSample ?? 0,                // 11
                    _currentFFmpegMediaSource.CurrentVideoStream?.DecoderEngine ?? 0                 // 12
                    ), LogType.Debug, true);
#endif
                _currentMediaPlayer.IsVideoFrameServerEnabled = IsUseVideoBgDynamicColorUpdate;
                if (IsUseVideoBgDynamicColorUpdate)
                {
                    _currentMediaPlayer.VideoFrameAvailable += FrameGrabberEvent;
                }

                _currentMediaPlayerFrame?.SetMediaPlayer(_currentMediaPlayer);
                _currentMediaPlayer.Play();
            }
            catch
            {
                DisposeMediaModules();
                await BackgroundMediaUtility.AssignDefaultImage(_currentImage);
                throw;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public void DisposeMediaModules()
        {
            if (_currentMediaPlayer != null)
            {
                _currentMediaPlayer.VideoFrameAvailable -= FrameGrabberEvent;
                _currentMediaPlayer.Dispose();
                Interlocked.Exchange(ref _currentMediaPlayer, null);
            }

            if (IsUseVideoBgDynamicColorUpdate)
            {
                while (_isCanvasCurrentlyDrawing)
                {
                    Thread.Sleep(100);
                }
            }

            if (_currentCanvasImageSource != null)
            {
                Interlocked.Exchange(ref _currentCanvasImageSource, null);
            }

            if (_currentCanvasBitmap != null)
            {
                _currentCanvasBitmap.Dispose();
                Interlocked.Exchange(ref _currentCanvasBitmap, null);
            }

            if (_currentCanvasDevice != null)
            {
                _currentCanvasDevice.Dispose();
                Interlocked.Exchange(ref _currentCanvasDevice, null);
            }

#if USEFFMPEGFORVIDEOBG
            _currentFFmpegMediaSource?.Dispose();
            _currentFFmpegMediaSource = null;
#endif
            _currentMediaStream?.Dispose();
            _currentMediaStream = null;
        }

#if !USEFFMPEGFORVIDEOBG
        private static void EnsureIfFormatIsDashOrUnsupported(Stream stream)
        {
            ReadOnlySpan<byte> dashSignature = "ftypdash"u8;

            byte[] buffer = new byte[64];
            stream.ReadExactly(buffer);

            try
            {
                if (buffer.AsSpan(4).StartsWith(dashSignature))
                    throw new FormatException("The video format is in \"MPEG-DASH\" format, which is unsupported.");
            }
            finally
            {
                stream.Position = 0;
            }
        }
#endif

        private async Task GetPreviewAsColorPalette(string file)
        {
            StorageFile                storageFile = await GetFileAsStorageFile(file);
            using StorageItemThumbnail thumbnail   = await storageFile.GetThumbnailAsync(ThumbnailMode.VideosView);
            await using Stream         stream      = thumbnail.AsStream();

            await ColorPaletteUtility.ApplyAccentColor(ParentUI, stream.AsRandomAccessStream(), string.Empty, false,
                                                       true);
        }

        private static async ValueTask<StorageFile> GetFileAsStorageFile(string filePath)
            => await StorageFile.GetFileFromPathAsync(filePath);

        private void FrameGrabberEvent(MediaPlayer mediaPlayer, object args)
        {
            if (_isCanvasCurrentlyDrawing)
            {
                return;
            }

            Interlocked.Exchange(ref _isCanvasCurrentlyDrawing, true);
            try
            {
                CurrentDispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, RunImpl);
            }
            catch (Exception e)
            {
                LogWriteLine($"[FrameGrabberEvent] Error drawing frame to canvas.\r\n{e}", LogType.Error, true);
            }
            finally
            {
                Interlocked.Exchange(ref _isCanvasCurrentlyDrawing, false);
            }

            return;

            void RunImpl()
            {
                using CanvasDrawingSession canvasDrawingSession = _currentCanvasImageSource!.CreateDrawingSession(_currentDefaultColor);
                mediaPlayer.CopyFrameToVideoSurface(_currentCanvasBitmap);
                canvasDrawingSession.DrawImage(_currentCanvasBitmap);
            }
        }

        public void Dimm()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(true));
        }

        public void Undimm()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ToggleImageVisibility(false));
        }

        private async Task ToggleImageVisibility(bool hideImage)
        {
            if (IsBackgroundDimm == hideImage) return;
            IsBackgroundDimm = hideImage;

            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                                                         ? BackgroundMediaUtility.TransitionDuration
                                                         : BackgroundMediaUtility.TransitionDurationSlow);
            await Task.WhenAll(
                AcrylicMask.StartAnimation(
                    duration,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                        hideImage ? 1f : 0f,
                        hideImage ? 0f : 1f)
                    ),
                OverlayTitleBar.StartAnimation(
                    duration,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                        hideImage ? 0f : 1f,
                        hideImage ? 1f : 0f)
                    )
                );
        }

        public void Show(bool isForceShow = false)
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(ShowInner());
        }

        private async Task ShowInner()
        {
            if (_currentMediaPlayerFrameParentGrid.Opacity > 0f) return;

            if (!IsUseVideoBgDynamicColorUpdate)
            {
                App.ToggleBlurBackdrop(false);
            }
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await _currentMediaPlayerFrameParentGrid
               .StartAnimation(duration,
                               CurrentCompositor
                                  .CreateScalarKeyFrameAnimation("Opacity", 1f, 0f)
                              );
        }

        public void Hide()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(HideInner());
        }

        private async Task HideInner()
        {
            bool isLastAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();

            if (!IsUseVideoBgDynamicColorUpdate)
            {
                App.ToggleBlurBackdrop(isLastAcrylicEnabled);
            }

            if (_currentMediaPlayerFrameParentGrid.Opacity < 1f) return;
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await _currentMediaPlayerFrameParentGrid
               .StartAnimation(duration,
                               CurrentCompositor
                                  .CreateScalarKeyFrameAnimation("Opacity", 0f,
                                                                 (float)_currentMediaPlayerFrameParentGrid
                                                                    .Opacity)
                              );

            DisposeMediaModules();
        }

        public async void WindowUnfocused()
        {
            try
            {
                await (BackgroundMediaUtility.SharedActionBlockQueue?.SendAsync(WindowUnfocusedInner()) ?? Task.CompletedTask);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task WindowUnfocusedInner()
        {
            double currentAudioVolume = _currentMediaPlayer?.Volume ?? 0;
            await InterpolateVolumeChange((float)currentAudioVolume, 0f, true);
            Pause();
        }

        public async void WindowFocused()
        {
            try
            {
                await (BackgroundMediaUtility.SharedActionBlockQueue?.SendAsync(WindowFocusedInner()) ?? Task.CompletedTask);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task WindowFocusedInner()
        {
            double currentAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume")
                                                      .ToDouble();
            Play();
            await InterpolateVolumeChange(0f, (float)currentAudioVolume, false);
        }

        public void Mute()
        {
            if (_currentMediaPlayer == null) return;
            _currentMediaPlayer.IsMuted = true;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            if (_currentMediaPlayer == null) return;

            _currentMediaPlayer.IsMuted = false;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        private async ValueTask InterpolateVolumeChange(float from, float to, bool isMute)
        {
            if (_currentMediaPlayer == null) return;

            double tFrom = from;
            double tTo   = to;

            double current = tFrom;
            double inc     = isMute ? -0.05 : 0.05;

            Loops:
            current                   += inc;
            _currentMediaPlayer.Volume =  current;

            await Task.Delay(10);
            switch (isMute)
            {
                case true when current > tTo - inc:
                case false when current < tTo - inc:
                    goto Loops;
            }

            _currentMediaPlayer.Volume = tTo;
        }

        public void SetVolume(double value)
        {
            if (_currentMediaPlayer != null)
                _currentMediaPlayer.Volume = value;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioVolume", value);
        }

        public void Play()
        {
            _currentMediaPlayer?.Play();
        }

        public void Pause()
        {
            _currentMediaPlayer?.Pause();
        }
    }
}