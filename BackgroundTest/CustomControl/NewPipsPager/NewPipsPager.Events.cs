using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.System;

namespace BackgroundTest.CustomControl.NewPipsPager;

public partial class NewPipsPager
{
    #region Size Measure Override

    protected override Size MeasureOverride(Size parentSize)
    {
        Vector2 containerSize = _pipsPagerItemsRepeater.ActualSize;
        double  containerTotalWidth  = containerSize.X;
        double  containerTotalHeight = containerSize.Y;

        GetNavigationButtonTotalSize(PreviousNavigationButtonMode,
                                     _previousPageButton,
                                     out double buttonPrevSizeTotalWidth,
                                     out double buttonPrevSizeTotalHeight);

        GetNavigationButtonTotalSize(NextNavigationButtonMode,
                                     _nextPageButton,
                                     out double buttonNextSizeTotalWidth,
                                     out double buttonNextSizeTotalHeight);

        Vector2 scrollViewportSize        = parentSize.ToVector2();
        double  scrollViewportTotalWidth  = scrollViewportSize.X;
        double  scrollViewportTotalHeight = scrollViewportSize.Y;

        Size   selfSize              = base.MeasureOverride(parentSize);
        double totalCalculatedWidth  = selfSize.Width;
        double totalCalculatedHeight = selfSize.Height;

        if (Orientation == Orientation.Horizontal)
        {
            _pipsPagerScrollViewer.MaxWidth =
                GetViewportSize(scrollViewportTotalWidth,
                                containerTotalWidth,
                                buttonPrevSizeTotalWidth,
                                buttonNextSizeTotalWidth,
                                _pipsButtonSize,
                                out bool isContainerLarger);

            totalCalculatedWidth = 0;
            totalCalculatedWidth += !isContainerLarger
                ? containerTotalWidth
                : _pipsPagerScrollViewer.MaxWidth;
            totalCalculatedWidth += buttonPrevSizeTotalWidth +
                                    buttonNextSizeTotalWidth;
        }
        else
        {
            _pipsPagerScrollViewer.MaxHeight =
                GetViewportSize(scrollViewportTotalHeight,
                                containerTotalHeight,
                                buttonPrevSizeTotalHeight,
                                buttonNextSizeTotalHeight,
                                _pipsButtonSize,
                                out bool isContainerLarger);

            totalCalculatedHeight = 0;
            totalCalculatedHeight += !isContainerLarger
                ? containerTotalHeight
                : _pipsPagerScrollViewer.MaxHeight;
            totalCalculatedHeight += buttonPrevSizeTotalHeight +
                                     buttonNextSizeTotalHeight;
        }

        return new Size(totalCalculatedWidth, totalCalculatedHeight);
    }

    private static void GetNavigationButtonTotalSize(
        NewPipsPagerNavigationMode mode,
        Button                     element,
        out double                 width,
        out double                 height)
    {
        bool      isVisible = mode != NewPipsPagerNavigationMode.Hidden;
        Vector2   size      = isVisible ? element.ActualSize : Vector2.Zero;
        Thickness margin    = isVisible ? element.Margin : default;
        width  = size.X + margin.Left + margin.Right;
        height = size.Y + margin.Top + margin.Bottom;
    }

    private static double GetViewportSize(double   initialViewportSize,
                                          double   initialContainerSize,
                                          double   previousSideElementSize,
                                          double   nextSideElementSize,
                                          double   perButtonSize,
                                          out bool isContainerLarger)
    {
        // Decrease viewport based on total width of navigation buttons
        initialViewportSize -= previousSideElementSize + nextSideElementSize;
        isContainerLarger   =  initialContainerSize > initialViewportSize;

        // Clamp to display only viewable pips
        if (isContainerLarger)
        {
            double dividedPerButtonSize = Math.Floor(initialViewportSize / perButtonSize);
            initialViewportSize = dividedPerButtonSize * perButtonSize;
            int clampedNth = (int)dividedPerButtonSize;
            if (clampedNth % 2 == 0)
            {
                initialViewportSize = (clampedNth - 1) * perButtonSize;
            }

            return Math.Max(initialViewportSize, 0);
        }

        return double.PositiveInfinity;
    }

    #endregion

    #region UI Events - Navigation Buttons

    private void PreviousPageButton_OnClick(object sender, RoutedEventArgs e) => ItemIndex--;

    private void NextPageButton_OnClick(object sender, RoutedEventArgs e) => ItemIndex++;

    private void KeyboardKeys_Pressed(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.Up:
                PreviousPageButton_OnClick(sender, e);
                break;
            case VirtualKey.Right:
            case VirtualKey.Down:
                NextPageButton_OnClick(sender, e);
                break;
        }
    }

    #endregion

    #region UI Events - Orientation

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

    #endregion

    #region Property Changes

    private static void ItemsCount_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NewPipsPager pager)
        {
            return;
        }

        object obj   = e.NewValue;
        int    value = 0;
        if (obj is int asInt)
        {
            value = asInt;
        }

        pager.ItemsCount_OnChange(value);
    }

    private void ItemsCount_OnChange(int value)
    {
        if (!_isTemplateLoaded)
        {
            return;
        }

        if (value < 0)
        {
            throw new IndexOutOfRangeException("ItemsCount cannot be negative!");
        }

        int oldItemsCount = _itemsDummy.Length;

        using (_atomicLock.EnterScope())
        {
            try
            {

                if (value == 0)
                {
                    _itemsDummy = [];
                    return;
                }

                _itemsDummy = Enumerable.Range(0, value).ToArray();
            }
            finally
            {
                // Update ItemsSource if already assigned
                _pipsPagerItemsRepeater.ItemsSource = _itemsDummy;
                _pipsPagerItemsRepeater.UpdateLayout();

                // Update index only if the count is invalid
                int currentIndex = ItemIndex;
                if (currentIndex > value ||
                    (value != 0 && currentIndex < 0))
                {
                    ItemIndex = 0;
                }

                ItemsCountChanged?.Invoke(this, new ChangedStructItemArgs<int>(oldItemsCount, value));
            }
        }
    }

    private static void ItemIndex_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Ignore if the old and new index are equal
        if (e is not { NewValue: int asNewIndex, OldValue: int asOldIndex } ||
            asNewIndex == asOldIndex)
        {
            return;
        }

        NewPipsPager pager = (NewPipsPager)d;

        // Update navigation buttons state
        UpdatePreviousButtonVisualState(pager);
        UpdateNextButtonVisualState(pager);

        // Update pip buttons state
        UpdateAndBringSelectedPipToView(pager, asNewIndex, asOldIndex);
    }

    #endregion

    #region ItemsRepeater

    private void ItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Button asButton)
        {
            return;
        }

        if (asButton.Tag is not int asIndex)
        {
            return;
        }

        AssignPipButtonStyle(asButton,
                             asIndex != ItemIndex
                                 ? PipButtonStyleNormal
                                 : PipButtonStyleSelected);

        // Avoid redundant loaded + unloaded events assignment
        if (asButton.IsLoaded)
        {
            return;
        }

        asButton.Loaded   += ItemsRepeaterPipButton_LoadedEvent;
        asButton.Unloaded += ItemsRepeaterPipButton_UnloadedEvent;
    }

    private void ItemsRepeater_OnSizeChanged(object sender, SizeChangedEventArgs e) => InvalidateMeasure();

    private void ItemsRepeaterPipButton_UnloadedEvent(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        if (SelectionMode == NewPipsPagerSelectionMode.Click)
        {
            button.Click -= ItemsRepeaterPipButton_OnClick;
        }
        else
        {
            button.PointerEntered -= ItemsRepeaterPipButton_OnClick;
        }

        button.Loaded   -= ItemsRepeaterPipButton_LoadedEvent;
        button.Unloaded -= ItemsRepeaterPipButton_UnloadedEvent;
    }

    private void ItemsRepeaterPipButton_LoadedEvent(object sender, RoutedEventArgs e)
    {
        Button button = (Button)sender;
        if (SelectionMode == NewPipsPagerSelectionMode.Click)
        {
            button.Click += ItemsRepeaterPipButton_OnClick;
        }
        else
        {
            button.PointerEntered += ItemsRepeaterPipButton_OnClick;
        }
    }

    private void ItemsRepeaterPipButton_OnClick(object sender, RoutedEventArgs args)
    {
        ItemIndex = (int)((Button)sender).Tag;
    }

    #endregion

    #region ScrollViewer

    private void ScrollViewer_OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Pointer.IsInRange ||
            sender is not UIElement element)
        {
            return;
        }

        PointerPoint pointer      = e.GetCurrentPoint(element);
        int          orientation  = pointer.Properties.MouseWheelDelta;
        bool         isHorizontal = Orientation == Orientation.Horizontal;
        double       delta        = _pipsButtonSize * (orientation / 120d);

        double toOffset = (isHorizontal
            ? _pipsPagerScrollViewer.HorizontalOffset
            : _pipsPagerScrollViewer.VerticalOffset) + -delta;
        toOffset = Math.Round(toOffset / _pipsButtonSize) * _pipsButtonSize;

        if (isHorizontal)
        {
            toOffset = Math.Clamp(toOffset, 0, _pipsPagerScrollViewer.ExtentWidth);
            _pipsPagerScrollViewer.ChangeView(toOffset, _pipsPagerScrollViewer.VerticalOffset, _pipsPagerScrollViewer.ZoomFactor);
        }
        else
        {
            toOffset = Math.Clamp(toOffset, 0, _pipsPagerScrollViewer.ExtentHeight);
            _pipsPagerScrollViewer.ChangeView(_pipsPagerScrollViewer.HorizontalOffset, toOffset, _pipsPagerScrollViewer.ZoomFactor);
        }
    }

    #endregion

    #region Loaded and Unloaded

    private void NewPipsPager_Unloaded(object sender, RoutedEventArgs e)
    {
        UnapplyNavigationButtonEvents();
        UnapplyKeyPressEvents();
        UnapplyItemsRepeaterEvents();

        Loaded   -= NewPipsPager_Loaded;
        Unloaded -= NewPipsPager_Unloaded;

        _pipsPagerItemsRepeater.ItemsSource = null;
    }

    private void NewPipsPager_Loaded(object sender, RoutedEventArgs e)
    {
        ItemsCount_OnChange(ItemsCount);
        UpdateAndBringSelectedPipToView(this, ItemIndex, -1);

        if (sender is not NewPipsPager pager)
        {
            return;
        }

        // Update navigation buttons state
        UpdatePreviousButtonVisualState(pager);
        UpdateNextButtonVisualState(pager);
    }

    #endregion
}
