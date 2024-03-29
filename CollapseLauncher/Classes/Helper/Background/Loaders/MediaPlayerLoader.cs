using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CommunityToolkit.WinUI;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.Shared.Region;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    internal class MediaPlayerLoader : IBackgroundMediaLoader
    {
        private FrameworkElement _parentUI { get; init; }
        private Compositor _currentCompositor { get; init; }

        private MediaPlayerElement? _currentMediaPlayerFrame { get; init; }
        private ImageUI? _currentMediaImage { get; init; }
        private Grid _currentMediaPlayerFrameParentGrid { get; init; }

        private bool _isVolumeWindowUnfocusedChangeWatcherRunning { get; set; }
        private bool _isMediaPlayerLoading { get; set; }
        private bool _isMediaPlayerDimm { get; set; }
        private double _animationDuration { get; set; }
        private FileStream? _currentMediaStream { get; set; }
        private MediaSource? _currentMediaSource { get; set; }
        private MediaPlayer? _currentMediaPlayer { get; set; }
        private Stopwatch? _currentStopwatch { get; set; }
        private CancellationTokenSourceWrapper? _innerCancellationToken { get; set; }
        private List<bool>? _focusState { get; set; }

        private SoftwareBitmap? _currentFrameBitmap { get; set; }
        private CanvasImageSource? _currentCanvasImageSource { get; set; }
        private CanvasDevice? _canvasDevice { get; set; }
        private CanvasBitmap? _canvasBitmap { get; set; }
        private CanvasDrawingSession? _canvasDrawingSession { get; set; }

        internal MediaPlayerLoader(
            FrameworkElement parentUI,
            Grid mediaPlayerParentGrid, MediaPlayerElement? mediaPlayerCurrent,
            double animationDuration = BackgroundMediaUtility.TransitionDuration)
        {
            _parentUI = parentUI;
            _currentCompositor = parentUI.GetElementCompositor();

            _currentMediaPlayerFrameParentGrid = mediaPlayerParentGrid;
            _currentMediaPlayerFrame = mediaPlayerCurrent;
            _currentMediaImage = mediaPlayerParentGrid.AddElementToGridRowColumn(new ImageUI()
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithStretch(Stretch.UniformToFill));

            _animationDuration = animationDuration;
            _isMediaPlayerLoading = false;
        }

        public async ValueTask LoadAsync(string filePath, bool isImageLoadForFirstTime, bool isRequestInit, CancellationToken token)
        {
            // Wait until the image loading sequence is completed
            while (_isMediaPlayerLoading) await Task.Delay(250, token);

            if (_currentMediaStream != null) await _currentMediaStream.DisposeAsync();
            if (_currentMediaSource != null) _currentMediaSource.Dispose();
            if (_currentMediaPlayer != null)
            {
                _currentMediaPlayer.Dispose();
            }

            try
            {
                if (_innerCancellationToken != null)
                {
                    await _innerCancellationToken.CancelAsync();
                    _innerCancellationToken.Dispose();
                }

                if (_focusState == null) _focusState = new List<bool>();
                if (_focusState.Count > 0) _focusState.Clear();

                _innerCancellationToken = new CancellationTokenSourceWrapper();

                if (_currentMediaPlayer != null)
                {
                    _currentMediaPlayer.VideoFrameAvailable -= FrameGrabberEvent;
                    _currentMediaPlayer.Dispose();
                }

                await BackgroundMediaUtility.AssignDefaultImage(_currentMediaImage);

                int canvasWidth = (int)_currentMediaPlayerFrameParentGrid.ActualWidth;
                int canvasHeight = (int)_currentMediaPlayerFrameParentGrid.ActualHeight;

                _canvasDevice = CanvasDevice.GetSharedDevice();
                if (_currentFrameBitmap == null)
                {
                    // FrameServerImage in this example is a XAML image control
                    _currentFrameBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, canvasWidth, canvasHeight, BitmapAlphaMode.Ignore);
                }
                if (_currentCanvasImageSource == null)
                {
                    _currentCanvasImageSource = new CanvasImageSource(_canvasDevice, canvasWidth, canvasHeight, 96);//96); 
                }
                _currentMediaImage!.Source = _currentCanvasImageSource;

                _currentStopwatch = Stopwatch.StartNew();

                await GetPreviewAsColorPalette(filePath);

                _currentMediaStream = File.OpenRead(filePath);
                _currentMediaPlayer = new MediaPlayer();

                if (InnerLauncherConfig.IsWindowCurrentlyFocused())
                    _currentMediaPlayer.AutoPlay = true;

                bool IsAudioMute = LauncherConfig.GetAppConfigValue("BackgroundAudioIsMute").ToBool();
                double LastAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();

                _currentMediaPlayer.IsMuted = IsAudioMute;
                _currentMediaPlayer.Volume = LastAudioVolume;
                _currentMediaPlayer.IsLoopingEnabled = true;
                _currentMediaPlayer.SetStreamSource(_currentMediaStream.AsRandomAccessStream());
                // _currentMediaPlayer.IsVideoFrameServerEnabled = true;
                _currentMediaPlayer.VideoFrameAvailable += FrameGrabberEvent;

                _currentMediaPlayerFrame!.SetMediaPlayer(_currentMediaPlayer);
                VolumeWindowUnfocusedChangeWatcher(_innerCancellationToken);
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                _isMediaPlayerLoading = false;
            }
        }

        private async ValueTask GetPreviewAsColorPalette(string file)
        {
            StorageFile storageFile = await StorageFile.GetFileFromPathAsync(file);
            using StorageItemThumbnail thumbnail = await storageFile.GetThumbnailAsync(ThumbnailMode.VideosView);
            using Stream stream = thumbnail.AsStream();

            await ColorPaletteUtility.ApplyAccentColor(_parentUI, stream.AsRandomAccessStream(), string.Empty, false, false);
        }

        private async void FrameGrabberEvent(MediaPlayer mediaPlayer, object args)
        {
            await _parentUI.DispatcherQueue.EnqueueAsync(() =>
            {
                int bitmapWidth = _currentFrameBitmap?.PixelWidth ?? 0;
                int bitmapHeight = _currentFrameBitmap?.PixelHeight ?? 0;
                if (_canvasBitmap == null)
                    _canvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(_canvasDevice, _currentFrameBitmap);

                using (CanvasDrawingSession? ds = _currentCanvasImageSource?.CreateDrawingSession(Windows.UI.Color.FromArgb(255, 0, 0, 0)))
                {
                    mediaPlayer.CopyFrameToVideoSurface(_canvasBitmap);
                    ds?.DrawImage(_canvasBitmap);

                    if (_currentStopwatch?.ElapsedMilliseconds > 2000)
                    {
                        _currentStopwatch.Restart();
                        byte[] bufferBytes = _canvasBitmap.GetPixelBytes();
                        IntPtr ptr = ArrayToPtr(bufferBytes);

                        if (bufferBytes.Length >= bitmapWidth * bitmapHeight * 4)
                        {
                            UpdatePalette(new BitmapInputStruct
                            {
                                buffer = ptr,
                                channel = 4,
                                width = bitmapWidth,
                                height = bitmapHeight,
                            });
                        }
                    }
                }
            });
        }

        private async void UpdatePalette(BitmapInputStruct structBitmap)
        {
            await ColorPaletteUtility.ApplyAccentColor(_parentUI, structBitmap, string.Empty, false, true);
        }

        private unsafe IntPtr ArrayToPtr(byte[] buffer)
        {
            fixed (byte* bufferPtr = &buffer[0])
                return (IntPtr)bufferPtr;
        }

        public async ValueTask DimmAsync(CancellationToken token)
        {
            while (_isMediaPlayerLoading) await Task.Delay(250, token);

            if (!_isMediaPlayerDimm) await ToggleImageVisibility(true);
            _isMediaPlayerDimm = true;
        }

        public async ValueTask UndimmAsync(CancellationToken token)
        {
            while (_isMediaPlayerLoading) await Task.Delay(250, token);

            if (_isMediaPlayerDimm) await ToggleImageVisibility(false);
            _isMediaPlayerDimm = false;
        }

        private async ValueTask ToggleImageVisibility(bool hideImage, bool completeInvisible = false)
        {
            TimeSpan duration = TimeSpan.FromSeconds(hideImage ? BackgroundMediaUtility.TransitionDuration : BackgroundMediaUtility.TransitionDurationSlow);
            await Task.WhenAll(
                _currentMediaPlayerFrameParentGrid.StartAnimation(
                    duration,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0.2f : 1f, hideImage ? 1f : 0.2f)
                )
            );
        }

        public async ValueTask ShowAsync(CancellationToken token)
        {
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await _currentMediaPlayerFrameParentGrid!.StartAnimation(duration,
                _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", _isMediaPlayerDimm ? 0.2f : 1f, 0f)
                );

            App.ToggleBlurBackdrop(false);
        }

        public async ValueTask HideAsync(CancellationToken token)
        {
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await _currentMediaPlayerFrameParentGrid!.StartAnimation(duration,
                _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0f, (float)_currentMediaPlayerFrameParentGrid.Opacity)
                );

            if (_currentMediaStream != null) await _currentMediaStream.DisposeAsync();
            if (_currentMediaSource != null) _currentMediaSource.Dispose();
            if (_currentMediaPlayer != null) _currentMediaPlayer.Dispose();
            _currentStopwatch?.Stop();

            bool IsLastAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();
            App.ToggleBlurBackdrop(IsLastAcrylicEnabled);
        }

        public void WindowUnfocused()
        {
            _focusState?.Add(false);
        }

        public void WindowFocused()
        {
            _focusState?.Add(true);
        }

        public void Mute()
        {
            _currentMediaPlayer!.IsMuted = true;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            _currentMediaPlayer!.IsMuted = false;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        bool _isFocusChangeRunning = false;

        private async void VolumeWindowUnfocusedChangeWatcher(CancellationTokenSourceWrapper token)
        {
#if DEBUG
            Logger.LogWriteLine($"[MediaPlayerLoader] Window focus watcher is starting!", LogType.Debug, true);
#endif

            _isVolumeWindowUnfocusedChangeWatcherRunning = true;
            while (!token.IsDisposed && !token.IsCancelled)
            {
                while (_focusState?.Count > 0)
                {
                    while (_isFocusChangeRunning) await Task.Delay(100);
                    _isFocusChangeRunning = true;
                    if (_focusState[0])
                    {
                        double currentAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();
                        this.Play();
                        await InterpolateVolumeChange(0f, (float)currentAudioVolume, false);
                    }
                    else
                    {
                        double currentAudioVolume = _currentMediaPlayer!.Volume;
                        await InterpolateVolumeChange((float)currentAudioVolume, 0f, true);
                        this.Pause();
                    }
                    lock (_focusState)
                    {
                        _focusState.RemoveAt(0);
                        _isFocusChangeRunning = false;
                    }
                }
                await Task.Delay(100);
            }
            _isVolumeWindowUnfocusedChangeWatcherRunning = false;

#if DEBUG
            Logger.LogWriteLine($"[MediaPlayerLoader] Window focus watcher is closing!", LogType.Debug, true);
#endif
        }

        private async ValueTask InterpolateVolumeChange(float from, float to, bool isMute)
        {
            double tFrom = from;
            double tTo = to;

            double current = tFrom;
            double inc = isMute ? -0.05 : 0.05;

        Loops:
            current += inc;
            _currentMediaPlayer!.Volume = current;

            await Task.Delay(10);
            if (isMute && current > tTo - inc)
                goto Loops;
            if (!isMute && current < tTo - inc)
                goto Loops;

            _currentMediaPlayer!.Volume = tTo;
        }

        public void SetVolume(double value)
        {
            _currentMediaPlayer!.Volume = value;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioVolume", value);
        }

        public void Play() => _currentMediaPlayer?.Play();

        public void Pause() => _currentMediaPlayer?.Pause();
    }
}
