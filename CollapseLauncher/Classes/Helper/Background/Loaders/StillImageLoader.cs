using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
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
        private ImageUI? ImageFrontCurrent { get; }
        private ImageUI? ImageFrontLast { get; }
        private Grid? ImageFrontParentGrid { get; }
        private ImageUI? ImageBackCurrent { get; }
        private ImageUI? ImageBackLast { get; }
        private Grid? ImageBackParentGrid { get; }

        private bool IsImageLoading { get; set; }
        internal bool IsImageDimm { get; set; }
        private double AnimationDuration { get; }

        internal StillImageLoader(
            FrameworkElement parentUI,
            Grid imageFrontParentGrid, Grid imageBackParentGrid,
            ImageUI? imageFrontCurrent, ImageUI? imageFrontLast,
            ImageUI? imageBackCurrent, ImageUI? imageBackLast,
            double animationDuration = BackgroundMediaUtility.TransitionDuration)
        {
            ParentUI = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            ImageFrontCurrent = imageFrontCurrent;
            ImageFrontLast = imageFrontLast;
            ImageFrontParentGrid = imageFrontParentGrid;

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
                await using (FileStream? imageStream =
                             await ImageLoaderHelper.LoadImage(filePath, isRequestInit, isImageLoadForFirstTime))
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
            bool isNeedToggleFront = InnerLauncherConfig.m_appMode != InnerLauncherConfig.AppMode.Hi3CacheUpdater;

            TimeSpan timeSpan = TimeSpan.FromSeconds(duration);

            ImageBackLast!.Source = ImageBackCurrent!.Source;
            ImageFrontLast!.Source = ImageFrontCurrent!.Source;

            ImageBackCurrent!.Opacity = 0;
            ImageFrontCurrent!.Opacity = 0;

            ImageBackLast!.Opacity = 1;
            ImageFrontLast!.Opacity = 1;
            ImageBackCurrent!.Source = imageToApply;
            ImageFrontCurrent!.Source = imageToApply;

            await Task.WhenAll(
                ImageBackCurrent.StartAnimation(timeSpan,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0)),
                ImageBackLast.StartAnimation(timeSpan,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1, timeSpan * 0.8)),
                isNeedToggleFront
                    ? ImageFrontCurrent.StartAnimation(timeSpan,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0))
                    : Task.CompletedTask,
                ImageFrontLast.StartAnimation(timeSpan,
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

        private async ValueTask ToggleImageVisibility(bool hideImage, bool completeInvisible = false,
            bool forceHideFront = false)
        {
            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                ? BackgroundMediaUtility.TransitionDuration
                : BackgroundMediaUtility.TransitionDurationSlow);
            TimeSpan durationSlow = TimeSpan.FromSeconds(BackgroundMediaUtility.TransitionDuration);

            float fromScale = !hideImage ? 0.95f : 1f;
            Vector3 fromTranslate =
                new Vector3(-((float)(ImageFrontParentGrid?.ActualWidth ?? 0) * (fromScale - 1f) / 2),
                    -((float)(ImageFrontParentGrid?.ActualHeight ?? 0) * (fromScale - 1f) / 2), 0);
            float toScale = hideImage ? 1.1f : 1f;
            Vector3 toTranslate = new Vector3(-((float)(ImageFrontParentGrid?.ActualWidth ?? 0) * (toScale - 1f) / 2),
                -((float)(ImageFrontParentGrid?.ActualHeight ?? 0) * (toScale - 1f) / 2), 0);

            await Task.WhenAll(
                ImageFrontParentGrid.StartAnimation(
                    duration,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                        hideImage ? 0f : forceHideFront ? 0f : 1f, hideImage ? forceHideFront ? 0f : 1f : 0f),
                    CurrentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale),
                        new Vector3(fromScale)),
                    CurrentCompositor.CreateVector3KeyFrameAnimation("Translation", toTranslate, fromTranslate)
                ),
                ImageBackParentGrid.StartAnimation(
                    durationSlow,
                    CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                        hideImage ? completeInvisible ? 0f : 0.4f : 1f, hideImage ? 1f : completeInvisible ? 0f : 0.4f)
                )
            );
        }

        public async ValueTask ShowAsync(CancellationToken token)
        {
            await ToggleImageVisibility(false, true, IsImageDimm);
        }

        public async ValueTask HideAsync(CancellationToken token)
        {
            await ToggleImageVisibility(true, true, IsImageDimm);
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