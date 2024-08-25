using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Animation;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
#if USEFFMPEGFORVIDEOBG
using FFmpegInteropX;
using Hi3Helper;
#endif
using Hi3Helper.Shared.Region;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.FileProperties;
using static Hi3Helper.Logger;

#if USEDYNAMICVIDEOPALETTE
using CollapseLauncher.Extension;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System.Diagnostics;
using Windows.Graphics.Imaging;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;
#endif

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal class MediaPlayerLoader : IBackgroundMediaLoader
    {
    #pragma warning disable CS0169 // Field is never used
        private bool _isFocusChangeRunning;
    #pragma warning restore CS0169 // Field is never used

        private FrameworkElement ParentUI          { get; }
        private Compositor       CurrentCompositor { get; }

        private MediaPlayerElement? CurrentMediaPlayerFrame           { get; }
        private Grid                CurrentMediaPlayerFrameParentGrid { get; }

        private Grid AcrylicMask     { get; }
        private Grid OverlayTitleBar { get; }
        
        public   bool                            IsBackgroundDimm { get; set; }
        private  FileStream?                     CurrentMediaStream     { get; set; }
        private  MediaPlayer?                    CurrentMediaPlayer     { get; set; }
        private  CancellationTokenSourceWrapper? InnerCancellationToken { get; set; }
#if USEFFMPEGFORVIDEOBG
        private  FFmpegMediaSource?              CurrentFFmpegMediaSource { get; set; }
#endif

#if USEDYNAMICVIDEOPALETTE
        private ImageUI?           CurrentMediaImage        { get; }
        private Stopwatch?         CurrentStopwatch         { get; set; }
        private SoftwareBitmap?    CurrentFrameBitmap       { get; set; }
        private CanvasImageSource? CurrentCanvasImageSource { get; set; }
        private CanvasDevice?      CanvasDevice             { get; set; }
        private CanvasBitmap?      CanvasBitmap             { get; set; }
#endif

        internal MediaPlayerLoader(
            FrameworkElement parentUI,
            Grid             acrylicMask,           Grid                overlayTitleBar,
            Grid             mediaPlayerParentGrid, MediaPlayerElement? mediaPlayerCurrent)
        {
            ParentUI          = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            AcrylicMask     = acrylicMask;
            OverlayTitleBar = overlayTitleBar;

            CurrentMediaPlayerFrameParentGrid = mediaPlayerParentGrid;
            CurrentMediaPlayerFrame           = mediaPlayerCurrent;

#if USEDYNAMICVIDEOPALETTE
            CurrentMediaImage = mediaPlayerParentGrid.AddElementToGridRowColumn(new ImageUI()
                                                                                   .WithHorizontalAlignment(HorizontalAlignment
                                                                                       .Center)
                                                                                   .WithVerticalAlignment(VerticalAlignment
                                                                                       .Center)
                                                                                   .WithStretch(Stretch
                                                                                       .UniformToFill));
#endif
        }

        ~MediaPlayerLoader() => Dispose();

        public void Dispose()
        {
#if USEDYNAMICVIDEOPALETTE
            CurrentStopwatch?.Stop();
            CanvasDevice?.Dispose();
            CurrentFrameBitmap?.Dispose();
#endif
            try
            {
                CurrentMediaPlayer?.Dispose();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error disposing CurrentMediaPlayer: {ex.Message}", LogType.Error, true);
            }

            try
            {
                InnerCancellationToken?.Dispose();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error disposing InnerCancellationToken: {ex.Message}", LogType.Error, true);
            }

            try
            {
                CurrentMediaStream?.Dispose();
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error disposing CurrentMediaStream: {ex.Message}", LogType.Error, true);
            }

            GC.SuppressFinalize(this);
        }

        public async Task LoadAsync(string filePath,      bool              isImageLoadForFirstTime,
                                    bool   isRequestInit, CancellationToken token)
        {
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

                InnerCancellationToken = new CancellationTokenSourceWrapper();

                if (CurrentMediaPlayer != null)
                {
#if USEDYNAMICVIDEOPALETTE
                    CurrentMediaPlayer.VideoFrameAvailable -= FrameGrabberEvent;
#endif
#if !USEDYNAMICVIDEOPALETTE
                    CurrentMediaPlayer.Dispose();
#endif
                }

                int canvasWidth  = (int)CurrentMediaPlayerFrameParentGrid.ActualWidth;
                int canvasHeight = (int)CurrentMediaPlayerFrameParentGrid.ActualHeight;

#if USEDYNAMICVIDEOPALETTE
                await BackgroundMediaUtility.AssignDefaultImage(CurrentMediaImage);

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
#endif

                await GetPreviewAsColorPalette(filePath);

                CurrentMediaStream = BackgroundMediaUtility.GetAlternativeFileStream() ?? File.Open(filePath, StreamUtility.FileStreamOpenReadOpt);

#if !USEFFMPEGFORVIDEOBG
                EnsureIfFormatIsDashOrUnsupported(CurrentMediaStream);
#endif
                CurrentMediaPlayer = new MediaPlayer();

                if (WindowUtility.IsCurrentWindowInFocus())
                {
                    CurrentMediaPlayer.AutoPlay = true;
                }

                bool   isAudioMute     = LauncherConfig.GetAppConfigValue("BackgroundAudioIsMute").ToBool();
                double lastAudioVolume = LauncherConfig.GetAppConfigValue("BackgroundAudioVolume").ToDouble();

                CurrentMediaPlayer.IsMuted          = isAudioMute;
                CurrentMediaPlayer.Volume           = lastAudioVolume;
                CurrentMediaPlayer.IsLoopingEnabled = true;

#if !USEFFMPEGFORVIDEOBG
                CurrentMediaPlayer.SetStreamSource(CurrentMediaStream.AsRandomAccessStream());
#else
                if (CurrentFFmpegMediaSource != null)
                {
                    CurrentFFmpegMediaSource.Dispose();
                    CurrentFFmpegMediaSource = null;
                }

                CurrentFFmpegMediaSource ??= await FFmpegMediaSource.CreateFromStreamAsync(CurrentMediaStream.AsRandomAccessStream());

                await CurrentFFmpegMediaSource.OpenWithMediaPlayerAsync(CurrentMediaPlayer);
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
                    CurrentFFmpegMediaSource.Duration.ToString("c"),                                // 0
                    CurrentFFmpegMediaSource.CurrentVideoStream?.CodecName ?? "No Video Stream",    // 1
                    CurrentFFmpegMediaSource.CurrentVideoStream?.Bitrate ?? 0,                      // 2
                    CurrentFFmpegMediaSource.CurrentVideoStream?.DecoderEngine                      // 3
                    == DecoderEngine.FFmpegD3D11HardwareDecoder ? "Hardware" : "Software",
                    CurrentFFmpegMediaSource.CurrentAudioStream?.CodecName ?? "No Audio Stream",    // 4
                    CurrentFFmpegMediaSource.CurrentAudioStream?.Bitrate ?? 0,                      // 5
                    CurrentFFmpegMediaSource.CurrentAudioStream?.Channels ?? 0,                     // 6
                    CurrentFFmpegMediaSource.CurrentAudioStream?.SampleRate ?? 0,                   // 7
                    CurrentFFmpegMediaSource.CurrentAudioStream?.BitsPerSample ?? 0,                // 8
                    CurrentFFmpegMediaSource.CurrentVideoStream?.PixelWidth ?? 0,                   // 9
                    CurrentFFmpegMediaSource.CurrentVideoStream?.PixelHeight ?? 0,                  // 10
                    CurrentFFmpegMediaSource.CurrentVideoStream?.BitsPerSample ?? 0,                // 11
                    CurrentFFmpegMediaSource.CurrentVideoStream?.DecoderEngine ?? 0                 // 12
                    ), LogType.Debug, true);
#endif

#if USEDYNAMICVIDEOPALETTE
                CurrentMediaPlayer.IsVideoFrameServerEnabled =  true;
                CurrentMediaPlayer.VideoFrameAvailable       += FrameGrabberEvent;
#endif

                CurrentMediaPlayerFrame?.SetMediaPlayer(CurrentMediaPlayer);
            }
            catch
            {
                CurrentMediaStream?.Dispose();
                throw;
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

#if !USEFFMPEGFORVIDEOBG
        private void EnsureIfFormatIsDashOrUnsupported(Stream stream)
        {
            ReadOnlySpan<byte> dashSignature = "ftypdash"u8;

            byte[] buffer = new byte[64];
            stream.ReadExactly(buffer);

            try
            {
                if (buffer.AsSpan(4).StartsWith(dashSignature))
                    throw new FormatException($"The video format is in \"MPEG-DASH\" format, which is unsupported.");
            }
            finally
            {
                stream.Position = 0;
            }
        }

#endif

        private async ValueTask GetPreviewAsColorPalette(string file)
        {
            StorageFile                storageFile = await GetFileAsStorageFile(file);
            using StorageItemThumbnail thumbnail   = await storageFile.GetThumbnailAsync(ThumbnailMode.VideosView);
            await using Stream         stream      = thumbnail.AsStream();

            await ColorPaletteUtility.ApplyAccentColor(ParentUI, stream.AsRandomAccessStream(), string.Empty, false,
                                                       true);
        }

        private async ValueTask<StorageFile> GetFileAsStorageFile(string filePath)
            => await StorageFile.GetFileFromPathAsync(filePath);

#if USEDYNAMICVIDEOPALETTE
        private void FrameGrabberEvent(MediaPlayer mediaPlayer, object args)
        {
            ParentUI?.DispatcherQueue?.TryEnqueue(() =>
            {
                int bitmapWidth  = CurrentFrameBitmap?.PixelWidth ?? 0;
                int bitmapHeight = CurrentFrameBitmap?.PixelHeight ?? 0;
                if (CanvasBitmap == null)
                {
                    CanvasBitmap =
                        CanvasBitmap.CreateFromSoftwareBitmap(CanvasDevice,
                                 CurrentFrameBitmap);
                }

                using CanvasDrawingSession? ds =
                    CurrentCanvasImageSource
                      ?.CreateDrawingSession(Color.FromArgb(255, 0, 0, 0));
                mediaPlayer.CopyFrameToVideoSurface(CanvasBitmap);
                ds?.DrawImage(CanvasBitmap);

                if (!(CurrentStopwatch?.ElapsedMilliseconds > 5000))
                {
                    return;
                }

                CurrentStopwatch.Restart();
                byte[] bufferBytes = CanvasBitmap.GetPixelBytes();
                IntPtr ptr         = ArrayToPtr(bufferBytes);

                if (bufferBytes.Length < bitmapWidth * bitmapHeight * 4) return;
                BitmapInputStruct bitmapInput = new BitmapInputStruct
                {
                    Buffer  = ptr,
                    Channel = 4,
                    Width   = bitmapWidth,
                    Height  = bitmapHeight
                };
                UpdatePalette(bitmapInput);
            });
        }

        private async void UpdatePalette(BitmapInputStruct bitmapInput)
        {
            await Task.Run(() =>
            {
                QuantizedColor color = ColorThief.GetColor(bitmapInput.Buffer,
                                                           bitmapInput.Channel, bitmapInput.Width,
                                                           bitmapInput.Height, 10);
                Color adjustedColor = Color
                                     .FromArgb(255, color.Color.R, color.Color.G, color.Color.B)
                                     .SetSaturation(1.5);
                adjustedColor = InnerLauncherConfig.IsAppThemeLight
                    ? adjustedColor.GetDarkColor()
                    : adjustedColor.GetLightColor();

                ParentUI?.DispatcherQueue?.TryEnqueue(() => ColorPaletteUtility.SetColorPalette(ParentUI, adjustedColor));
            });
        }

        private unsafe IntPtr ArrayToPtr(byte[] buffer)
        {
            fixed (byte* bufferPtr = &buffer[0])
            {
                return (IntPtr)bufferPtr;
            }
        }
#endif

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
            if (CurrentMediaPlayerFrameParentGrid.Opacity > 0f) return;

#if !USEDYNAMICVIDEOPALETTE
            App.ToggleBlurBackdrop(false);
#endif
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await CurrentMediaPlayerFrameParentGrid
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
#if !USEDYNAMICVIDEOPALETTE
            App.ToggleBlurBackdrop(isLastAcrylicEnabled);
#endif

            if (CurrentMediaPlayerFrameParentGrid.Opacity < 1f) return;
            TimeSpan duration = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            await CurrentMediaPlayerFrameParentGrid
               .StartAnimation(duration,
                               CurrentCompositor
                                  .CreateScalarKeyFrameAnimation("Opacity", 0f,
                                                                 (float)CurrentMediaPlayerFrameParentGrid
                                                                    .Opacity)
                              );

            if (CurrentMediaStream != null)
            {
                await CurrentMediaStream.DisposeAsync();
            }

            if (CurrentMediaPlayer != null)
            {
                CurrentMediaPlayer.Dispose();
            }

#if USEDYNAMICVIDEOPALETTE
                CurrentStopwatch?.Stop();
#endif
        }

        public void WindowUnfocused()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(WindowUnfocusedInner());
        }

        private async Task WindowUnfocusedInner()
        {
            double currentAudioVolume = CurrentMediaPlayer?.Volume ?? 0;
            await InterpolateVolumeChange((float)currentAudioVolume, 0f, true);
            Pause();
        }

        public void WindowFocused()
        {
            BackgroundMediaUtility.SharedActionBlockQueue?.Post(WindowFocusedInner());
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
            if (CurrentMediaPlayer == null) return;
            CurrentMediaPlayer.IsMuted = true;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            if (CurrentMediaPlayer == null) return;

            CurrentMediaPlayer.IsMuted = false;
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        private async ValueTask InterpolateVolumeChange(float from, float to, bool isMute)
        {
            if (CurrentMediaPlayer == null) return;

            double tFrom = from;
            double tTo   = to;

            double current = tFrom;
            double inc     = isMute ? -0.05 : 0.05;

            Loops:
            current                    += inc;
            CurrentMediaPlayer.Volume =  current;

            await Task.Delay(10);
            if (isMute && current > tTo - inc)
            {
                goto Loops;
            }

            if (!isMute && current < tTo - inc)
            {
                goto Loops;
            }

            CurrentMediaPlayer.Volume = tTo;
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