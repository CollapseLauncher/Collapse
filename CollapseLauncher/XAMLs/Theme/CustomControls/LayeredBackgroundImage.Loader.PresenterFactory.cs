using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using FFmpegInteropX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private async ValueTask<Image?> AddImagePresenterAsync(
        Panel                        parent,
        object?                      source,
        RoutedEventHandler?          onLoadedEventHandler,
        ExceptionRoutedEventHandler? onFailedEventHandler = null)
    {
        if (!LayeredBackgroundImageExtensions
               .TryGetBindingPropertyNames(parent,
                                           out string? stretchNameOf,
                                           out string? hAlignNameOf,
                                           out string? vAlignNameOf))
        {
            return null;
        }

        ImageSource? imageSource = await LayeredBackgroundImageExtensions.CreateImageSourceFromObjectAsync(source);
        if (imageSource is null)
            return null;

        Image image = new();
        parent.Children.Add(image);

        image.ImageOpened += onLoadedEventHandler;
        image.ImageFailed += onFailedEventHandler;

        image.BindProperty(this,
                           stretchNameOf,
                           Image.StretchProperty,
                           BindingMode.OneWay);

        image.BindProperty(this,
                           hAlignNameOf,
                           HorizontalAlignmentProperty,
                           BindingMode.OneWay);

        image.BindProperty(this,
                           vAlignNameOf,
                           VerticalAlignmentProperty,
                           BindingMode.OneWay);

        return image;
    }

    private async Task<MediaFoundationPresenter?> AddVideoPresenterAsync(
        Panel               parent,
        object?             source,
        SourceContext       sourceContext,
        RoutedEventHandler? onLoadedEventHandler)
    {
        if (!LayeredBackgroundImageExtensions
               .TryGetBindingPropertyNames(parent,
                                           out string? stretchNameOf,
                                           out string? hAlignNameOf,
                                           out string? vAlignNameOf))
        {
            return null;
        }

        bool isUseFfmpeg = UseFfmpegDecoder && ImageBackgroundManager.Shared.GlobalIsFFmpegAvailable;
        object? mediaSource = await LayeredBackgroundImageExtensions.CreateMediaSourceFromObjectAsync(source, isUseFfmpeg, FfmpegDecoderMode);
        if (mediaSource is null)
            return null;

        MediaPlayer mediaPlayer = new()
        {
            IsLoopingEnabled          = true,
            IsVideoFrameServerEnabled = true,
            AutoPlay                  = true
        };
        sourceContext.Player = mediaPlayer;

        MediaFoundationPresenter presenter = new();
        sourceContext.PlayerPresenter = presenter;
        mediaPlayer.MediaOpened += MediaPlayerOnMediaOpened;

        /*
        presenter.BindProperty(this,
                               stretchNameOf,
                               MediaFoundationPresenter.StretchProperty,
                               BindingMode.OneWay);
        */

        presenter.BindProperty(this,
                               hAlignNameOf,
                               HorizontalAlignmentProperty,
                               BindingMode.OneWay);

        presenter.BindProperty(this,
                               vAlignNameOf,
                               VerticalAlignmentProperty,
                               BindingMode.OneWay);

        parent.Children.Add(presenter);

        if (mediaSource is FFmpegMediaSource ffmpegMediaSource)
        {
            await ffmpegMediaSource.OpenWithMediaPlayerAsync(mediaPlayer);
        }

        if (mediaSource is MediaSource asMediaSource)
        {
            mediaPlayer.Source = asMediaSource;
        }

        return presenter;

        void MediaPlayerOnMediaOpened(MediaPlayer sender, object args)
        {
            mediaPlayer.MediaOpened -= MediaPlayerOnMediaOpened;
            presenter.DispatcherQueue.TryEnqueue(() => onLoadedEventHandler?.Invoke(presenter, null));
        }
    }
}
