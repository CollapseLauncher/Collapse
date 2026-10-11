using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// ReSharper disable AccessToModifiedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private async void LoadAllPresenters()
    {
        Task placeholderLoadTask = LoadPresenter(_placeholderGrid, PlaceholderSource);
        Task foregroundLoadTask  = LoadPresenter(_foregroundGrid,  ForegroundSource);
        Task backgroundLoadTask  = LoadPresenter(_backgroundGrid,  BackgroundSource);

        await Task.WhenAll(placeholderLoadTask,
                           foregroundLoadTask,
                           backgroundLoadTask);
    }

    private async Task LoadPresenter(
        Panel   parent,
        object? source)
    {
        if (source == null)
            return;

        FrameworkElement? currentPresenter  = null;
        SourceContext sourceContext = new()
        {
            Source = source
        };

        try
        {
            SourceGuessResult sourceGuessResult = LayeredBackgroundImageExtensions.GuessSourceType(source);
            if (sourceGuessResult.ObjectType == SourceObjectType.Unknown ||
                sourceGuessResult.FormatType == SourceFormatType.Unknown)
            {
                // Can't read. Return and clear the media instead.
                return;
            }

            sourceContext.IsDisposeSource = sourceGuessResult.ObjectType
                is SourceObjectType.URL
                or SourceObjectType.IRandomAccessStream // Reason? It's just a wrapper. It won't dispose the managed Stream underneath.
                or SourceObjectType.ImageSource
                or SourceObjectType.MediaSource;

            if (sourceGuessResult.FormatType == SourceFormatType.Image)
            {
                currentPresenter = await AddImagePresenterAsync(parent,
                                                                source,
                                                                ImagePresenterOnLoaded);

                void ImagePresenterOnLoaded(object sender, RoutedEventArgs e)
                {
                    if (sender is not Image asImage)
                    {
                        return;
                    }

                    asImage.ImageOpened -= ImagePresenterOnLoaded;
                    UnloadLastPresenter(parent, asImage);
                }
            }

            if (sourceGuessResult.FormatType == SourceFormatType.Video)
            {
                currentPresenter = await AddVideoPresenterAsync(parent,
                                                                source,
                                                                sourceContext,
                                                                VideoPresenterOnLoaded);

                void VideoPresenterOnLoaded(object sender, RoutedEventArgs e)
                {
                    if (sender is not MediaFoundationPresenter presenter)
                    {
                        return;
                    }

                    UnloadLastPresenter(parent, presenter);
                }
            }
        }
        finally
        {
            // Clear the parent if no presenter is available
            if (currentPresenter is null)
            {
                UnloadLastPresenter(parent);
            }
        }
    }

    private static async void UnloadLastPresenter(
        Panel             parent,
        FrameworkElement? currentPresenter = null)
    {
        UIElement? lastMostFrontElement = parent.Children.LastOrDefault(x => x != currentPresenter);

        Task volumeFadeOutTask = Task.CompletedTask;
        Task visualFadeOutTask = LayeredBackgroundImageExtensions
           .StartFadeReplaceAnimationAsync(lastMostFrontElement,
                                           currentPresenter,
                                           ClearAllChildrenExcept);

        if (lastMostFrontElement is FrameworkElement { Tag: SourceContext sourceContext })
        {
            TaskCompletionSource volumeFadeOutTcs = new();
            volumeFadeOutTask = volumeFadeOutTcs.Task;

            // If it has player in its source context, fade out the player first before disposing.
            sourceContext.SetVolume(0, () =>
            {
                sourceContext.Dispose();
                volumeFadeOutTcs.SetResult(); // Tell to complete the task.
            });
        }

        await Task.WhenAll(volumeFadeOutTask, visualFadeOutTask);
        return;

        void ClearAllChildrenExcept()
        {
            if (lastMostFrontElement is null)
                return;

            List<UIElement> others = [.. parent.Children.Where(x => x != currentPresenter)];
            foreach (UIElement other in others)
            {
                parent.Children.Remove(other);
            }
        }
    }

    private void UnloadAllPresenters()
    {
        UnloadLastPresenter(_placeholderGrid);
        UnloadLastPresenter(_foregroundGrid);
        UnloadLastPresenter(_backgroundGrid);
    }
}
