using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;

/// <summary>
/// An acrylic-like XAML brush that samples an explicit composition surface
/// instead of the compositor backdrop.
/// </summary>
public sealed partial class CompositionSurfaceAcrylicBrush : XamlCompositionBrushBase
{
    private ICompositionSurface?     _source;
    private CompositionSurfaceBrush? _surfaceBrush;
    private bool                     _isConnected;

    public static readonly DependencyProperty BlurAmountProperty = DependencyProperty.Register(
        nameof(BlurAmount),
        typeof(double),
        typeof(CompositionSurfaceAcrylicBrush),
        new PropertyMetadata(16d, OnEffectPropertyChanged));

    public static readonly DependencyProperty SaturationProperty = DependencyProperty.Register(
        nameof(Saturation),
        typeof(double),
        typeof(CompositionSurfaceAcrylicBrush),
        new PropertyMetadata(1d, OnEffectPropertyChanged));

    public static readonly DependencyProperty TintColorProperty = DependencyProperty.Register(
        nameof(TintColor),
        typeof(Color),
        typeof(CompositionSurfaceAcrylicBrush),
        new PropertyMetadata(Colors.Black, OnEffectPropertyChanged));

    public static readonly DependencyProperty TintOpacityProperty = DependencyProperty.Register(
        nameof(TintOpacity),
        typeof(double),
        typeof(CompositionSurfaceAcrylicBrush),
        new PropertyMetadata(0.15d, OnEffectPropertyChanged));

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
        nameof(Stretch),
        typeof(CompositionStretch),
        typeof(CompositionSurfaceAcrylicBrush),
        new PropertyMetadata(CompositionStretch.UniformToFill, OnStretchChanged));

    public CompositionSurfaceAcrylicBrush()
    {
        FallbackColor = Colors.Black;
        RegisterPropertyChangedCallback(FallbackColorProperty, OnFallbackColorChanged);
    }

    /// <summary>
    /// Gets or sets the surface sampled by this brush. Set this from the UI thread.
    /// The brush does not own or dispose the surface.
    /// </summary>
    public ICompositionSurface? Source
    {
        get => _source;
        set
        {
            if (ReferenceEquals(_source, value))
                return;

            _source = value;
            if (_isConnected)
                RebuildCompositionBrush();
        }
    }

    /// <summary>The blur radius, in DIPs.</summary>
    public double BlurAmount
    {
        get => (double)GetValue(BlurAmountProperty);
        set => SetValue(BlurAmountProperty, value);
    }

    /// <summary>The saturation multiplier applied after the blur.</summary>
    public double Saturation
    {
        get => (double)GetValue(SaturationProperty);
        set => SetValue(SaturationProperty, value);
    }

    /// <summary>The color composited over the blurred surface.</summary>
    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    /// <summary>The opacity of <see cref="TintColor"/>.</summary>
    public double TintOpacity
    {
        get => (double)GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    /// <summary>How the source surface is mapped into the painted element.</summary>
    public CompositionStretch Stretch
    {
        get => (CompositionStretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    protected override void OnConnected()
    {
        if (_isConnected)
            return;

        _isConnected = true;
        RebuildCompositionBrush();
    }

    protected override void OnDisconnected()
    {
        if (!_isConnected)
            return;

        _isConnected = false;
        DisposeCompositionResources();
    }

    private static void OnEffectPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        CompositionSurfaceAcrylicBrush brush = (CompositionSurfaceAcrylicBrush)sender;
        if (brush._isConnected)
            brush.RebuildCompositionBrush();
    }

    private static void OnStretchChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        CompositionSurfaceAcrylicBrush brush = (CompositionSurfaceAcrylicBrush)sender;
        if (brush._surfaceBrush != null)
            brush._surfaceBrush.Stretch = (CompositionStretch)args.NewValue;
    }

    private static void OnFallbackColorChanged(DependencyObject sender, DependencyProperty property)
    {
        CompositionSurfaceAcrylicBrush brush = (CompositionSurfaceAcrylicBrush)sender;
        if (brush._isConnected && brush._source == null)
            brush.RebuildCompositionBrush();
    }

    private void RebuildCompositionBrush()
    {
        DisposeCompositionResources();
        if (!_isConnected)
            return;

        Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();
        if (_source == null)
        {
            CompositionBrush = compositor.CreateColorBrush(FallbackColor);
            return;
        }

        _surfaceBrush                          = compositor.CreateSurfaceBrush(_source);
        _surfaceBrush.Stretch                  = Stretch;
        _surfaceBrush.HorizontalAlignmentRatio = 0.5f;
        _surfaceBrush.VerticalAlignmentRatio   = 0.5f;

        CompositionCapabilities capabilities = new();
        if (!capabilities.AreEffectsSupported())
        {
            CompositionBrush = _surfaceBrush;
            return;
        }

        GaussianBlurEffect blurEffect = new()
        {
            BlurAmount   = (float)Math.Clamp(BlurAmount, 0d, 250d),
            BorderMode   = EffectBorderMode.Hard,
            Optimization = EffectOptimization.Balanced,
            Source       = new CompositionEffectSourceParameter("Surface")
        };

        SaturationEffect saturationEffect = new()
        {
            Saturation = (float)Math.Clamp(Saturation, 0d, 2d),
            Source     = blurEffect
        };

        Color tintColor = TintColor;
        tintColor.A = (byte)Math.Round(255d * Math.Clamp(TintOpacity, 0d, 1d));
        ColorSourceEffect tintEffect = new()
        {
            Color = tintColor
        };

        CompositeEffect acrylicEffect = new()
        {
            Mode = CanvasComposite.SourceOver
        };
        acrylicEffect.Sources.Add(saturationEffect);
        acrylicEffect.Sources.Add(tintEffect);

        using CompositionEffectFactory effectFactory = compositor.CreateEffectFactory(acrylicEffect);
        CompositionEffectBrush effectBrush = effectFactory.CreateBrush();
        effectBrush.SetSourceParameter("Surface", _surfaceBrush);
        CompositionBrush = effectBrush;
    }

    private void DisposeCompositionResources()
    {
        CompositionBrush? compositionBrush = CompositionBrush;
        CompositionBrush = null;

        if (compositionBrush != null && !ReferenceEquals(compositionBrush, _surfaceBrush))
            compositionBrush.Dispose();

        _surfaceBrush?.Dispose();
        _surfaceBrush = null;
    }
}
