using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.FullPageOverlay;

public enum FullPageOverlaySize
{
    /// <summary>
    /// Stretch to full, with 16px gap.
    /// </summary>
    Full,
    
    /// <summary>
    /// Set the size to follow the content.
    /// </summary>
    FollowContent,

    /// <summary>
    /// Stretch to full, with no gap.
    /// </summary>
    FullNoGap
}

public partial class FullPageOverlay
{
    #region Fields
    public  Button?           LayoutCloseButton;
    private Rectangle?        _layoutSmokeBackgroundGrid;
    private ContentPresenter? _layoutContentPresenter;
    private Grid?             _layoutOverlayTitleGrid;

    private readonly bool _isAlwaysOnTop;
    private          Grid _parentOverlayGrid;

    private CancellationTokenSource? _closeCts;

    public static readonly List<FullPageOverlay> CurrentlyOpenedOverlays = [];
    private static readonly Lock ThisThreadLock = new();
    #endregion

    #region Properties
    /// <summary>
    /// Defines the size of the overlay frame.
    /// </summary>
    public FullPageOverlaySize Size
    {
        get => (FullPageOverlaySize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Sets the background of the content frame.
    /// </summary>
    public Brush? ContentBackground
    {
        get => (Brush?)GetValue(ContentBackgroundProperty);
        set => SetValue(ContentBackgroundProperty, value);
    }

    /// <summary>
    /// Sets the icon source of the overlay
    /// </summary>
    public IconSource? OverlayTitleIcon
    {
        get => (IconSource?)GetValue(OverlayTitleIconProperty);
        set => SetValue(OverlayTitleIconProperty, value);
    }

    /// <summary>
    /// Sets the title of the overlay
    /// </summary>
    public string? OverlayTitle
    {
        get => (string?)GetValue(OverlayTitleProperty);
        set => SetValue(OverlayTitleProperty, value);
    }

    /// <summary>
    /// Sets the visibility of the close button
    /// </summary>
    public Visibility CloseButtonVisibility
    {
        get => (Visibility)GetValue(CloseButtonVisibilityProperty);
        set => SetValue(CloseButtonVisibilityProperty, value);
    }

    /// <summary>
    /// Sets the source callback to get the title of the overlay
    /// </summary>
    public Func<string?>? OverlayTitleSource
    {
        get;
        set;
    }
    #endregion

    #region Callback Methods
    private static void OnUILayoutLoaded(object sender, RoutedEventArgs e)
    {
        // NOP
    }

    private static void OnUILayoutUnloaded(object sender, RoutedEventArgs e)
    {
        using (ThisThreadLock.EnterScope())
        {
            if (sender is not FullPageOverlay asOverlay)
            {
                return;
            }

            asOverlay.UnassignEvents();
            asOverlay.OverlayTitleSource = null;
            asOverlay.OverlayTitle       = null;
            asOverlay.OverlayTitleIcon   = null;
            asOverlay.ContentBackground  = null;
            asOverlay.Content            = null;

            asOverlay._closeCts?.Dispose();
            Interlocked.Exchange(ref asOverlay._closeCts,                   null);
            Interlocked.Exchange(ref asOverlay._parentOverlayGrid!,         null);
            Interlocked.Exchange(ref asOverlay.LayoutCloseButton!,          null);
            Interlocked.Exchange(ref asOverlay._layoutSmokeBackgroundGrid!, null);
            Interlocked.Exchange(ref asOverlay._layoutContentPresenter!,    null);
            Interlocked.Exchange(ref asOverlay._layoutOverlayTitleGrid!,    null);

            GC.Collect();
        }
    }
    #endregion

    #region DependencyProperty
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(nameof(Size), typeof(FullPageOverlaySize), typeof(FullPageOverlay), new PropertyMetadata(FullPageOverlaySize.Full, AssignContentFrameSizeChanged));
    public static readonly DependencyProperty ContentBackgroundProperty = DependencyProperty.Register(nameof(ContentBackground), typeof(Brush), typeof(FullPageOverlay), new PropertyMetadata(null));
    public static readonly DependencyProperty OverlayTitleIconProperty = DependencyProperty.Register(nameof(OverlayTitleIcon), typeof(IconSource), typeof(FullPageOverlay), new PropertyMetadata(new FontIconSource { Glyph = "\uE80A" }));
    public static readonly DependencyProperty OverlayTitleProperty = DependencyProperty.Register(nameof(OverlayTitle), typeof(string), typeof(FullPageOverlay), new PropertyMetadata(null));
    public static readonly DependencyProperty CloseButtonVisibilityProperty = DependencyProperty.Register(nameof(CloseButtonVisibility), typeof(Visibility), typeof(FullPageOverlay), new PropertyMetadata(Visibility.Visible));
    #endregion
}
