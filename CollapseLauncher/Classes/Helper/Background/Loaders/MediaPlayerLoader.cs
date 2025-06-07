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
using Windows.Foundation;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;
using static Hi3Helper.Logger;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable StringLiteralTypo
// ReSharper disable AsyncVoidMethod
// ReSharper disable BadControlBracesIndent

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal sealed partial class MediaPlayerLoader : IBackgroundMediaLoader
    {
        private readonly Color _currentDefaultColor = Color.FromArgb(0, 0, 0, 0);

        private FrameworkElement ParentUI               { get; }
        private Compositor       CurrentCompositor      { get; }
        private DispatcherQueue  CurrentDispatcherQueue { get; }
        private static bool IsUseVideoBgDynamicColorUpdate
        {
            get => LauncherConfig.IsUseVideoBGDynamicColorUpdate && LauncherConfig.EnableAcrylicEffect;
        }

        private Grid AcrylicMask      { get; }
        private Grid OverlayTitleBar  { get; }
        public  bool IsBackgroundDimm { get; set; }

        private FileStream?        _currentMediaStream;
        private MediaPlayer?       _currentMediaPlayer;
#if USEFFMPEGFORVIDEOBG
        private FFmpegMediaSource? _currentFFmpegMediaSource;
#endif

        private const float CanvasBaseDpi = 96f;

        private          CanvasVirtualImageSource? _currentCanvasVirtualImageSource;
        private          CanvasBitmap?             _currentCanvasBitmap;
        private          CanvasDevice?             _currentCanvasDevice;
        private volatile CanvasDrawingSession?     _currentCanvasDrawingSession;
        private volatile float                     _currentCanvasWidth;
        private volatile float                     _currentCanvasHeight;
        private          Rect                      _currentCanvasDrawArea;
        private readonly MediaPlayerElement?       _currentMediaPlayerFrame;
        private readonly Grid                      _currentMediaPlayerFrameParentGrid;
        private readonly ImageUI                   _currentImage;
        private readonly Lock                      _currentLock = new();

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

            _currentMediaPlayerFrameParentGrid             =  mediaPlayerParentGrid;
            _currentMediaPlayerFrameParentGrid.SizeChanged += UpdateCanvasOnSizeChangeEvent;
            _currentMediaPlayerFrame                       =  mediaPlayerCurrent;

            float actualWidth   = (float)_currentMediaPlayerFrameParentGrid.ActualWidth;
            float actualHeight  = (float)_currentMediaPlayerFrameParentGrid.ActualHeight;
            float scalingFactor = (float)WindowUtility.CurrentWindowMonitorScaleFactor;

            _currentCanvasWidth    = actualWidth * scalingFactor;
            _currentCanvasHeight   = actualHeight * scalingFactor;
            _currentCanvasDrawArea = new Rect(0f, 0f, _currentCanvasWidth, _currentCanvasHeight);

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
                _currentMediaPlayerFrameParentGrid.SizeChanged -= UpdateCanvasOnSizeChangeEvent;
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
                    CreateAndAssignCanvasVirtualImageSource();
                    CreateCanvasBitmap();

                    _currentImage.Visibility = Visibility.Visible;
                    App.ToggleBlurBackdrop();
                }
                else
                {
                    _currentImage.Visibility = Visibility.Collapsed;
                }

                await GetPreviewAsColorPalette(filePath);

                _currentMediaStream ??= BackgroundMediaUtility.GetAlternativeFileStream() ?? File.Open(filePath, StreamExtension.FileStreamOpenReadOpt);

#if !USEFFMPEGFORVIDEOBG
                EnsureIfFormatIsDashOrUnsupported(_currentMediaStream);
                _currentMediaPlayer ??= new MediaPlayer();
#endif

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
                _currentFFmpegMediaSource ??= await FFmpegMediaSource.CreateFromStreamAsync(_currentMediaStream.AsRandomAccessStream());

                await _currentFFmpegMediaSource.OpenWithMediaPlayerAsync(_currentMediaPlayer);
                const string mediaInfoStrFormat = """
                                                  Playing background video with FFmpeg!
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
                                                  """;
                LogWriteLine(
                    string.Format(mediaInfoStrFormat,
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
                    _currentFFmpegMediaSource.CurrentVideoStream?.BitsPerSample ?? 0                 // 11
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

        private void UpdateCanvasOnSizeChangeEvent(object sender, SizeChangedEventArgs e)
        {
            using (_currentLock.EnterScope())
            {
                float scalingFactor = (float)WindowUtility.CurrentWindowMonitorScaleFactor;
                float newWidth      = (float)(e.NewSize.Width * scalingFactor);
                float newHeight     = (float)(e.NewSize.Height * scalingFactor);

                LogWriteLine($"Updating video canvas size from: {_currentCanvasWidth}x{_currentCanvasHeight} to {newWidth}x{newHeight}", LogType.Debug, true);

                _currentCanvasWidth    = newWidth;
                _currentCanvasHeight   = newHeight;
                _currentCanvasDrawArea = new Rect(0, 0, _currentCanvasWidth, _currentCanvasHeight);

                _currentCanvasBitmap?.Dispose();
                _currentCanvasBitmap             = null;
                _currentCanvasVirtualImageSource = null;
                CreateAndAssignCanvasVirtualImageSource();
                CreateCanvasBitmap();
            }
        }

        private void CreateAndAssignCanvasVirtualImageSource()
        {
            _currentCanvasVirtualImageSource ??= new CanvasVirtualImageSource(_currentCanvasDevice,
                                                                              _currentCanvasWidth,
                                                                              _currentCanvasHeight,
                                                                              CanvasBaseDpi);

            _currentImage.Source = _currentCanvasVirtualImageSource.Source;
        }

        private void CreateCanvasBitmap()
        {
            int widthInt  = (int)_currentCanvasWidth;
            int heightInt = (int)_currentCanvasHeight;

            byte[] temporaryBuffer = ArrayPool<byte>.Shared.Rent(widthInt * heightInt * 4);
            try
            {
                _currentCanvasBitmap ??= CanvasBitmap
                    .CreateFromBytes(_currentCanvasDevice,
                                     temporaryBuffer,
                                     widthInt,
                                     heightInt,
                                     Windows.Graphics.DirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized,
                                     CanvasBaseDpi,
                                     CanvasAlphaMode.Ignore);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(temporaryBuffer);
            }
        }

        public void DisposeMediaModules()
        {
#if !USEFFMPEGFORVIDEOBG
            if (_currentMediaPlayer != null)
            {
                _currentMediaPlayer.VideoFrameAvailable -= FrameGrabberEvent;
                _currentMediaPlayer.Dispose();
                Interlocked.Exchange(ref _currentMediaPlayer, null);
            }
#endif

            if (IsUseVideoBgDynamicColorUpdate)
            {
                using (_currentLock.EnterScope())
                {
                    _currentCanvasDrawingSession?.Dispose();
                    Interlocked.Exchange(ref _currentCanvasDrawingSession, null);
                }
            }

            if (_currentCanvasVirtualImageSource != null)
            {
                Interlocked.Exchange(ref _currentCanvasVirtualImageSource, null);
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

            Span<byte> buffer = stackalloc byte[64];
            stream.ReadExactly(buffer);

            try
            {
                if (buffer.StartsWith(dashSignature))
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

            await ColorPaletteUtility
                .ApplyAccentColor(ParentUI,
                                  thumbnail,
                                  string.Empty,
                                  false,
                                  true);
        }

        private static async Task<StorageFile> GetFileAsStorageFile(string filePath)
            => await StorageFile.GetFileFromPathAsync(filePath);

    #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        private async void FrameGrabberEvent(MediaPlayer mediaPlayer, object args)
    #pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            using (_currentLock.EnterScope())
            {
                if (_currentCanvasVirtualImageSource is null)
                {
                    return;
                }

                if (_currentCanvasDrawingSession is not null)
                {
#if DEBUG
                    LogWriteLine($@"[FrameGrabberEvent] Frame skipped at: {mediaPlayer.Position:hh\:mm\:ss\.ffffff}", LogType.Debug, true);
#endif
                    return;
                }
            }

            try
            {
                using (_currentLock.EnterScope())
                {
                    mediaPlayer.CopyFrameToVideoSurface(_currentCanvasBitmap);
                    _currentCanvasDrawingSession = _currentCanvasVirtualImageSource
                        .CreateDrawingSession(_currentDefaultColor,
                                              _currentCanvasDrawArea);
                    _currentCanvasDrawingSession.DrawImage(_currentCanvasBitmap);
                }
            }
            catch
#if DEBUG
            (Exception e)
            {
                LogWriteLine($"[FrameGrabberEvent] Error while drawing frame to bitmap.\r\n{e}", LogType.Warning, true);
            }
#else
            {
                // ignored
            }
#endif
            finally
            {
                using (_currentLock.EnterScope())
                {
                    CurrentDispatcherQueue.TryEnqueue(() =>
                    {
                        _currentCanvasDrawingSession?.Dispose();
                        _currentCanvasDrawingSession = null;
                    });
                }
            }
        }

        public void Dimm() => BackgroundMediaUtility.RunQueuedTask(ToggleImageVisibility(true));

        public void Undimm() => BackgroundMediaUtility.RunQueuedTask(ToggleImageVisibility(false));

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

        public void Show(bool isForceShow = false) => BackgroundMediaUtility.RunQueuedTask(ShowInner());

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

        public void Hide() => BackgroundMediaUtility.RunQueuedTask(HideInner());

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

        public void WindowUnfocused() => BackgroundMediaUtility.RunQueuedTask(WindowUnfocusedInner());

        private async Task WindowUnfocusedInner()
        {
            double currentAudioVolume = _currentMediaPlayer?.Volume ?? 0;
            await InterpolateVolumeChange((float)currentAudioVolume, 0f, true);
            Pause();
        }

        public void WindowFocused() => BackgroundMediaUtility.RunQueuedTask(WindowFocusedInner());

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

        private async Task InterpolateVolumeChange(float from, float to, bool isMute)
        {
            double tFrom = from;
            double tTo   = to;

            double current = tFrom;
            double inc     = isMute ? -0.05 : 0.05;

            Loops:
            if (_currentMediaPlayer == null) return;

            current += inc;
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