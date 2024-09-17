// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//// Image loading reference from https://blogs.windows.com/buildingapps/2017/07/18/working-brushes-content-xaml-visual-layer-interop-part-one/#MA0k4EYWzqGKV501.97

using CommunityToolkit.WinUI.Media;
using CanvasBlendEffect = Microsoft.Graphics.Canvas.Effects.BlendEffect;

#if WINUI2
using Windows.UI.Composition;
using Windows.UI.Xaml.Media.Imaging;
#endif

#nullable enable
namespace Hi3Helper.CommunityToolkit.WinUI.Media;

/// <summary>
/// Brush which blends a <see cref="BitmapImage"/> to the Backdrop in a given mode. See http://microsoft.github.io/Win2D/html/T_Microsoft_Graphics_Canvas_Effects_BlendEffect.htm.
/// </summary>
public partial class ImageBlendBrush : XamlCompositionBrushBase
{
    private LoadedImageSurface? _surface;
    private CompositionSurfaceBrush? _surfaceBrush;

    /// <summary>
    /// Gets or sets the <see cref="BitmapImage"/> source of the image to composite.
    /// </summary>
    public Uri SourceUri
    {
        get => (Uri)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Source"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(SourceUri),
        typeof(Uri), // We use ImageSource type so XAML engine will automatically construct proper object from String.
        typeof(ImageBlendBrush),
        new PropertyMetadata(null, OnImageSourceChanged));

    /// <summary>
    /// Gets or sets how to stretch the image within the brush.
    /// </summary>
    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Stretch"/> dependency property.
    /// Requires 16299 or higher for modes other than None.
    /// </summary>
    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(Stretch),
        typeof(ImageBlendBrush),
        new PropertyMetadata(Stretch.None, OnStretchChanged));

    /// <summary>
    /// Gets or sets how to blend the image with the backdrop.
    /// </summary>
    public ImageBlendMode Mode
    {
        get => (ImageBlendMode)GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Mode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(
        nameof(Mode),
        typeof(ImageBlendMode),
        typeof(ImageBlendBrush),
        new PropertyMetadata(ImageBlendMode.Multiply, OnModeChanged));

    private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var brush = (ImageBlendBrush)d;

        // Unbox and update surface if CompositionBrush exists
        if (brush._surfaceBrush != null)
        {
            // If UriSource is invalid, StartLoadFromUri will return a blank texture.
            var uri = (e.NewValue as BitmapImage)?.UriSource ?? new Uri("ms-appx:///");
            var newSurface = LoadedImageSurface.StartLoadFromUri(uri);

            brush._surface = newSurface;
            brush._surfaceBrush.Surface = newSurface;
        }
        else
        {
            // If we didn't initially have a valid surface, we need to recreate our effect now.
            brush.OnDisconnected();
            brush.OnConnected();
        }
    }

    private static void OnStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var brush = (ImageBlendBrush)d;

        // Unbox and update surface if CompositionBrush exists
        if (brush._surfaceBrush != null)
        {
            // Modify the stretch property on our brush.
            brush._surfaceBrush.Stretch = CompositionStretchFromStretch((Stretch)e.NewValue);
        }
    }

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var brush = (ImageBlendBrush)d;

        // We can't animate our enum properties so recreate our internal brush.
        brush.OnDisconnected();
        brush.OnConnected();
    }

    /// <inheritdoc/>
    protected override void OnConnected()
    {
#if WINUI2
        var compositor = Window.Current.Compositor;
#elif WINUI3
        var compositor = CompositionTarget.GetCompositorForCurrentThread();
#endif

        // Delay creating composition resources until they're required.
        if (CompositionBrush == null && SourceUri != null && SourceUri is Uri bitmapUri)
        {
            // Use LoadedImageSurface API to get ICompositionSurface from image uri provided
            // If UriSource is invalid, StartLoadFromUri will return a blank texture.
            _surface = LoadedImageSurface.StartLoadFromUri(bitmapUri);

            // Load Surface onto SurfaceBrush
            _surfaceBrush = compositor.CreateSurfaceBrush(_surface);
            _surfaceBrush.Stretch = CompositionStretchFromStretch(Stretch);

#if WINUI2
            var compositionCapabilities = CompositionCapabilities.GetForCurrentView();
#else
            var compositionCapabilities = new CompositionCapabilities();
#endif
            // Abort if effects aren't supported.
            if (!compositionCapabilities.AreEffectsSupported())
            {
                // Just use image straight-up, if we don't support effects.
                CompositionBrush = _surfaceBrush;
                return;
            }

            var backdrop = compositor.CreateBackdropBrush();

            // Use a Win2D invert affect applied to a CompositionBackdropBrush.
            var graphicsEffect = new CanvasBlendEffect
            {
                Name = "Invert",
                Mode = (BlendEffectMode)(int)Mode,
                Background = new CompositionEffectSourceParameter("backdrop"),
                Foreground = new CompositionEffectSourceParameter("image")
            };

            var effectFactory = compositor.CreateEffectFactory(graphicsEffect);
            var effectBrush = effectFactory.CreateBrush();

            effectBrush.SetSourceParameter("backdrop", backdrop);
            effectBrush.SetSourceParameter("image", _surfaceBrush);

            CompositionBrush = effectBrush;
        }
    }

    /// <inheritdoc/>
    protected override void OnDisconnected()
    {
        // Dispose of composition resources when no longer in use.
        if (CompositionBrush != null)
        {
            CompositionBrush.Dispose();
            CompositionBrush = null;
        }

        if (_surfaceBrush != null)
        {
            _surfaceBrush.Dispose();
            _surfaceBrush = null;
        }

        if (_surface != null)
        {
            _surface.Dispose();
            _surface = null;
        }
    }

    //// Helper to allow XAML developer to use XAML stretch property rather than another enum.
    private static CompositionStretch CompositionStretchFromStretch(Stretch value)
    {
        switch (value)
        {
            case Stretch.None:
                return CompositionStretch.None;
            case Stretch.Fill:
                return CompositionStretch.Fill;
            case Stretch.Uniform:
                return CompositionStretch.Uniform;
            case Stretch.UniformToFill:
                return CompositionStretch.UniformToFill;
        }

        return CompositionStretch.None;
    }
}