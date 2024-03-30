using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
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
using ImageUI = Microsoft.UI.Xaml.Controls.Image;

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal class MediaPlayerLoader : IBackgroundMediaLoader
    {
        private FrameworkElement ParentUI { get; }
        private Compositor CurrentCompositor { get; }

        private MediaPlayerElement? CurrentMediaPlayerFrame { get; }
        private ImageUI? CurrentMediaImage { get; }
        private Grid CurrentMediaPlayerFrameParentGrid { get; }

        private bool IsMediaPlayerLoading { get; set; }
        internal bool IsMediaPlayerDimm { get; set; }
        private FileStream? CurrentMediaStream { get; set; }
        private MediaPlayer? CurrentMediaPlayer { get; set; }
        private Stopwatch? CurrentStopwatch { get; set; }
        private CancellationTokenSourceWrapper? InnerCancellationToken { get; set; }
        private List<bool>? FocusState { get; set; }

        private SoftwareBitmap? CurrentFrameBitmap { get; set; }
        private CanvasImageSource? CurrentCanvasImageSource { get; set; }
        private CanvasDevice? CanvasDevice { get; set; }
        private CanvasBitmap? CanvasBitmap { get; set; }

        internal MediaPlayerLoader(
            FrameworkElement parentUI,
            Grid mediaPlayerParentGrid, MediaPlayerElement? mediaPlayerCurrent)
        {
            ParentUI = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            CurrentMediaPlayerFrameParentGrid = mediaPlayerParentGrid;
            CurrentMediaPlayerFrame = mediaPlayerCurrent;
            CurrentMediaImage = mediaPlayerParentGrid.AddElementToGridRowColumn(new ImageUI()
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithStretch(Stretch.UniformToFill));

            IsMediaPlayerLoading = false;
        }

        public async ValueTask LoadAsync(string filePath, bool isImageLoadForFirstTime, bool isRequestInit,
            CancellationToken token)
        {
            // Wait until the image loading sequence is completed
            while (IsMediaPlayerLoading)
            {
                await Task.Delay(250, token);
            }

            if (CurrentMediaStream != null)
            {
                await CurrentMediaStream.DisposeAsync();
            }

            if (CurrentMediaPlayer != null)
            {
                CurrentMediaPlayer.Dispose();
            }

            try
            {
                if (InnerCancellationToken != null)
                {
                    await InnerCancellationToken.CancelAsync();
                    InnerCancellationToken.Dispose();
                }

                FocusState ??= new List<bool>();

                if (FocusState.Count > 0)
                {
                    FocusState.Clear();
                }

                InnerCancellationToken = new CancellationTokenSourceWrapper();

                if (CurrentMediaPlayer != null)
                {
                    CurrentMediaPlayer.VideoFrameAvailable -= FrameGrabberEvent;
                    CurrentMediaPlayer.Dispose();
                }

                await BackgroundMediaUtility.AssignDefaultImage(CurrentMediaImage);

                int canvasWidth = (int)CurrentMediaPlayerFrameParentGrid.ActualWidth;
                int canvasHeight = (int)CurrentMediaPlayerFrameParentGrid.ActualHeight;

                CanvasDevice = CanvasDevice.GetSharedDevice();
                if (CurrentFrameBitmap == null)
                {
                    // FrameServerImage in this example is a XAML image control
                    CurrentFrameBitmap = new SoftwareBitmap(BitmapPixelFormat.Rgba8, canvasWidth, canvasHeight,
                        BitmapAlphaMode.Ignore);
                }

                if (CurrentCanvasImageSource == null)
                {
                    CurrentCanvasImageSource =
                        new CanvasImageSource(CanvasDevice, canvasWidth, canvasHeight, 96); //96); 
                }

                CurrentMediaImage!.Source = CurrentCanvasImageSource;

                CurrentStopwatch = Stopwatch.StartNew();

                await GetPreviewAsColorPalette(filePath);

                CurrentMediaStream = File.OpenRead(filePath);
                CurrentMediaPlayer = new MediaPlayer();

                if (InnerLauncherConfig.IsWindowCurrentlyFocused())
                {
                    CurrentMediaPlayer.AutoPlay = true;
                }

                bool isAudioMute = LauncherConfig.GetAppConfigValue("BackgroundAudioIsMute").ToBool();
                double lastAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();

                CurrentMediaPlayer.IsMuted = isAudioMute;
                CurrentMediaPlayer.Volume = lastAudioVolume;
                CurrentMediaPlayer.IsLoopingEnabled = true;
                CurrentMediaPlayer.SetStreamSource(CurrentMediaStream.AsRandomAccessStream());
                // _currentMediaPlayer.IsVideoFrameServerEnabled = true;
                CurrentMediaPlayer.VideoFrameAvailable += FrameGrabberEvent;

                CurrentMediaPlayerFrame!.SetMediaPlayer(CurrentMediaPlayer);
                VolumeWindowUnfocusedChangeWatcher(InnerCancellationToken);
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                IsMediaPlayerLoading = false;
            }
        }

        private async ValueTask GetPreviewAsColorPalette(string file)
        {
            StorageFile storageFile = await StorageFile.GetFileFromPathAsync(file);
            using StorageItemThumbnail thumbnail = await storageFile.GetThumbnailAsync(ThumbnailMode.VideosView);
            await using Stream stream = thumbnail.AsStream();

            await ColorPaletteUtility.ApplyAccentColor(ParentUI, stream.AsRandomAccessStream(), string.Empty, false,
                true);
        }

        private async void FrameGrabberEvent(MediaPlayer mediaPlayer, object args)
        {
            await ParentUI.DispatcherQueue.EnqueueAsync(() =>
            {
                int bitmapWidth = CurrentFrameBitmap?.PixelWidth ?? 0;
                int bitmapHeight = CurrentFrameBitmap?.PixelHeight ?? 0;
                if (CanvasBitmap == null)
                {
                    CanvasBitmap = CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice, CurrentFrameBitmap);
                }

                using CanvasDrawingSession? ds =
                    CurrentCanvasImageSource?.CreateDrawingSession(Color.FromArgb(255, 0, 0, 0));
                mediaPlayer.CopyFrameToVideoSurface(CanvasBitmap);
                ds?.DrawImage(CanvasBitmap);

                if (!(CurrentStopwatch?.ElapsedMilliseconds > 2000)) return;

                CurrentStopwatch.Restart();
                byte[] bufferBytes = CanvasBitmap.GetPixelBytes();
                IntPtr ptr = ArrayToPtr(bufferBytes);

                if (bufferBytes.Length >= bitmapWidth * bitmapHeight * 4)
                {
                    UpdatePalette(new BitmapInputStruct
                    {
                        Buffer = ptr,
                        Channel = 4,
                        Width = bitmapWidth,
                        Height = bitmapHeight
                    });
                }
            });
        }

        private async void UpdatePalette(BitmapInputStruct structBitmap)
        {
            await ColorPaletteUtility.ApplyAccentColor(ParentUI, structBitmap, string.Empty, false, true);
        }

        private unsafe IntPtr ArrayToPtr(byte[] buffer)
        {
            fixed (byte* bufferPtr = &buffer[0])
            {
                return (IntPtr)bufferPtr;
            }
        }

        public async ValueTask DimmAsync(CancellationToken token)
        {
            while (IsMediaPlayerLoading)
            {
                await Task.Delay(250, token);
            }

            if (!IsMediaPlayerDimm)
            {
                await ToggleImageVisibility(true);
            }

            IsMediaPlayerDimm = true;
        }

        public async ValueTask UndimmAsync(CancellationToken token)
        {
            while (IsMediaPlayerLoading)
            {
                await Task.Delay(250, token);
            }

            if (IsMediaPlayerDimm)
            {
                await ToggleImageVisibility(false);
            }

            IsMediaPlayerDimm = false;
        }

        private async ValueTask ToggleImageVisibility(bool hideImage)
        {
            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                ? BackgroundMediaUtility.TransitionDuration
                : BackgroundMediaUtility.TransitionDurationSlow);
            await Task.WhenAll(
                CurrentMediaPlayerFrameParentGrid.StartAnimation(
                    duration,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0.2f : 1f,
                        hideImage ? 1f : 0.2f)
                )
            );
        }

        public async ValueTask ShowAsync(CancellationToken token)
        {
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await CurrentMediaPlayerFrameParentGrid.StartAnimation(duration,
                CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", IsMediaPlayerDimm ? 0.2f : 1f, 0f)
            );

            App.ToggleBlurBackdrop(false);
        }

        public async ValueTask HideAsync(CancellationToken token)
        {
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await CurrentMediaPlayerFrameParentGrid.StartAnimation(duration,
                CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0f,
                    (float)CurrentMediaPlayerFrameParentGrid.Opacity)
            );

            if (CurrentMediaStream != null)
            {
                await CurrentMediaStream.DisposeAsync();
            }

            if (CurrentMediaPlayer != null)
            {
                CurrentMediaPlayer.Dispose();
            }

            CurrentStopwatch?.Stop();

            bool isLastAcrylicEnabled = LauncherConfig.GetAppConfigValue("EnableAcrylicEffect").ToBool();
            App.ToggleBlurBackdrop(isLastAcrylicEnabled);
        }

        public void WindowUnfocused()
        {
            FocusState?.Add(false);
        }

        public void WindowFocused()
        {
            FocusState?.Add(true);
        }

        public void Mute()
        {
            CurrentMediaPlayer!.IsMuted = true;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            CurrentMediaPlayer!.IsMuted = false;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        private bool _isFocusChangeRunning;

        private async void VolumeWindowUnfocusedChangeWatcher(CancellationTokenSourceWrapper token)
        {
#if DEBUG
            Logger.LogWriteLine($"[MediaPlayerLoader] Window focus watcher is starting!", LogType.Debug, true);
#endif

            while (token is { IsDisposed: false, IsCancelled: false })
            {
                while (FocusState?.Count > 0)
                {
                    while (_isFocusChangeRunning)
                    {
                        await Task.Delay(100);
                    }

                    _isFocusChangeRunning = true;
                    if (FocusState[0])
                    {
                        double currentAudioVolume =
                            LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();
                        Play();
                        await InterpolateVolumeChange(0f, (float)currentAudioVolume, false);
                    }
                    else
                    {
                        double currentAudioVolume = CurrentMediaPlayer!.Volume;
                        await InterpolateVolumeChange((float)currentAudioVolume, 0f, true);
                        Pause();
                    }

                    lock (FocusState)
                    {
                        FocusState.RemoveAt(0);
                        _isFocusChangeRunning = false;
                    }
                }

                await Task.Delay(100);
            }

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
            CurrentMediaPlayer!.Volume = current;

            await Task.Delay(10);
            if (isMute && current > tTo - inc)
            {
                goto Loops;
            }

            if (!isMute && current < tTo - inc)
            {
                goto Loops;
            }

            CurrentMediaPlayer!.Volume = tTo;
        }

        public void SetVolume(double value)
        {
            CurrentMediaPlayer!.Volume = value;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioVolume", value);
        }

        public void Play()
        {
            CurrentMediaPlayer?.Play();
        }

        public void Pause()
        {
            CurrentMediaPlayer?.Pause();
        }
    }
}