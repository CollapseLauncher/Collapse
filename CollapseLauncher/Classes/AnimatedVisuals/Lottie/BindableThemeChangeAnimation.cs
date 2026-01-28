using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.AnimatedVisuals.Lottie;

public abstract class BindableThemeChangeAnimation : DependencyObject
{
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Dependency property for Foreground.
    /// </summary>
    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register(nameof(Foreground), typeof(Brush), typeof(BindableThemeChangeAnimation),
                                    new PropertyMetadata(null!, OnForegroundChanged));

    protected static void OnForegroundChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
        => ((BindableThemeChangeAnimation)d).OnForegroundChanged(args.NewValue as Brush);

    protected abstract void OnForegroundChanged(Brush? brush);
}
