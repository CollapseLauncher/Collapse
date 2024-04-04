using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using ImageUI = Microsoft.UI.Xaml.Controls.Image;

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal class StillImageLoader : IBackgroundMediaLoader
    {
        private FrameworkElement ParentUI { get; }
        private Compositor CurrentCompositor { get; }
        private ImageUI? ImageBackCurrent { get; }
        private ImageUI? ImageBackLast { get; }
        private Grid? ImageBackParentGrid { get; }

        private Grid AcrylicMask { get; }
        private Grid OverlayTitleBar { get; }

        private bool IsImageLoading { get; set; }
        internal bool IsImageDimm { get; set; }
        private double AnimationDuration { get; }

        internal StillImageLoader(
            FrameworkElement parentUI,
            Grid acrylicMask, Grid overlayTitleBar,
            Grid imageBackParentGrid,
            ImageUI? imageBackCurrent, ImageUI? imageBackLast,
            double animationDuration = BackgroundMediaUtility.TransitionDuration)
        {
            ParentUI = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            AcrylicMask = acrylicMask;
            OverlayTitleBar = overlayTitleBar;

            ImageBackCurrent = imageBackCurrent;
            ImageBackLast = imageBackLast;
            ImageBackParentGrid = imageBackParentGrid;

            AnimationDuration = animationDuration;
            IsImageLoading = false;
        }

        public async ValueTask LoadAsync(string filePath, bool isImageLoadForFirstTime, bool isRequestInit,
            CancellationToken token)
        {
            // Wait until the image loading sequence is completed
            while (IsImageLoading)
            {
                await Task.Delay(250, token);
            }

            try
            {
                // Set the image loading state
                IsImageLoading = true;

                // Get the image stream
                token.ThrowIfCancellationRequested();
                await using (FileStream? imageStream = BackgroundMediaUtility.GetAlternativeFileStream() ??
                              await ImageLoaderHelper.LoadImage(filePath, false, isImageLoadForFirstTime))
                {
                    // Return if the stream is null due to cancellation or an error.
                    if (imageStream == null)
                    {
                        return;
                    }

                    BitmapImage bitmapImage =
                        await ImageLoaderHelper.Stream2BitmapImage(imageStream.AsRandomAccessStream());

                    await Task.WhenAll(
                        ColorPaletteUtility.ApplyAccentColor(ParentUI, imageStream.AsRandomAccessStream(), filePath,
                            isImageLoadForFirstTime, false),
                        ApplyAndSwitchImage(AnimationDuration, bitmapImage)
                    );
                }
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                IsImageLoading = false;
            }
        }

        private async Task ApplyAndSwitchImage(double duration, BitmapImage imageToApply)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(duration);

            ImageBackLast!.Source = ImageBackCurrent!.Source;

            ImageBackCurrent!.Opacity = 0;

            ImageBackLast!.Opacity = 1;
            ImageBackCurrent!.Source = imageToApply;

            await Task.WhenAll(
                ImageBackCurrent.StartAnimation(timeSpan,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0)),
                ImageBackLast.StartAnimation(timeSpan,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1, timeSpan * 0.8))
            );
        }

        public async ValueTask DimmAsync(CancellationToken token)
        {
            while (IsImageLoading)
            {
                await Task.Delay(250, token);
            }

            if (!IsImageDimm)
            {
                await ToggleImageVisibility(true);
            }

            IsImageDimm = true;
        }

        public async ValueTask UndimmAsync(CancellationToken token)
        {
            while (IsImageLoading)
            {
                await Task.Delay(250, token);
            }

            if (IsImageDimm)
            {
                await ToggleImageVisibility(false);
            }

            IsImageDimm = false;
        }

        private async ValueTask ToggleImageVisibility(bool hideImage, bool completeInvisible = false)
        {
            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                ? BackgroundMediaUtility.TransitionDuration
                : BackgroundMediaUtility.TransitionDurationSlow);

            if (completeInvisible)
            {
                await Task.WhenAll(
                    ImageBackParentGrid.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                            hideImage ? completeInvisible ? 0f : 0.4f : 1f, hideImage ? 1f : 0f)
                    )
                );
            }
            else
            {
                await Task.WhenAll(
                    AcrylicMask.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                            hideImage ? 1f : 0f, hideImage ? 0f : 1f)
                    ),
                    OverlayTitleBar.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                            hideImage ? 0f : 1f, hideImage ? 1f : 0f)
                    )
                );
            }
        }

        public async ValueTask ShowAsync(CancellationToken token)
        {
            await ToggleImageVisibility(false, true);
        }

        public async ValueTask HideAsync(CancellationToken token)
        {
            await ToggleImageVisibility(true, true);
        }

        public void Mute()
        {
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", true);
        }

        public void Unmute()
        {
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioIsMute", false);
        }

        public void SetVolume(double value)
        {
            LauncherConfig.SetAndSaveConfigValue("BackgroundAudioVolume", value);
        }

        public void WindowFocused()
        {
        }

        public void WindowUnfocused()
        {
        }

        public void Play()
        {
        }

        public void Pause()
        {
        }
    }
}
