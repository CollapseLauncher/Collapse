using Microsoft.UI.Xaml;
using System;
using System.IO;
using Windows.Storage.Streams;

namespace BackgroundTest.CustomControl.MediaSlideshow;

public partial class MediaSlideshow
{
    #region Properties

    /// <summary>
    /// List of media sources. This can be in forms of <see cref="Uri"/>, <see cref="string"/> (Either for URL or Local Path), <see cref="Stream"/> or <see cref="IRandomAccessStream"/>.
    /// </summary>
    public ManagedObservableList<object> MediaItems
    {
        get => (ManagedObservableList<object>)GetValue(MediaItemsProperty);
        set => SetValue(MediaItemsProperty, value);
    }

    /// <summary>
    /// The index of current media being displayed.
    /// </summary>
    public int CurrentMediaIndex
    {
        get => (int)GetValue(CurrentMediaIndexProperty);
        set => SetValue(CurrentMediaIndexProperty, value);
    }

    /// <summary>
    /// Determine how long the duration for the slideshow to switch into the next media.
    /// </summary>
    public double SlideshowDuration
    {
        get => (double)GetValue(SlideshowDurationProperty);
        set => SetValue(SlideshowDurationProperty, value);
    }

    /// <summary>
    /// Override <see cref="SlideshowDuration"/> and use video's own duration instead.
    /// </summary>
    public bool OverrideSlideshowDurationOnVideo
    {
        get => (bool)GetValue(OverrideSlideshowDurationOnVideoProperty);
        set => SetValue(OverrideSlideshowDurationOnVideoProperty, value);
    }

    /// <summary>
    /// Enable parallax effect on the media canvas.
    /// </summary>
    public bool EnableParallaxEffect
    {
        get => (bool)GetValue(EnableParallaxEffectProperty);
        set => SetValue(EnableParallaxEffectProperty, value);
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MediaItemsProperty =
        DependencyProperty.Register(nameof(MediaItems), typeof(ManagedObservableList<object>), typeof(MediaSlideshow),
                                    new PropertyMetadata(new ManagedObservableList<object>(), MediaItems_OnChanged));

    public static readonly DependencyProperty CurrentMediaIndexProperty =
        DependencyProperty.Register(nameof(CurrentMediaIndex), typeof(int), typeof(MediaSlideshow),
                                    new PropertyMetadata(0));

    public static readonly DependencyProperty SlideshowDurationProperty =
        DependencyProperty.Register(nameof(SlideshowDuration), typeof(double), typeof(MediaSlideshow),
                                    new PropertyMetadata(0));

    public static readonly DependencyProperty OverrideSlideshowDurationOnVideoProperty =
        DependencyProperty.Register(nameof(OverrideSlideshowDurationOnVideo), typeof(bool), typeof(MediaSlideshow),
                                    new PropertyMetadata(true));

    public static readonly DependencyProperty EnableParallaxEffectProperty =
        DependencyProperty.Register(nameof(EnableParallaxEffect), typeof(bool), typeof(MediaSlideshow),
                                    new PropertyMetadata(false));

    #endregion
}
