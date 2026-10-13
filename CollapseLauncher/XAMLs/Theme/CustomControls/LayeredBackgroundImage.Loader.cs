using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        FrameworkElement? currentPresenter = null;
        try
        {
            InvalidateLastLoadingToken(parent);
            if (source == null)
                return;

            SourceContext sourceContext = new()
            {
                Source = source
            };

            CancellationToken    loadToken = sourceContext.SourceLoadingCts.Token;
            TaskCompletionSource tcs       = new();

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
                                                                sourceContext,
                                                                ImagePresenterOnLoaded,
                                                                token: loadToken);

                async void ImagePresenterOnLoaded(object sender, RoutedEventArgs e)
                {
                    if (sender is not Image asImage)
                    {
                        return;
                    }

                    asImage.ImageOpened -= ImagePresenterOnLoaded;
                    asImage.ImageFailed -= ImagePresenterOnLoaded;
                    await UnloadLastPresenter(parent, asImage);
                    tcs.TrySetResult();
                }
            }

            if (sourceGuessResult.FormatType == SourceFormatType.Video)
            {
                currentPresenter = await AddVideoPresenterAsync(parent,
                                                                source,
                                                                sourceContext,
                                                                VideoPresenterOnLoaded,
                                                                loadToken);

                async void VideoPresenterOnLoaded(object sender, RoutedEventArgs e)
                {
                    if (sender is not MediaFoundationPresenter presenter)
                    {
                        return;
                    }

                    await UnloadLastPresenter(parent, presenter);
                    tcs.TrySetResult();
                }
            }

            if (currentPresenter == null)
                tcs.TrySetResult();

            await tcs.Task;
        }
        finally
        {
            // Clear the parent if no presenter is available
            if (currentPresenter is null)
            {
                _ = UnloadLastPresenter(parent);
            }
        }
    }

    private static void InvalidateLastLoadingToken(Panel parent)
    {
        foreach (UIElement element in parent.Children)
        {
            if (element is not FrameworkElement { Tag: SourceContext sourceContext })
            {
                continue;
            }

            sourceContext.SourceLoadingCts?.Cancel();
        }
    }

    private static async Task UnloadLastPresenter(
        Panel             parent,
        UIElement?        currentPresenter = null)
    {
        UIElement?      lastPresenter = parent.Children.LastOrDefault(x => x != currentPresenter);
        List<UIElement> others        = [.. parent.Children.Where(other => currentPresenter != other)];

        Task volumeFadeOutTask = Task.CompletedTask;
        if (lastPresenter is FrameworkElement { Tag: SourceContext sourceContext })
        {
            TaskCompletionSource volumeFadeOutTcs = new();
            volumeFadeOutTask = volumeFadeOutTcs.Task;

            // If it has player in its source context, fade out the player first before disposing.
            sourceContext.SetVolume(0, volumeFadeOutTcs.SetResult); // Tell to complete the task.
        }

        try
        {
            Task visualFadeOutTask = LayeredBackgroundImageExtensions
               .StartFadeReplaceAnimationAsync(lastPresenter, currentPresenter);

            await Task.WhenAll(volumeFadeOutTask, visualFadeOutTask);
            foreach (UIElement other in others)
            {
                parent.Children.Remove(other);
                if (other is FrameworkElement { Tag: SourceContext otherSourceContext })
                    otherSourceContext.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private async void UnloadAllPresenters()
    {
        Task placeholderTask = UnloadLastPresenter(_placeholderGrid);
        Task foregroundTask  = UnloadLastPresenter(_foregroundGrid);
        Task backgroundTask  = UnloadLastPresenter(_backgroundGrid);

        await Task.WhenAll(placeholderTask, foregroundTask, backgroundTask);
    }
}
