using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.Generic;
using System.Threading;

namespace BackgroundTest.CustomControl.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Properties

    /// <summary>
    /// List of elements to be displayed in the slideshow.
    /// </summary>
    public IList<UIElement> Items
    {
        get => (IList<UIElement>)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    /// <summary>
    /// The index of current element being displayed.
    /// </summary>
    public int ItemIndex
    {
        get => (int)GetValue(ItemIndexProperty);
        set
        {
            using (_atomicLock.EnterScope())
            {
                int itemsCount = Items.Count;
                if (itemsCount == 0)
                {
                    return;
                }

                if (value < 0)
                {
                    value = itemsCount - 1;
                }

                if (value >= itemsCount)
                {
                    value = 0;
                }

                SetValue(ItemIndexProperty, value);
            }
        }
    }

    /// <summary>
    /// The <see cref="Transition"/> used for transitioning between element.
    /// </summary>
    public Transition ItemTransition
    {
        get => (Transition)GetValue(ItemTransitionProperty);
        set => SetValue(ItemTransitionProperty, value);
    }

    /// <summary>
    /// Determine how long the duration for the slideshow to switch into the next panel. Set it to 0 to disable slideshow.
    /// </summary>
    public double SlideshowDuration
    {
        get => (double)GetValue(SlideshowDurationProperty);
        set => SetValue(SlideshowDurationProperty, value);
    }

    /// <summary>
    /// Toggle the progress bar visibility to show slideshow duration countdown.
    /// </summary>
    public Visibility ProgressBarVisibility
    {
        get => (Visibility)GetValue(ProgressBarVisibilityProperty);
        set => SetValue(ProgressBarVisibilityProperty, value);
    }

    #endregion

    #region Fields

    private readonly Lock _atomicLock = new();
    private bool _isMouseHover = false;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(UIElementCollection), typeof(PanelSlideshow),
                                    new PropertyMetadata(new ManagedObservableList<UIElement>(), Items_OnChange));

    public static readonly DependencyProperty ItemIndexProperty =
        DependencyProperty.Register(nameof(ItemIndex), typeof(int), typeof(PanelSlideshow),
                                    new PropertyMetadata(0, ItemIndex_OnChange));

    public static readonly DependencyProperty ItemTransitionProperty =
        DependencyProperty.Register(nameof(ItemTransition), typeof(Transition), typeof(PanelSlideshow),
                                    new PropertyMetadata(new PopupThemeTransition(), ItemTransition_OnChange));

    public static readonly DependencyProperty SlideshowDurationProperty =
        DependencyProperty.Register(nameof(SlideshowDuration), typeof(double), typeof(PanelSlideshow),
                                    new PropertyMetadata(0, SlideshowDuration_OnChange));

    public static readonly DependencyProperty ProgressBarVisibilityProperty =
        DependencyProperty.Register(nameof(ProgressBarVisibility), typeof(Visibility), typeof(PanelSlideshow),
                                    new PropertyMetadata(Visibility.Visible));

    #endregion
}
