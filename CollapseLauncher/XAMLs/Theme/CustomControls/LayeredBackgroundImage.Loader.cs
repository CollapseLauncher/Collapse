using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;

// ReSharper disable AccessToModifiedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Loader

    private async void LoadAllPresenters()
    {
        Task foregroundLoadTask = LoadPresenter(_foregroundGrid, ForegroundSource);
        Task backgroundLoadTask = LoadPresenter(_backgroundGrid, BackgroundSource);

        await Task.WhenAll(foregroundLoadTask, backgroundLoadTask);
    }

    private async Task LoadPresenter(
        Panel         parent,
        object?       source)
    {
        FrameworkElement? currentPresenter = null;
        ulong             sourceContextGen = Interlocked.Increment(ref _sourceContextGeneration);
        try
        {
            InvalidateLastSourceLoadingToken(parent);
            if (source == null)
                return;

            bool isBackgroundParent = parent.Name.StartsWith("Background", StringComparison.OrdinalIgnoreCase);
            SourceContext sourceContext = new(this, sourceContextGen)
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

                    // -- For Background Grid Only. Update MediaState and assign subscriber for it.
                    if (isBackgroundParent &&
                        sourceGuessResult.FormatType == SourceFormatType.Video &&
                        presenter.Tag is SourceContext { Player: { } player } thisSourceContext)
                    {
                        if (IsAudioEnabled)
                            SetVolume(AudioVolume);

                        SetValue(MediaStateProperty,            player.CurrentState);    // Update the state one time.
                        SetValue(MediaDurationPositionProperty, player.Position);        // Update the state one time.
                        SetValue(MediaDurationProperty,         player.NaturalDuration); // Update the state one time.
                        thisSourceContext.RegisterPlayerCallbacks(MediaPlayer_OnCurrentStateChanged,
                                                                  MediaPlayer_OnPositionChanged);

                        TimeSpan lastPosition = thisSourceContext.LoadPlayerLastPosition();
                        player.Position = lastPosition; // Seek to the last position.
                    }

                    await UnloadLastPresenter(parent, presenter);
                    tcs.TrySetResult();
                }
            }

            // -- For Background Grid Only and notify if the source is loaded as image, notify
            //    the MediaState as Closed.
            if (isBackgroundParent && sourceGuessResult.FormatType == SourceFormatType.Image)
            {
                SetValue(MediaStateProperty, MediaPlayerState.Closed);
            }

            if (currentPresenter == null)
                tcs.TrySetResult();

            await tcs.Task;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"An error has occurred while loading source for: {parent.Name}\r\n{ex}", LogType.Error, true);
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

    #endregion

    #region Invalidator

    private static void InvalidateLastSourceLoadingToken(Panel parent)
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

    #endregion

    #region Unloader

    private static async Task UnloadLastPresenter(
        Panel      parent,
        UIElement? currentPresenter = null)
    {
        Task            volumeFadeOutTask = Task.CompletedTask;
        List<UIElement> others            = [];

        try
        {
            UIElement? lastPresenter = currentPresenter == null
                ? parent.Children.LastOrDefault()
                : parent.Children.LastOrDefault(x => x != currentPresenter);

            others.AddRange(currentPresenter == null
                                ? parent.Children
                                : parent.Children.Where(other => currentPresenter != other));

            if (lastPresenter is FrameworkElement { Tag: SourceContext lastSourceContext })
            {
                TaskCompletionSource volumeFadeOutTcs = new();
                volumeFadeOutTask = volumeFadeOutTcs.Task;

                // If it has player in its source context, fade out the player first before disposing.
                lastSourceContext.SetVolume(0, () => volumeFadeOutTcs.TrySetResult()); // Tell to complete the task.
            }

            Task visualFadeOutTask = LayeredBackgroundImageExtensions
               .StartFadeReplaceAnimationAsync(lastPresenter, currentPresenter);

            await Task.WhenAll(volumeFadeOutTask, visualFadeOutTask);

            foreach (UIElement other in others)
            {
                parent.Children.Remove(other);
                if (other is FrameworkElement { Tag: SourceContext otherSourceContext })
                {
                    otherSourceContext.UnregisterPlayerCallbacks(); // Unregister callback early
                    otherSourceContext.Dispose();
                }
            }

            Logger.LogWriteLine($"Parent Grid: {parent.Name} has remained: {parent.Children.Count} children", LogType.Debug);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }

    private async void UnloadAllPresenters()
    {
        Task foregroundTask = UnloadLastPresenter(_foregroundGrid);
        Task backgroundTask = UnloadLastPresenter(_backgroundGrid);

        await Task.WhenAll(foregroundTask, backgroundTask);
    }

    #endregion

    #region Helper

    public bool TryGetBackgroundSourceContext(
        [NotNullWhen(true)] out SourceContext? sourceContext,
        [NotNullWhen(true)] out MediaPlayer?   player)
    {
        Unsafe.SkipInit(out sourceContext);
        Unsafe.SkipInit(out player);

        if (_backgroundGrid == null! ||
            _backgroundGrid.Children.LastOrDefault() is not FrameworkElement
            {
                Tag: SourceContext { Player: { } thisPlayer } thisSourceContext
            })
        {
            return false;
        }

        player        = thisPlayer;
        sourceContext = thisSourceContext;
        return true;
    }

    #endregion
}
