using CollapseLauncher.Helper.Animation;
using CollapseLauncher.Helper.Image;
using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using ImageUI = Microsoft.UI.Xaml.Controls.Image;

#nullable enable
namespace CollapseLauncher.Helper.Background.Loaders
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    internal class StillImageLoader : IBackgroundImageLoader
    {
        private FrameworkElement _parentUI { get; init; }
        private Compositor _currentCompositor { get; init; }
        private ImageUI? _imageFrontCurrent { get; init; }
        private ImageUI? _imageFrontLast { get; init; }
        private Grid? _imageFrontParentGrid { get; init; }
        private ImageUI? _imageBackCurrent { get; init; }
        private ImageUI? _imageBackLast { get; init; }
        private Grid? _imageBackParentGrid { get; init; }

        private bool _isImageLoading { get; set; }
        private bool _isImageDimm { get; set; }
        private double _animationDuration { get; set; }

        internal StillImageLoader(
            FrameworkElement parentUI,
            Grid imageFrontParentGrid, Grid imageBackParentGrid,
            ImageUI? imageFrontCurrent, ImageUI? imageFrontLast,
            ImageUI? imageBackCurrent, ImageUI? imageBackLast,
            double animationDuration = BackgroundImageUtility.TransitionDuration)
        {
            _parentUI = parentUI;
            _currentCompositor = parentUI.GetElementCompositor();

            _imageFrontCurrent = imageFrontCurrent;
            _imageFrontLast = imageFrontLast;
            _imageFrontParentGrid = imageFrontParentGrid;

            _imageBackCurrent = imageBackCurrent;
            _imageBackLast = imageBackLast;
            _imageBackParentGrid = imageBackParentGrid;

            _animationDuration = animationDuration;
            _isImageLoading = false;
        }

        public async ValueTask LoadAsync(string filePath, bool isImageLoadForFirstTime, bool isRequestInit, CancellationToken token)
        {
            // Wait until the image loading sequence is completed
            while (_isImageLoading) await Task.Delay(250, token);

            try
            {
                // Set the image loading state
                _isImageLoading = true;

                // Get the image stream
                token.ThrowIfCancellationRequested();
                await using (FileStream? imageStream = await ImageLoaderHelper.LoadImage(filePath, isRequestInit, isImageLoadForFirstTime))
                {
                    // Return if the stream is null due to cancellation or an error.
                    if (imageStream == null) return;

                    BitmapImage bitmapImage = await ImageLoaderHelper.Stream2BitmapImage(imageStream.AsRandomAccessStream());
                    Bitmap bitmapAccentColor = await Task.Run(() => ImageLoaderHelper.Stream2Bitmap(imageStream.AsRandomAccessStream()));

                    await Task.WhenAll(
                        ColorPaletteUtility.ApplyAccentColor(_parentUI, bitmapAccentColor, filePath, isImageLoadForFirstTime),
                        ApplyAndSwitchImage(_animationDuration, bitmapImage)
                        );
                }
            }
            finally
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                _isImageLoading = false;
            }
        }

        private async Task ApplyAndSwitchImage(double duration, BitmapImage imageToApply)
        {
            bool IsNeedToggleFront = InnerLauncherConfig.m_appMode != InnerLauncherConfig.AppMode.Hi3CacheUpdater;

            TimeSpan timeSpan = TimeSpan.FromSeconds(duration);

            _imageBackLast!.Source = _imageBackCurrent!.Source;
            _imageFrontLast!.Source = _imageFrontCurrent!.Source;

            _imageBackCurrent!.Opacity = 0;
            _imageFrontCurrent!.Opacity = 0;

            _imageBackLast!.Opacity = 1;
            _imageFrontLast!.Opacity = 1;
            _imageBackCurrent!.Source = imageToApply;
            _imageFrontCurrent!.Source = imageToApply;

            await Task.WhenAll(
                _imageBackCurrent.StartAnimation(timeSpan,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0)),
                _imageBackLast.StartAnimation(timeSpan,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1, timeSpan * 0.8)),
                IsNeedToggleFront ? _imageFrontCurrent.StartAnimation(timeSpan,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 1, 0)) : Task.CompletedTask,
                _imageFrontLast.StartAnimation(timeSpan,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", 0, 1, timeSpan * 0.8))
                );
        }

        public async ValueTask DimmAsync(CancellationToken token)
        {
            while (_isImageLoading) await Task.Delay(250, token);

            if (!_isImageDimm) await ToggleImageVisibility(true);
            _isImageDimm = true;
        }

        public async ValueTask UndimmAsync(CancellationToken token)
        {
            while (_isImageLoading) await Task.Delay(250, token);

            if (_isImageDimm) await ToggleImageVisibility(false);
            _isImageDimm = false;
        }

        private async ValueTask ToggleImageVisibility(bool hideImage, bool completeInvisible = false)
        {
            TimeSpan duration = TimeSpan.FromSeconds(hideImage ? BackgroundImageUtility.TransitionDuration : BackgroundImageUtility.TransitionDurationSlow);
            TimeSpan durationSlow = TimeSpan.FromSeconds(BackgroundImageUtility.TransitionDuration);

            float fromScale = !hideImage ? 0.95f : 1f;
            Vector3 fromTranslate = new Vector3(-((float)(_imageFrontParentGrid?.ActualWidth ?? 0) * (fromScale - 1f) / 2), -((float)(_imageFrontParentGrid?.ActualHeight ?? 0) * (fromScale - 1f) / 2), 0);
            float toScale = hideImage ? 1.1f : 1f;
            Vector3 toTranslate = new Vector3(-((float)(_imageFrontParentGrid?.ActualWidth ?? 0) * (toScale - 1f) / 2), -((float)(_imageFrontParentGrid?.ActualHeight ?? 0) * (toScale - 1f) / 2), 0);

            await Task.WhenAll(
                _imageFrontParentGrid.StartAnimation(
                    duration,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? 0f : 1f, hideImage ? 1f : 0f),
                    _currentCompositor.CreateVector3KeyFrameAnimation("Scale", new Vector3(toScale), new Vector3(fromScale)),
                    _currentCompositor.CreateVector3KeyFrameAnimation("Translation", toTranslate, fromTranslate)
                ),
                _imageBackParentGrid.StartAnimation(
                    durationSlow,
                    _currentCompositor.CreateScalarKeyFrameAnimation("Opacity", hideImage ? (completeInvisible ? 0f : 0.4f) : 1f, hideImage ? 1f : (completeInvisible ? 0f : 0.4f))
                )
            );
        }

        public async ValueTask ShowAsync(CancellationToken token) => await ToggleImageVisibility(false, true);

        public async ValueTask HideAsync(CancellationToken token) => await ToggleImageVisibility(true, true);
    }
}
