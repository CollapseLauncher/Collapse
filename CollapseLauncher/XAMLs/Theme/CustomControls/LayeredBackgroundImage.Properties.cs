using FFmpegInteropX;
using Hi3Helper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Properties

    public IMediaCacheHandler? MediaCacheHandler
    {
        get => (IMediaCacheHandler?)GetValue(MediaCacheHandlerProperty);
        set => SetValue(MediaCacheHandlerProperty, value);
    }

    public bool IsVideoAutoplay
    {
        get => (bool)GetValue(IsVideoAutoplayProperty);
        set => SetValue(IsVideoAutoplayProperty, value);
    }

    public bool UseFfmpegDecoder
    {
        get => (bool)GetValue(UseFfmpegDecoderProperty);
        set => SetValue(UseFfmpegDecoderProperty, value);
    }

    public VideoDecoderMode FfmpegDecoderMode
    {
        get => (VideoDecoderMode)GetValue(FfmpegDecoderModeProperty);
        set => SetValue(FfmpegDecoderModeProperty, value);
    }

    public bool IsAudioEnabled
    {
        get => (bool)GetValue(IsAudioEnabledProperty);
        set => SetValue(IsAudioEnabledProperty, value);
    }

    public double AudioVolume
    {
        get => GetValue(AudioVolumeProperty).TryGetDouble();
        set => SetValue(AudioVolumeProperty, value);
    }

    public bool IsParallaxEnabled
    {
        get => (bool)GetValue(IsParallaxEnabledProperty);
        set => SetValue(IsParallaxEnabledProperty, value);
    }

    public double ParallaxHorizontalShift
    {
        get => (double)GetValue(ParallaxHorizontalShiftProperty);
        set => SetValue(ParallaxHorizontalShiftProperty, value);
    }

    public double ParallaxVerticalShift
    {
        get => (double)GetValue(ParallaxVerticalShiftProperty);
        set => SetValue(ParallaxVerticalShiftProperty, value);
    }

    public FrameworkElement? ParallaxHoverSource
    {
        get => (FrameworkElement?)GetValue(ParallaxHoverSourceProperty);
        set => SetValue(ParallaxHoverSourceProperty, value);
    }

    public bool ParallaxResetOnUnfocused
    {
        get => (bool)GetValue(ParallaxResetOnUnfocusedProperty);
        set => SetValue(ParallaxResetOnUnfocusedProperty, value);
    }

    public object? BackgroundSource
    {
        get => (object?)GetValue(BackgroundSourceProperty);
        set => SetValue(BackgroundSourceProperty, value);
    }

    public Stretch BackgroundStretch
    {
        get => (Stretch)GetValue(BackgroundStretchProperty);
        set => SetValue(BackgroundStretchProperty, value);
    }

    public HorizontalAlignment BackgroundHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(BackgroundHorizontalAlignmentProperty);
        set => SetValue(BackgroundHorizontalAlignmentProperty, value);
    }

    public VerticalAlignment BackgroundVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(BackgroundVerticalAlignmentProperty);
        set => SetValue(BackgroundVerticalAlignmentProperty, value);
    }

    public object? ForegroundSource
    {
        get => (object?)GetValue(ForegroundSourceProperty);
        set => SetValue(ForegroundSourceProperty, value);
    }

    public Stretch ForegroundStretch
    {
        get => (Stretch)GetValue(ForegroundStretchProperty);
        set => SetValue(ForegroundStretchProperty, value);
    }

    public HorizontalAlignment ForegroundHorizontalAlignment
    {
        get => (HorizontalAlignment)GetValue(ForegroundHorizontalAlignmentProperty);
        set => SetValue(ForegroundHorizontalAlignmentProperty, value);
    }

    public VerticalAlignment ForegroundVerticalAlignment
    {
        get => (VerticalAlignment)GetValue(ForegroundVerticalAlignmentProperty);
        set => SetValue(ForegroundVerticalAlignmentProperty, value);
    }

    public double ForegroundOpacity
    {
        get => (double)GetValue(ForegroundOpacityProperty);
        set => SetValue(ForegroundOpacityProperty, value);
    }

    public MediaPlayerState MediaState
    {
        get => (MediaPlayerState)GetValue(MediaStateProperty);
        set => SetValue(MediaStateProperty, value);
    }

    public TimeSpan MediaDurationPosition
    {
        get => (TimeSpan)GetValue(MediaDurationPositionProperty);
        set => SetValue(MediaDurationPositionProperty, value);
    }

    public TimeSpan MediaDuration
    {
        get => (TimeSpan)GetValue(MediaDurationProperty);
    }

    public double BackgroundElevationPixels
    {
        get => (double)GetValue(BackgroundElevationPixelsProperty);
        set => SetValue(BackgroundElevationPixelsProperty, value);
    }

    public bool IsBackgroundElevated
    {
        get => (bool)GetValue(IsBackgroundElevatedProperty);
        set => SetValue(IsBackgroundElevatedProperty, value);
    }

    public Brush SmokeBackground
    {
        get => (Brush)GetValue(SmokeBackgroundProperty);
        set => SetValue(SmokeBackgroundProperty, value);
    }

    public double SmokeOpacity
    {
        get => (double)GetValue(SmokeOpacityProperty);
        set => SetValue(SmokeOpacityProperty, value);
    }

    public bool UseImageCache
    {
        get => (bool)GetValue(UseImageCacheProperty);
        set => SetValue(UseImageCacheProperty, value);
    }

    public bool ForceSoftwareDecoding
    {
        get => (bool)GetValue(ForceSoftwareDecodingProperty);
        set => SetValue(ForceSoftwareDecodingProperty, value);
    }

    public string? MediaSourceCacheDir
    {
        get => (string?)GetValue(MediaSourceCacheDirProperty);
        set => SetValue(MediaSourceCacheDirProperty, value);
    }

    public double CanvasWidth => (double)GetValue(CanvasWidthProperty);

    public double CanvasHeight => (double)GetValue(CanvasHeightProperty);

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MediaCacheHandlerProperty =
        DependencyProperty.Register(nameof(MediaCacheHandler),
                                    typeof(IMediaCacheHandler),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!));

    public static readonly DependencyProperty IsAudioEnabledProperty =
        DependencyProperty.Register(nameof(IsAudioEnabled),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false, PropertyIsAudioEnabled_OnChange));

    public static readonly DependencyProperty IsVideoAutoplayProperty =
        DependencyProperty.Register(nameof(IsVideoAutoplay),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(true));

    public static readonly DependencyProperty UseFfmpegDecoderProperty =
        DependencyProperty.Register(nameof(UseFfmpegDecoder),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false));

    public static readonly DependencyProperty FfmpegDecoderModeProperty =
        DependencyProperty.Register(nameof(FfmpegDecoderMode),
                                    typeof(VideoDecoderMode),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(VideoDecoderMode.Automatic));

    public static readonly DependencyProperty AudioVolumeProperty =
        DependencyProperty.Register(nameof(AudioVolume),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(.5d, PropertyAudioVolume_OnChange));

    public static readonly DependencyProperty IsParallaxEnabledProperty =
        DependencyProperty.Register(nameof(IsParallaxEnabled),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false, PropertyIsParallelEnabled_OnChange));

    public static readonly DependencyProperty ParallaxHorizontalShiftProperty =
        DependencyProperty.Register(nameof(ParallaxHorizontalShift),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty ParallaxVerticalShiftProperty =
        DependencyProperty.Register(nameof(ParallaxVerticalShift),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty ParallaxHoverSourceProperty =
        DependencyProperty.Register(nameof(ParallaxHoverSource),
                                    typeof(UIElement),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!, PropertyParallaxHoverSource_OnChange));

    public static readonly DependencyProperty ParallaxResetOnUnfocusedProperty =
        DependencyProperty.Register(nameof(ParallaxResetOnUnfocused),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(true));

    public static readonly DependencyProperty BackgroundSourceProperty =
        DependencyProperty.Register(nameof(BackgroundSource),
                                    typeof(object),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!, PropertyBackgroundSource_OnChange));

    public static readonly DependencyProperty BackgroundStretchProperty =
        DependencyProperty.Register(nameof(BackgroundStretch),
                                    typeof(Stretch),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(Stretch.UniformToFill));

    public static readonly DependencyProperty BackgroundHorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(BackgroundHorizontalAlignment),
                                    typeof(HorizontalAlignment),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(HorizontalAlignment.Center));

    public static readonly DependencyProperty BackgroundVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(BackgroundVerticalAlignment),
                                    typeof(VerticalAlignment),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(VerticalAlignment.Center));

    public static readonly DependencyProperty ForegroundSourceProperty =
        DependencyProperty.Register(nameof(ForegroundSource),
                                    typeof(object),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!, PropertyForegroundSource_OnChange));

    public static readonly DependencyProperty ForegroundStretchProperty =
        DependencyProperty.Register(nameof(ForegroundStretch),
                                    typeof(Stretch),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(Stretch.UniformToFill));

    public static readonly DependencyProperty ForegroundHorizontalAlignmentProperty =
        DependencyProperty.Register(nameof(ForegroundHorizontalAlignment),
                                    typeof(HorizontalAlignment),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(HorizontalAlignment.Center));

    public static readonly DependencyProperty ForegroundVerticalAlignmentProperty =
        DependencyProperty.Register(nameof(ForegroundVerticalAlignment),
                                    typeof(VerticalAlignment),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(VerticalAlignment.Center));

    public static readonly DependencyProperty ForegroundOpacityProperty =
        DependencyProperty.Register(nameof(ForegroundOpacity),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(1d));

    public static readonly DependencyProperty MediaStateProperty =
        DependencyProperty.Register(nameof(MediaState),
                                    typeof(MediaPlayerState),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(MediaPlayerState.Stopped));

    public static readonly DependencyProperty MediaDurationPositionProperty =
        DependencyProperty.Register(nameof(MediaDurationPosition),
                                    typeof(TimeSpan),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(TimeSpan.Zero, PropertyMediaDurationPosition_OnChange));

    public static readonly DependencyProperty MediaDurationProperty =
        DependencyProperty.Register(nameof(MediaDuration),
                                    typeof(TimeSpan),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty BackgroundElevationPixelsProperty =
        DependencyProperty.Register(nameof(BackgroundElevationPixels),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(32d));

    public static readonly DependencyProperty IsBackgroundElevatedProperty =
        DependencyProperty.Register(nameof(IsBackgroundElevated),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false, PropertyIsBackgroundElevated_OnChange));

    public static readonly DependencyProperty SmokeBackgroundProperty =
        DependencyProperty.Register(nameof(SmokeBackground),
                                    typeof(Brush),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!));

    public static readonly DependencyProperty SmokeOpacityProperty =
        DependencyProperty.Register(nameof(SmokeOpacity),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(0d));

    public static readonly DependencyProperty UseImageCacheProperty =
        DependencyProperty.Register(nameof(UseImageCache),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(true));

    public static readonly DependencyProperty ForceSoftwareDecodingProperty =
        DependencyProperty.Register(nameof(ForceSoftwareDecoding),
                                    typeof(bool),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(false));

    public static readonly DependencyProperty MediaSourceCacheDirProperty =
        DependencyProperty.Register(nameof(MediaSourceCacheDir),
                                    typeof(string),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(null!));

    public static readonly DependencyProperty CanvasWidthProperty =
        DependencyProperty.Register(nameof(CanvasWidth),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(0d));

    public static readonly DependencyProperty CanvasHeightProperty =
        DependencyProperty.Register(nameof(CanvasHeight),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(0d));

    #endregion
}
