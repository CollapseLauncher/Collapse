using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
    [SuppressMessage("ReSharper", "IdentifierTypo")]
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal class StillImageLoader : IBackgroundMediaLoader
    {
        private FrameworkElement ParentUI            { get; }
        private Compositor       CurrentCompositor   { get; }
        private ImageUI?         ImageBackCurrent    { get; }
        private ImageUI?         ImageBackLast       { get; }
        private Grid?            ImageBackParentGrid { get; }
        
        public  bool                    IsBackgroundDimm { get; set; }
        private Grid                    AcrylicMask      { get; }
        private Grid                    OverlayTitleBar  { get; }
        private ActionBlock<ValueTask>? ActionTaskQueue  { get; set; }

        private  double AnimationDuration { get; }

        internal StillImageLoader(
            FrameworkElement parentUI,
            Grid             acrylicMask, Grid overlayTitleBar,
            Grid             imageBackParentGrid,
            ImageUI?         imageBackCurrent, ImageUI? imageBackLast,
            double           animationDuration = BackgroundMediaUtility.TransitionDuration)
        {
            ParentUI          = parentUI;
            CurrentCompositor = parentUI.GetElementCompositor();

            AcrylicMask     = acrylicMask;
            OverlayTitleBar = overlayTitleBar;

            ImageBackCurrent    = imageBackCurrent;
            ImageBackLast       = imageBackLast;
            ImageBackParentGrid = imageBackParentGrid;

            AnimationDuration = animationDuration;
            ActionTaskQueue   = new ActionBlock<ValueTask>(async (action) => {
                await action.ConfigureAwait(false);
            },
                new ExecutionDataflowBlockOptions
                {
                    EnsureOrdered = true,
                    MaxMessagesPerTask = 1,
                    MaxDegreeOfParallelism = 1,
                    TaskScheduler = TaskScheduler.Default
                });
        }

        ~StillImageLoader() => Dispose();

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async ValueTask LoadAsync(string            filePath, bool isImageLoadForFirstTime, bool isRequestInit,
                                         CancellationToken token)
        {
            try
            {
                // Get the image stream
                token.ThrowIfCancellationRequested();
                await using FileStream? imageStream = BackgroundMediaUtility.GetAlternativeFileStream() ??
                                                      await ImageLoaderHelper.LoadImage(filePath, false,
                                                               isImageLoadForFirstTime);
                // Return if the stream is null due to cancellation or an error.
                if (imageStream == null)
                {
                    return;
                }

                BitmapImage bitmapImage =
                    await ImageLoaderHelper.Stream2BitmapImage(imageStream.AsRandomAccessStream());

                await Task.WhenAll(
                                   ColorPaletteUtility.ApplyAccentColor(ParentUI,
                                                                        imageStream.AsRandomAccessStream(),
                                                                        filePath,
                                                                        isImageLoadForFirstTime, false),
                                   ApplyAndSwitchImage(AnimationDuration, bitmapImage)
                                  );
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private async Task ApplyAndSwitchImage(double duration, BitmapImage imageToApply)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(duration);

            ImageBackLast!.Source = ImageBackCurrent!.Source;

            ImageBackCurrent!.Opacity = 0;

            ImageBackLast!.Opacity   = 1;
            ImageBackCurrent!.Source = imageToApply;

            await Task.WhenAll(
                               ImageBackCurrent.StartAnimation(timeSpan,
                                                               CurrentCompositor
                                                                  .CreateScalarKeyFrameAnimation("Opacity", 1, 0)),
                               ImageBackLast.StartAnimation(timeSpan,
                                                            CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity",
                                                                0, 1, timeSpan * 0.8))
                              );
        }

        public void Dimm(CancellationToken token)
        {
            ActionTaskQueue?.Post(ToggleImageVisibility(true));
        }

        public void Undimm(CancellationToken token)
        {
            ActionTaskQueue?.Post(ToggleImageVisibility(false));
        }

        private async ValueTask ToggleImageVisibility(bool hideImage, bool completeInvisible = false)
        {
            if (IsBackgroundDimm == hideImage) return;
            IsBackgroundDimm = hideImage;

            TimeSpan duration = TimeSpan.FromSeconds(hideImage
                                                         ? BackgroundMediaUtility.TransitionDuration
                                                         : BackgroundMediaUtility.TransitionDurationSlow);

            float fromScale = 1f;
            Vector3 fromTranslate =
                new Vector3(-((float)(ImageBackParentGrid?.ActualWidth ?? 0) * (fromScale - 1f) / 2),
                    -((float)(ImageBackParentGrid?.ActualHeight ?? 0) * (fromScale - 1f) / 2), 0);
            float toScale = 1.07f;
            Vector3 toTranslate = new Vector3(-((float)(ImageBackParentGrid?.ActualWidth ?? 0) * (toScale - 1f) / 2),
                -((float)(ImageBackParentGrid?.ActualHeight ?? 0) * (toScale - 1f) / 2), 0);

            if (completeInvisible)
            {
                await Task.WhenAll(
                    ImageBackParentGrid.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? completeInvisible ? 0f : 0.4f : 1f, hideImage ? 1f : 0f)
                    )
                );
            }
            else
            {
                await Task.WhenAll(
                    AcrylicMask.StartAnimation(
                        duration,
                        CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 1f : 0f, hideImage ? 0f : 1f)
                        ),
                    OverlayTitleBar.StartAnimation(
                         duration,
                         CurrentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0f : 1f, hideImage ? 1f : 0f)
                        ),
                    ImageBackParentGrid.StartAnimation(
                        duration,
                        CurrentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(hideImage ? toScale : fromScale), new Vector3(!hideImage ? toScale : fromScale)),
                        CurrentCompositor.CreateVector3KeyFrameAnimation("Translation", hideImage ? toTranslate : fromTranslate, !hideImage ? toTranslate : fromTranslate)
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