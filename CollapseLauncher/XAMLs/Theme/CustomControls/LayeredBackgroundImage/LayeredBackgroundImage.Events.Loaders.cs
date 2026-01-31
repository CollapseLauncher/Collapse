using CollapseLauncher.Extension;
using FFmpegInteropX;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.UI;
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

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

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
    internal static readonly HashSet<string> SupportedImageBitmapExtensions = new([
        ".jpg", ".jpeg", ".jpe", ".jif", ".jfif", // "image/jpeg"
        ".apng", ".png",                          // "image/apng" and "image/png"
        ".bmp",                                   // "image/bmp"
        ".gif",                                   // "image/gif"
        ".ico",                                   // "image/x-icon"
        ".tif", ".tiff"                           // "image/tiff"
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageBitmapExtensionsLookup =
        SupportedImageBitmapExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    internal static readonly HashSet<string> SupportedImageBitmapExternalCodecExtensions = new([
        ".jxr",  // "image/jxr" (Requires additional codec)
        ".avif", // "image/avif" (Requires additional codec)
        ".webp"  // "image/webp" (Requires additional codec)
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageBitmapExternalCodecExtensionsLookup =
        SupportedImageBitmapExternalCodecExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    internal static readonly HashSet<string> SupportedImageVectorExtensions = new([
        ".svg" // "image/svg"
    ], StringComparer.OrdinalIgnoreCase);
    internal static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SupportedImageVectorExtensionsLookup =
        SupportedImageVectorExtensions.GetAlternateLookup<ReadOnlySpan<char>>();

    internal static readonly HashSet<string> SupportedVideoExtensions = new([
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

    private bool _isLoaded = true;

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
                                            false,
                                            ref element._lastPlaceholderSourceType);
    }

    private static void BackgroundSource_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage element = (LayeredBackgroundImage)d;
        if (!element.IsLoaded)
        {
            return;
        }

        // If the element can be used on initial load change (with autoplay off) or static source is changed while previous static background is used,
        // then load static background source.
        if (element is { CanUseStaticBackground: true, IsVideoPlay: true } or { CanUseStaticBackground: true, IsUseStaticBackgroundUsed: true })
        {
            BackgroundSource_UseStatic(element);
            return;
        }

        BackgroundSource_UseNormal(element);
    }

    private static void BackgroundSource_UseStatic(LayeredBackgroundImage element)
    {
        Grid grid = element._backgroundGrid;

        element.LoadFromSourceAsyncDetached(BackgroundStaticSourceProperty,
                                            nameof(BackgroundStretch),
                                            nameof(BackgroundHorizontalAlignment),
                                            nameof(BackgroundVerticalAlignment),
                                            grid,
                                            false,
                                            ref element._lastBackgroundStaticSourceType);

        element.IsUseStaticBackgroundUsed = true;
    }

    private static void BackgroundSource_UseNormal(LayeredBackgroundImage element)
    {
        Grid grid = element._backgroundGrid;
        element.LoadFromSourceAsyncDetached(BackgroundSourceProperty,
                                            nameof(BackgroundStretch),
                                            nameof(BackgroundHorizontalAlignment),
                                            nameof(BackgroundVerticalAlignment),
                                            grid,
                                            true,
                                            ref element._lastBackgroundSourceType);

        element.IsUseStaticBackgroundUsed = false;
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
                                            false,
                                            ref element._lastForegroundSourceType);
    }

    private void LoadFromSourceAsyncDetached(
        DependencyProperty  sourceProperty,
        string              stretchProperty,
        string              horizontalAlignmentProperty,
        string              verticalAlignmentProperty,
        Grid                grid,
        bool                canReceiveVideo,
        ref MediaSourceType lastMediaType,
        bool                isForceNotLoad = false)
    {
        try
        {
            object? source = GetValue(sourceProperty);
            if (isForceNotLoad)
            {
                source = null;
            }

            if (source is null)
            {
                goto ClearAndReturnUnknown;
            }

            if (!TryGetMediaPathFromSource(source, out string? mediaPath))
            {
                goto ClearAndReturnUnknown;
            }

            if (GetMediaSourceTypeFromPath(mediaPath) is var mediaType &&
                mediaType == MediaSourceType.Unknown)
            {
                goto ClearAndReturnUnknown;
            }

            if (lastMediaType == MediaSourceType.Video)
            {
                // Dispose and Invalidate Video Player
                DisposeAndPauseVideoView();
            }

            InnerLoadDetached();
            lastMediaType = mediaType;
            return;

            async void InnerLoadDetached()
            {
                try
                {
                    // ReSharper disable once ConvertIfStatementToSwitchStatement
                    if (mediaType == MediaSourceType.Image &&
                        await LoadImageFromSourceAsync(source,
                                                       stretchProperty,
                                                       horizontalAlignmentProperty,
                                                       verticalAlignmentProperty,
                                                       this,
                                                       grid))
                    {
                        return;
                    }

                    if (mediaType == MediaSourceType.Video &&
                        canReceiveVideo &&
                        await LoadVideoFromSourceAsync(source, this))
                    {
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

    ClearAndReturnUnknown:
        ClearMediaGrid(grid);
        lastMediaType = MediaSourceType.Unknown;
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

            // Update property state
            instance.SetValue(IsVideoPlayProperty, false);

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

            return await image.LoadImageAsync(sourceUri, sourceStream, instance, UseImageCacheProperty);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }
    }

    private static async Task<bool> LoadVideoFromSourceAsync(
        object?                source,
        LayeredBackgroundImage instance)
    {
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
            return false;
        }

        try
        {
            // Set-ups Video Player upfront
            instance.InitializeVideoPlayer();

            bool        useFfmpeg = instance.UseFfmpegDecoder;
            MediaPlayer player    = instance._videoPlayer;

            // Assign media source using FFmpeg.
            if (useFfmpeg)
            {
                IRandomAccessStream? sourceStreamRandom = null;
                bool                 isError            = false;
                try
                {
                    MediaSourceConfig ffmpegConfig = new()
                    {
                        Video =
                        {
                            MaxDecoderThreads = (uint)Environment.ProcessorCount
                        }
                    };

                    int loadFfmpegRetry = 5;
                LoadFfmpeg:
                    FFmpegMediaSource? ffmpegMediaSource = null;
                    WindowId           windowId          = instance.GetElementWindowId();

                    if (sourceStream != null)
                    {
                        sourceStreamRandom = sourceStream.AsRandomAccessStream(true);
                        ffmpegMediaSource =
                            await FFmpegMediaSource.CreateFromStreamAsync(sourceStreamRandom, ffmpegConfig,
                                                                          windowId.Value);
                    }
                    else if (sourceUri != null)
                    {
                        string sourceUriStr = sourceUri.IsFile ? sourceUri.LocalPath : sourceUri.ToString();
                        ffmpegMediaSource = sourceUri.IsFile
                            ? await FFmpegMediaSource.CreateFromFileAsync(sourceUriStr, ffmpegConfig, windowId.Value)
                            : await FFmpegMediaSource.CreateFromUriAsync(sourceUriStr, ffmpegConfig, windowId.Value);
                    }

                    // Yeet
                    if (ffmpegMediaSource == null)
                    {
                        isError = true;
                        return false;
                    }

                    await ffmpegMediaSource.OpenWithMediaPlayerAsync(player);
                    Interlocked.Exchange(ref instance._videoFfmpegMediaSource, ffmpegMediaSource);

                    // HACK:
                    // Sometimes the media source isn't ready when the window just get restored from minimized state,
                    // which causing media to stale and gets MediaPlayer.CurrentState to Closed. So, wait and reinitialize
                    // until it's ready.
                    if (!(ffmpegMediaSource.PlaybackItem?.Source.IsOpen ?? false))
                    {
                        await Task.Delay(150);

                        // Unsubscribe frame renderer event to avoid double call, and then mark deinitialization.
                        Interlocked.Exchange(ref instance._isVideoInitialized, 0);
                        player.VideoFrameAvailable -= !instance.UseSafeFrameRenderer
                            ? instance.VideoPlayer_VideoFrameAvailableUnsafe
                            : instance.VideoPlayer_VideoFrameAvailableSafe;

                        ffmpegMediaSource.Dispose();

                        if (loadFfmpegRetry <= 0)
                        {
                            isError = true;
                            return false;
                        }

                        --loadFfmpegRetry;
                        goto LoadFfmpeg;
                    }
                }
                catch (Exception ex)
                {
                    isError = true;
                    Logger.LogWriteLine($"Cannot load media source with FFmpeg due to an error: {ex}",
                                        LogType.Error,
                                        true);
                }
                finally
                {
                    if (isError) sourceStreamRandom?.Dispose();
                }
            }
            else
            {
                // Assign media source using Media Foundation.
                if (sourceStream != null)
                {
                    IRandomAccessStream sourceStreamRandom = sourceStream.AsRandomAccessStream(true);
                    player.SetStreamSource(sourceStreamRandom);
                }
                else if (sourceUri != null)
                {
                    instance._videoPlayer.SetUriSource(sourceUri);
                }
                else
                {
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return false;
        }

        return true;
    }

    private static bool TryGetSourceHashCode(object? obj, out int hashCode)
    {
        Unsafe.SkipInit(out hashCode);

        switch (obj)
        {
            case Uri asUri:
                hashCode = asUri.AbsolutePath.GetHashCode();
                return true;
            case string asString:
                hashCode = asString.GetHashCode();
                return true;
            case FileStream asStream:
                hashCode = asStream.Name.GetHashCode();
                return true;
            default:
                return false;
        }
    }

    private void InitializeVideoFrameOnMediaOpened(MediaPlayer sender, object args)
    {
        DispatcherQueue.TryEnqueue(Impl);
        return;

        void Impl()
        {
            // Update Media Duration and Update Binding to it.
            SetValue(MediaDurationProperty, sender.NaturalDuration);
            SetValue(IsCurrentMediaSeekableProperty, sender.CanSeek);

            // Create instance
            Image image = new()
            {
                Tag = (_backgroundGrid, this),
                Name = "VideoRenderFrame"
            };

            // Bind property
            image.BindProperty(this,
                               nameof(BackgroundStretch),
                               Image.StretchProperty,
                               BindingMode.OneWay);
            image.BindProperty(this,
                               nameof(BackgroundHorizontalAlignment),
                               HorizontalAlignmentProperty,
                               BindingMode.OneWay);
            image.BindProperty(this,
                               nameof(BackgroundVerticalAlignment),
                               VerticalAlignmentProperty,
                               BindingMode.OneWay);

            InitializeRenderTargetSize(sender.PlaybackSession);

            // Register events
            image.Loaded   += Image_VideoFrameOnLoaded;
            image.Unloaded += Image_VideoFrameOnUnloaded;

            // Add to children
            image.Transitions.Add(new ContentThemeTransition());
            _backgroundGrid.Children.Add(image);
        }
    }

    private static void Image_VideoFrameOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: ValueTuple<Grid, LayeredBackgroundImage> parentGrid })
        {
            return;
        }

        Image_ImageOpened(sender, e);
        if (parentGrid.Item2.IsVideoAutoplay)
        {
            parentGrid.Item2.InitializeAndPlayVideoView();
        }
    }

    private static void Image_VideoFrameOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image { Tag: ValueTuple<Grid, LayeredBackgroundImage> parentGrid })
        {
            return;
        }

        parentGrid.Item2.DisposeAndPauseVideoView();
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
            parentGrid.Item2.NotifyImageLoaded();
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

        return SupportedVideoExtensionsLookup.Contains(extension)
            ? MediaSourceType.Video
            : MediaSourceType.Unknown;
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
            // This one is for Video since ImageOpened with Canvas source will never trigger this so we use Loaded instead.
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
