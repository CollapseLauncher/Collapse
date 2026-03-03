using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.System;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.NewPipsPager;

public partial class NewPipsPager
{
    #region Size Measure Override

    protected override Size MeasureOverride(Size parentSize)
    {
        Orientation orientation = Orientation;
        double pipsButtonSize = GetButtonSize(orientation);

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

        if (orientation == Orientation.Horizontal)
        {
            _pipsPagerScrollViewer.MaxWidth =
                GetViewportSize(scrollViewportTotalWidth,
                                containerTotalWidth,
                                buttonPrevSizeTotalWidth,
                                buttonNextSizeTotalWidth,
                                pipsButtonSize,
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
                                pipsButtonSize,
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
        if (!isContainerLarger) return double.PositiveInfinity;

        double dividedPerButtonSize = Math.Floor(initialViewportSize / perButtonSize);
        initialViewportSize = dividedPerButtonSize * perButtonSize;
        int clampedNth = (int)dividedPerButtonSize;
        if (clampedNth % 2 == 0)
        {
            initialViewportSize = (clampedNth - 1) * perButtonSize;
        }

        // Avoid NaN or Infinite
        initialViewportSize = Math.Max(Math.Abs(initialViewportSize), 0);
        if (!double.IsFinite(initialViewportSize))
        {
            initialViewportSize = 0;
        }

        return initialViewportSize;

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
        if (!pager.IsLoaded)
        {
            return;
        }

        // Update navigation buttons state
        UpdatePreviousButtonVisualState(pager);
        UpdateNextButtonVisualState(pager);

        // Update pip buttons state
        UpdateAndBringSelectedPipToView(pager, asNewIndex, asOldIndex);

        // Update pager layout
        pager.UpdateLayout();
    }

    #endregion

    #region ItemsRepeater

    private void ItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not Button asButton)
        {
            return;
        }

        AssignPipButtonStyle(asButton, args.Index != ItemIndex
                                 ? NormalPipButtonStyle
                                 : SelectedPipButtonStyle);

        if (asButton.Tag is true)
        {
            return;
        }

        // Store Tag as true so the event won't be subscribed more than once.
        asButton.Tag = true;
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
        if (sender is not Button asButton)
        {
            return;
        }

        int index = _pipsPagerItemsRepeater.GetElementIndex(asButton);
        if (index < 0 ||
            ItemsCount <= index)
        {
            return;
        }

        ItemIndex = index;
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

        Orientation layoutOrientation = Orientation;
        double pipsButtonSize = GetButtonSize(layoutOrientation);

        PointerPoint pointer      = e.GetCurrentPoint(element);
        int          orientation  = pointer.Properties.MouseWheelDelta;
        bool         isHorizontal = layoutOrientation == Orientation.Horizontal;
        double       delta        = pipsButtonSize * (orientation / 120d);

        double toOffset = (isHorizontal
            ? _pipsPagerScrollViewer.HorizontalOffset
            : _pipsPagerScrollViewer.VerticalOffset) + -delta;
        toOffset = Math.Round(toOffset / pipsButtonSize) * pipsButtonSize;

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

    private double GetButtonSize(Orientation orientation)
    {
        double pipsButtonSize;

        bool isRetry = false;
        Retry:
        if (_pipsPagerItemsRepeater.TryGetElement(ItemIndex) is not { } button)
        {
            goto GetBasedOnRepeaterSize;
        }

        Vector2 desiredSize = button.ActualSize;
        pipsButtonSize = orientation == Orientation.Horizontal
            ? desiredSize.X
            : desiredSize.Y;

        if (pipsButtonSize != 0) return pipsButtonSize;

        GetBasedOnRepeaterSize:
        _pipsPagerItemsRepeater.UpdateLayout(); // Wake up the repeater and re-display element.
        if (!isRetry)
        {
            isRetry = true;
            goto Retry;
        }
        pipsButtonSize = orientation == Orientation.Horizontal
            ? _pipsPagerItemsRepeater.ActualHeight
            : _pipsPagerItemsRepeater.ActualWidth;

        return pipsButtonSize;
    }

    #endregion

    #region Loaded and Unloaded

    private void NewPipsPager_Unloaded(object sender, RoutedEventArgs e)
    {
        _pipsPagerItemsRepeater.ItemsSource = null;
    }

    private void NewPipsPager_Loaded(object sender, RoutedEventArgs e)
    {
        ItemsCount_OnChange(ItemsCount);
        UpdateAndBringSelectedPipToView(this, ItemIndex, -1);

        // Update navigation buttons state
        UpdatePreviousButtonVisualState(this);
        UpdateNextButtonVisualState(this);
    }

    #endregion
}
