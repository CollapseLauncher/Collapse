using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Playback;
using Windows.Storage.Streams;

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
#pragma warning disable CsWinRT1032

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Enums

    private enum MediaSourceType
    {
        Unknown,
        Image,
        Video,
    }

    /// <summary>
    /// All the sources in this list are common image formats supported by Windows Imaging Component (WIC).
    /// Some formats might require additional codecs to be installed.
    /// <br/><br/>
    /// The extensions list are taken from:<br/>
    /// https://developer.mozilla.org/en-US/docs/Web/Media/Guides/Formats/Image_types#jpeg_joint_photographic_experts_group_image
    /// </summary>
    private static readonly HashSet<string> SupportedImageBitmapExtensions = new([
        ".jpg", ".jpeg", ".jpe", ".jif", ".jfif", // "image/jpeg"
        ".apng", ".png",                          // "image/apng" and "image/png"
        ".bmp",                                   // "image/bmp"
        ".gif",                                   // "image/gif"
        ".ico",                                   // "image/x-icon"
        ".tif", ".tiff",                          // "image/tiff"
        ".xbm"                                    // "image/xbm"
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageBitmapExtensionsLookup =
        SupportedImageBitmapExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> SupportedImageBitmapExternalCodecExtensions = new([
        ".jxr",  // "image/jxr" (Requires additional codec)
        ".avif", // "image/avif" (Requires additional codec)
        ".webp"  // "image/webp" (Requires additional codec)
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageBitmapExternalCodecExtensionsLookup =
        SupportedImageBitmapExternalCodecExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> SupportedImageVectorExtensions = new([
        ".svg" // "image/svg"
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageVectorExtensionsLookup =
        SupportedImageVectorExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    private static readonly HashSet<string> SupportedVideoExtensions = new([
        ".3gp", ".3gp2",                                                // "video/3gp"
        ".asf", ".wmv",                                                 // "video/wmv"
        ".avi",                                                         // "video/avi"
        ".flv", ".f4v",                                                 // "video/flv"
        ".mp4", ".m4v",                                                 // "video/mp4"
        ".mov", ".movie", ".qt",                                        // "video/quicktime"
        ".webm",                                                        // "video/webm"
        ".mpg", ".mpeg", ".ts", ".tsv", ".ps", ".m2ts", ".mts", ".vob", // "video/mpeg"
        ".ogv",                                                         // "video/ogg"
        ".mkv", ".mks"                                                  // "video/matroska"
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedVideoExtensionsLookup =
        SupportedVideoExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    #endregion

    #region Fields

    private bool _isResizingVideoCanvas;
    private bool _isDrawingVideoFrame;

    private MediaSourceType? _lastMediaType;

    #endregion

    #region Loaders

    private static void PlaceholderSource_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage element = (LayeredBackgroundImage)d;
        if (!element.IsLoaded)
        {
            return;
        }

        Grid grid = element._placeholderGrid;
        element.LoadFromSourceAsyncDetached(PlaceholderSourceProperty,
                                            nameof(PlaceholderStretch),
                                            nameof(PlaceholderHorizontalAlignment),
                                            nameof(PlaceholderVerticalAlignment),
                                            grid,
                                            element,
                                            false);
    }

    private static void BackgroundSource_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage element = (LayeredBackgroundImage)d;
        if (!element.IsLoaded)
        {
            return;
        }

        Grid grid = element._backgroundGrid;
        element.LoadFromSourceAsyncDetached(BackgroundSourceProperty,
                                            nameof(BackgroundStretch),
                                            nameof(BackgroundHorizontalAlignment),
                                            nameof(BackgroundVerticalAlignment),
                                            grid,
                                            element,
                                            true);
    }

    private static void ForegroundSource_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage element = (LayeredBackgroundImage)d;
        if (!element.IsLoaded)
        {
            return;
        }

        Grid grid = element._foregroundGrid;
        element.LoadFromSourceAsyncDetached(ForegroundSourceProperty,
                                            nameof(ForegroundStretch),
                                            nameof(ForegroundHorizontalAlignment),
                                            nameof(ForegroundVerticalAlignment),
                                            grid,
                                            element,
                                            false);
    }

    #endregion

    #region

    private async void LoadFromSourceAsyncDetached(
        DependencyProperty     sourceProperty,
        string                 stretchProperty,
        string                 horizontalAlignmentProperty,
        string                 verticalAlignmentProperty,
        Grid                   grid,
        LayeredBackgroundImage instance,
        bool                   canReceiveVideo)
    {
        try
        {
            object? source = GetValue(sourceProperty);
            if (source is null)
            {
                goto ClearGrid;
            }

            if (!TryGetMediaPathFromSource(source, out string? mediaPath))
            {
                goto ClearGrid;
            }

            if (GetMediaSourceTypeFromPath(mediaPath) is var mediaType &&
                mediaType == MediaSourceType.Unknown)
            {
                goto ClearGrid;
            }

            if (grid.Name.StartsWith("Background") &&
                _lastMediaType == MediaSourceType.Video)
            {
                DisposeVideoPlayer();
            }

            _lastMediaType = mediaType;

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (mediaType == MediaSourceType.Image &&
                await LoadImageFromSourceAsync(source,
                                               stretchProperty,
                                               horizontalAlignmentProperty,
                                               verticalAlignmentProperty,
                                               instance,
                                               grid))
            {
                return;
            }

            if (mediaType == MediaSourceType.Video &&
                canReceiveVideo &&
                await LoadVideoFromSourceAsync(source,
                                               stretchProperty,
                                               horizontalAlignmentProperty,
                                               verticalAlignmentProperty,
                                               instance,
                                               grid))
            {
                return;
            }

        ClearGrid:
            ClearMediaGrid(grid);
        }
        catch
        {
            // ignored
        }
    }

    private static async ValueTask<bool> LoadImageFromSourceAsync(
        object?                source,
        string                 stretchProperty,
        string                 horizontalAlignmentProperty,
        string                 verticalAlignmentProperty,
        LayeredBackgroundImage instance,
        Grid                   grid)
    {
        try
        {
            // Create instance
            Image image = new();

            // Bind property
            image.BindProperty(instance, stretchProperty,             Image.StretchProperty,       BindingMode.OneWay);
            image.BindProperty(instance, horizontalAlignmentProperty, HorizontalAlignmentProperty, BindingMode.OneWay);
            image.BindProperty(instance, verticalAlignmentProperty,   VerticalAlignmentProperty,   BindingMode.OneWay);

            image.Transitions.Add(new ContentThemeTransition());
            grid.Children.Add(image);

            image.Tag         =  (grid, instance);
            image.ImageOpened += Image_ImageOpened;

            Uri? sourceUri = source as Uri;

            if (sourceUri == null &&
                source is string asStringSource)
            {
                sourceUri = asStringSource.GetStringAsUri();
            }

            Stream? sourceStream = null;
            if (source is Stream { CanSeek: true, CanRead: true } asSeekableStream)
            {
                sourceStream = asSeekableStream;
            }

            if (sourceStream == null &&
                sourceUri == null)
            {
                return false;
            }

            return await image.LoadImageAsync(sourceUri, sourceStream, instance);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private static async Task<bool> LoadVideoFromSourceAsync(
        object?                source,
        string                 stretchProperty,
        string                 horizontalAlignmentProperty,
        string                 verticalAlignmentProperty,
        LayeredBackgroundImage instance,
        Grid                   grid)
    {
        // Setting up video player
        instance.SetupVideoPlayer();
        MediaPlayer player = instance._videoPlayer;
        player.MediaOpened += InitializeVideoFrameOnMediaOpened;

        if (instance.MediaCacheHandler is { } cacheHandler)
        {
            MediaCacheResult cacheResult = await cacheHandler.LoadCachedSource(source);
            source = cacheResult.CachedSource;
        }

        Uri? sourceUri = source as Uri;

        if (sourceUri == null &&
            source is string asStringSource)
        {
            sourceUri = asStringSource.GetStringAsUri();
        }

        Stream? sourceStream = null;
        if (source is Stream { CanSeek: true, CanRead: true } asSeekableStream)
        {
            sourceStream = asSeekableStream;
        }

        if (sourceStream == null &&
            sourceUri == null)
        {
            goto CleanupAndReturnFalse;
        }

        // Assign media source
        if (sourceStream != null)
        {
            IRandomAccessStream? sourceStreamRandom = sourceStream.AsRandomAccessStream();
            player.SetStreamSource(sourceStreamRandom);
        }
        else if (sourceUri != null)
        {
            player.SetUriSource(sourceUri);
        }
        else
        {
            goto CleanupAndReturnFalse;
        }

        return true;

    CleanupAndReturnFalse:
        player.MediaOpened -= InitializeVideoFrameOnMediaOpened;
        return false;

        void InitializeVideoFrameOnMediaOpened(MediaPlayer sender, object args)
        {
            instance.DispatcherQueue.TryEnqueue(() =>
            {
                // Create instance
                Image image = new()
                {
                    Tag = (grid, instance)
                };

                // Bind property
                image.BindProperty(instance,
                                   stretchProperty,
                                   Image.StretchProperty,
                                   BindingMode.OneWay);
                image.BindProperty(instance,
                                   horizontalAlignmentProperty,
                                   HorizontalAlignmentProperty,
                                   BindingMode.OneWay);
                image.BindProperty(instance,
                                   verticalAlignmentProperty,
                                   VerticalAlignmentProperty,
                                   BindingMode.OneWay);

                instance.InitializeCanvasBitmapSource(image, sender.PlaybackSession);

                // Register events
                image.Loaded   += Image_VideoFrameOnLoaded;
                image.Unloaded += Image_VideoFrameOnUnloaded;

                // Add to children
                image.Transitions.Add(new ContentThemeTransition());
                grid.Children.Add(image);
                instance._videoPlayer.Play();
            });
        }
    }

    private unsafe void VideoPlayer_VideoFrameAvailable(MediaPlayer sender, object args)
    {
        if (_canvasImageSource == null)
        {
            return;
        }

        // Use Interlock to ensure thre session is only created one time. But with one downside that
        // the first frame will always be skipped due to atomic operation.
        // Note:
        // _currentCanvasDrawingSession is ensured to expect null first, then create and exit.
        // The next frame will be drawn.
        CanvasDrawingSession? session = null;
        if ((session = Interlocked.Exchange(ref _currentCanvasDrawingSession, null)) == null)
        {
            _currentCanvasDrawingSession = _canvasImageSource.CreateDrawingSession(default, _currentCanvasRenderSize);
            return;
        }

        try
        {
#if USENATIVESWAPCHAIN
            DXGI_PRESENT_PARAMETERS parameter = default;

            var swapChain = _swapChainContext.DXGISwapChain;
            var surface = _swapChainContext.Direct3DSurface;

            Rect renderArea = new(0, 0, _canvasWidth, _canvasHeight);
            // CanvasDrawingSession? canvasSession = _canvasImageSource.CreateDrawingSession(default);
            sender.CopyFrameToVideoSurface(surface, renderArea);
            swapChain?.Present1(1, 0, in parameter);
#else
            sender.CopyFrameToVideoSurface(_canvasRenderTarget);
            session.DrawImage(_canvasRenderTarget);

            if (DispatcherQueue.HasThreadAccess)
            {
                FinalizeFrame();
            }
            else
            {
                DispatcherQueue.TryEnqueue(FinalizeFrame);
            }

            void FinalizeFrame()
            {
                try
                {
                    session.Dispose();
                }
                catch { }
                finally
                {
                }
            }
#endif
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Interlocked.Exchange(ref _isDrawingVideoFrame, false);
        }
    }

    private static void Image_VideoFrameOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: ValueTuple<Grid, LayeredBackgroundImage> parentGrid })
        {
            return;
        }

        parentGrid.Item2.Play();
        Image_ImageOpened(sender, e);
    }

    private static void Image_VideoFrameOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: ValueTuple<Grid, LayeredBackgroundImage> parentGrid })
        {
            return;
        }

        parentGrid.Item2.Pause();
    }

    private static void Image_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: ValueTuple<Grid, LayeredBackgroundImage> parentGrid } image)
        {
            return;
        }

        // Set placeholder to hidden once loaded
        ref bool isPlaceholderHidden = ref parentGrid.Item2._isPlaceholderHidden;
        if (parentGrid.Item1.Name.StartsWith("Background", StringComparison.OrdinalIgnoreCase) &&
            !Interlocked.Exchange(ref isPlaceholderHidden, true))
        {
            VisualStateManager.GoToState(parentGrid.Item2, StateNamePlaceholderStateHidden, true);
        }

        // HACK: Tells the Grid to temporarily detach all UIElement children
        //       then re-add the image to the grid
        ClearMediaGrid(parentGrid.Item1, image);

        // Remove transition once loaded
        image.Transitions.Clear();
    }

    private static MediaSourceType GetMediaSourceTypeFromPath(ReadOnlySpan<char> path)
    {
        ReadOnlySpan<char> extension = Path.GetExtension(path);

        if (SupportedImageBitmapExtensionsLookup.Contains(extension) ||
            SupportedImageBitmapExternalCodecExtensionsLookup.Contains(extension) ||
            SupportedImageVectorExtensionsLookup.Contains(extension))
        {
            return MediaSourceType.Image;
        }

        if (SupportedVideoExtensionsLookup.Contains(extension))
        {
            return MediaSourceType.Video;
        }

        return MediaSourceType.Unknown;
    }

    // Returns true if the media source is supported. Otherwise, false and return a null string path.
    private static bool TryGetMediaPathFromSource(object? source, [NotNullWhen(true)] out string? path)
    {
        Unsafe.SkipInit(out path);

        path = source switch
        {
            string asString         => asString,
            Uri asUrl               => asUrl.AbsolutePath,
            FileInfo asFileInfo     => asFileInfo.FullName,
            FileStream asFileStream => asFileStream.Name,
            _                       => path
        };

        return !string.IsNullOrEmpty(path);
    }

    private static void ClearMediaGrid(Grid grid, UIElement? except = null)
    {
        List<UIElement> elementExcepted =
            except == null ? grid.Children.ToList() :
                grid.Children.Where(x => x != except)
                    .ToList();

        foreach (Image image in elementExcepted.OfType<Image>())
        {
            // This one is for Image. The source will always guarantee to call this event.
            image.ImageOpened -= Image_ImageOpened;
            // This one is for Video since ImageOpened with Canvas source will never triggers this so we use Loaded instead.
            image.Loaded   -= Image_VideoFrameOnLoaded;
            image.Unloaded -= Image_VideoFrameOnUnloaded;
            // Clears the loaded ImageSource
            image.Source = null;
        }

        foreach (UIElement element in elementExcepted)
        {
            grid.Children.Remove(element);
        }
    }

#endregion
}
