using CollapseLauncher.Extension;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using FFmpegInteropX;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
// ReSharper disable AccessToModifiedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private async ValueTask<Image?> AddImagePresenterAsync(
        Panel                        parent,
        object?                      source,
        SourceContext                sourceContext,
        RoutedEventHandler?          onLoadedEventHandler,
        ExceptionRoutedEventHandler? onFailedEventHandler = null,
        CancellationToken            token                = default)
    {
        if (token.IsCancellationRequested)
            return null;

        if (!LayeredBackgroundImageExtensions
               .TryGetBindingPropertyNames(parent,
                                           out string? stretchNameOf,
                                           out string? hAlignNameOf,
                                           out string? vAlignNameOf))
        {
            return null;
        }

        ImageSource? imageSource = await LayeredBackgroundImageExtensions
           .CreateImageSourceFromObjectAsync(source, token);

        if (imageSource is null)
            return null;

        sourceContext.MediaSource = imageSource;
        Image image = new()
        {
            Tag = sourceContext
        };
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

        image.Source = imageSource;
        return image;
    }

    private async Task<MediaFoundationPresenter?> AddVideoPresenterAsync(
        Panel               parent,
        object?             source,
        SourceContext       sourceContext,
        RoutedEventHandler? onLoadedEventHandler,
        CancellationToken   token)
    {
        if (token.IsCancellationRequested)
            return null;

        if (!LayeredBackgroundImageExtensions
               .TryGetBindingPropertyNames(parent,
                                           out string? stretchNameOf,
                                           out string? hAlignNameOf,
                                           out string? vAlignNameOf))
        {
            return null;
        }

        bool     isUseFfmpeg = UseFfmpegDecoder && ImageBackgroundManager.Shared.GlobalIsFFmpegAvailable;
        WindowId windowId    = parent.GetElementWindowId();

        MediaFoundationPresenter? presenter = null;

        (MediaPlayer? mediaPlayer, FFmpegMediaSource? ffmpegMediaSource) =
            await LayeredBackgroundImageExtensions
               .CreateMediaSourceFromObjectAsync(source,
                                                 isUseFfmpeg,
                                                 FfmpegDecoderMode,
                                                 windowId,
                                                 IsVideoAutoplay,
                                                 OnMediaOpened,
                                                 token: token);

        if (mediaPlayer is null)
            return null;

        presenter = new MediaFoundationPresenter
        {
            Tag = sourceContext
        };

        sourceContext.Player          = mediaPlayer;
        sourceContext.PlayerPresenter = presenter;
        sourceContext.MediaSource     = ffmpegMediaSource;

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
        return presenter;

        void OnMediaOpened() => presenter?.DispatcherQueue.TryEnqueue(() => onLoadedEventHandler?.Invoke(presenter, null));
    }
}
