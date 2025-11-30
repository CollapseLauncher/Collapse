using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Xaml.Interactivity;
using System;
using System.Linq;
using System.Threading;

namespace BackgroundTest.CustomControl.NewPipsPager;

public partial class NewPipsPager
{
    #region Properties

    private int[] _itemsDummy = [];

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public int ItemsCount
    {
        get => (int)GetValue(ItemsCountProperty);
        set => SetValue(ItemsCountProperty, value);
    }

    public int ItemIndex
    {
        get => (int)GetValue(ItemIndexProperty);
        set
        {
            using (_atomicLock.EnterScope())
            {
                int itemsCount = ItemsCount;
                if (itemsCount == 0)
                {
                    value = -1;
                    SetValue(ItemIndexProperty, value);
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

    public Visibility ItemCounterIndicatorVisibility
    {
        get => (Visibility)GetValue(ItemCounterIndicatorVisibilityProperty);
        set => SetValue(ItemCounterIndicatorVisibilityProperty, value);
    }

    public NewPipsPagerNavigationMode PreviousNavigationButtonMode
    {
        get => (NewPipsPagerNavigationMode)GetValue(PreviousNavigationButtonModeProperty);
        set => SetValue(PreviousNavigationButtonModeProperty, value);
    }

    public NewPipsPagerNavigationMode NextNavigationButtonMode
    {
        get => (NewPipsPagerNavigationMode)GetValue(NextNavigationButtonModeProperty);
        set => SetValue(NextNavigationButtonModeProperty, value);
    }

    #endregion

    #region Fields

    private readonly Lock _atomicLock = new();
    #endregion

    #region Dependency Change Methods

    private static void Orientation_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        NewPipsPager pager = (NewPipsPager)d;
        Orientation orientation = (Orientation)e.NewValue;

        Orientation_OnChange(pager, orientation);
    }

    private static void Orientation_OnChange(NewPipsPager pager, Orientation orientation)
    {
        string state = orientation == Orientation.Vertical
            ? "VerticalOrientationView"
            : "HorizontalOrientationView";
        VisualStateManager.GoToState(pager, state, true);
    }

    private static void ItemsCount_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        object obj   = e.NewValue;
        int    value = 0;
        if (obj is int asInt)
        {
            value = asInt;
        }
        ItemsCount_OnChange((NewPipsPager)d, value);
    }

    private static void ItemsCount_OnChange(NewPipsPager pager, int value)
    {
        if (value < 0)
        {
            throw new IndexOutOfRangeException("ItemsCount cannot be negative!");
        }

        if (pager._pipsPagerItemsRepeater == null)
        {
            return;
        }

        try
        {
            if (value == 0)
            {
                pager._itemsDummy = [];
                return;
            }

            pager._itemsDummy = Enumerable.Range(0, value).ToArray();
        }
        finally
        {
            // Update index
            pager.ItemIndex = 0;

            // Update ItemsSource if already assigned
            pager._pipsPagerItemsRepeater.ItemsSource = pager._itemsDummy;
        }
    }

    private static void ItemIndex_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        NewPipsPager pager = (NewPipsPager)d;

        UpdatePreviousButtonVisualState(pager);
        UpdateNextButtonVisualState(pager);
    }

    private static void UpdatePreviousButtonVisualState(NewPipsPager pager)
    {
        NewPipsPagerNavigationMode mode = pager.PreviousNavigationButtonMode;
        if (mode == NewPipsPagerNavigationMode.Hidden)
        {
            VisualStateManager.GoToState(pager, "PreviousPageButtonCollapsed", true);
            return;
        }

        if (pager._previousPageButton == null)
        {
            return;
        }

        pager._previousPageButton!.IsEnabled = true;
        VisualStateManager.GoToState(pager, "PreviousPageButtonVisible", true);
        if (mode == NewPipsPagerNavigationMode.Visible)
        {
            return;
        }

        if (pager.ItemIndex <= 0)
        {
            pager._previousPageButton!.IsEnabled = false;
            VisualStateManager.GoToState(pager, "PreviousPageButtonHidden", true);
        }
    }

    private static void UpdateNextButtonVisualState(NewPipsPager pager)
    {
        NewPipsPagerNavigationMode mode = pager.NextNavigationButtonMode;
        if (mode == NewPipsPagerNavigationMode.Hidden)
        {
            VisualStateManager.GoToState(pager, "NextPageButtonCollapsed", true);
            return;
        }

        if (pager._nextPageButton == null)
        {
            return;
        }

        pager._nextPageButton.IsEnabled = true;
        VisualStateManager.GoToState(pager, "NextPageButtonVisible", true);
        if (mode == NewPipsPagerNavigationMode.Visible)
        {
            return;
        }

        if (pager.ItemIndex + 1 >= pager.ItemsCount)
        {
            pager._nextPageButton!.IsEnabled = false;
            VisualStateManager.GoToState(pager, "NextPageButtonHidden", true);
        }
    }

    private static void PreviousNavigationButtonMode_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        UpdatePreviousButtonVisualState((NewPipsPager)d);
    }

    private static void NextNavigationButtonMode_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        UpdateNextButtonVisualState((NewPipsPager)d);
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation), typeof(NewPipsPager), new PropertyMetadata(Orientation.Vertical, Orientation_OnChange));
    public static readonly DependencyProperty ItemsCountProperty = DependencyProperty.Register(nameof(ItemsCount), typeof(int), typeof(NewPipsPager), new PropertyMetadata(0, ItemsCount_OnChange));
    public static readonly DependencyProperty ItemIndexProperty = DependencyProperty.Register(nameof(ItemIndex), typeof(int), typeof(NewPipsPager), new PropertyMetadata(-1, ItemIndex_OnChange));
    public static readonly DependencyProperty ItemCounterIndicatorVisibilityProperty = DependencyProperty.Register(nameof(ItemCounterIndicatorVisibility), typeof(Visibility), typeof(NewPipsPager), new PropertyMetadata(Visibility.Visible));
    public static readonly DependencyProperty PreviousNavigationButtonModeProperty = DependencyProperty.Register(nameof(PreviousNavigationButtonMode), typeof(NewPipsPagerNavigationMode), typeof(NewPipsPager), new PropertyMetadata(NewPipsPagerNavigationMode.Auto, PreviousNavigationButtonMode_OnChange));
    public static readonly DependencyProperty NextNavigationButtonModeProperty = DependencyProperty.Register(nameof(NextNavigationButtonMode), typeof(NewPipsPagerNavigationMode), typeof(NewPipsPager), new PropertyMetadata(NewPipsPagerNavigationMode.Auto, NextNavigationButtonMode_OnChange));

    #endregion
}
