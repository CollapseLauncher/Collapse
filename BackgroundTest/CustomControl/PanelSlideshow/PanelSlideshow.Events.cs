using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

namespace BackgroundTest.CustomControl.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Events

    private static void Items_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Reset the index on applying media items
        PanelSlideshow slideshow = (PanelSlideshow)d;
        slideshow.ItemIndex = 0;
        slideshow.Items_OnChange(e);
    }

    private void Items_OnChange(DependencyPropertyChangedEventArgs e)
    {
        ManagedUIElementList? oldList = e.OldValue as ManagedUIElementList;
        ManagedUIElementList? newList = e.NewValue as ManagedUIElementList;
        ItemsChanged?.Invoke(this, new ChangedObjectItemArgs<ManagedUIElementList>(oldList, newList));
    }

    private static void ItemIndex_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow slideshow = (PanelSlideshow)d;
        int newIndex = (int)e.NewValue;
        int oldIndex = (int)e.OldValue;
        slideshow.ItemIndex_OnChange(newIndex, oldIndex);
    }

    private void ItemIndex_OnChange(int newIndex, int oldIndex)
    {
        if (Items.Count == 0 || !IsLoaded)
        {
            return;
        }

        if (newIndex < 0 || newIndex >= Items.Count)
        {
            return;
        }

        // Restart slideshow timer
        RestartTimer(SlideshowDuration);

        // Preload to invisible grid. This preload is necessary to avoid
        // the element jumping off the element due to size and offset being unset.
        PreloadPastOrFutureItem(Items, _preloadGrid, newIndex, Items.Count);

        // Just add to Grid if oldIndex is -1 (initialization on startup)
        if (oldIndex == -1 && Items.FirstOrDefault() is { } firstElement)
        {
            _preloadGrid.Children.Remove(firstElement);
            _presenter.Content = firstElement;
            _presenter.Transitions.Add(ItemTransition);
            return;
        }

        bool isBackward = (newIndex < oldIndex &&
                           oldIndex - 1 == newIndex) ||
                          (newIndex == Items.Count - 1 && oldIndex == 0);

        IList<UIElement> elements = Items;

        try
        {
            switch (ItemTransition)
            {
                case PaneThemeTransition paneThemeTransition:
                    SwitchUsingPaneThemeTransition(_presenter, elements, paneThemeTransition, newIndex, isBackward);
                    return;
                case EdgeUIThemeTransition edgeUIThemeTransition:
                    SwitchUsingEdgeUIThemeTransition(_presenter, elements, edgeUIThemeTransition, newIndex, isBackward);
                    return;
                default:
                    SwitchUsingOtherTransition(_presenter, elements, ItemTransition, newIndex);
                    break;
            }
        }
        finally
        {
            UIElement newElement = elements[newIndex];
            UIElement? oldElement = oldIndex < 0 || oldIndex >= elements.Count
                ? null
                : elements[oldIndex];

            SetValue(ItemProperty, newElement);
            ItemChanged?.Invoke(this, new ChangedObjectItemArgs<UIElement>(oldElement, newElement));
            ItemIndexChanged?.Invoke(this, new ChangedStructItemArgs<int>(oldIndex, newIndex));
        }
    }

    private static void ItemTransition_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow slideshow = (PanelSlideshow)d;
        slideshow.ItemTransition_OnChange(slideshow.Items, e.OldValue as Transition, e.NewValue as Transition);
    }

    private void ItemTransition_OnChange(IList<UIElement> elements, Transition? oldTransition, Transition? newTransition)
    {
        if (newTransition != null)
        {
            foreach (UIElement element in elements)
            {
                element.Transitions.Clear();
                element.Transitions.Add(newTransition);
            }
        }

        ItemTransitionChanged?.Invoke(this, new ChangedObjectItemArgs<Transition>(oldTransition, newTransition));
    }

    private static void SlideshowDuration_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow slideshow = (PanelSlideshow)d;
        slideshow.RestartTimer((double)e.NewValue);
    }

    #endregion

    #region Element Preload Helper

    private static void PreloadPastOrFutureItem(IList<UIElement> collection, Grid preloadGrid, int currentIndex, int itemsCount)
    {
        if (currentIndex < 0)
        {
            return;
        }

        int pastIndex = currentIndex switch
                        {
                            0 when itemsCount >= 3 => itemsCount - 1,
                            > 0 => currentIndex - 1,
                            _ => -1
                        };

        int futureIndex = -1;
        if (itemsCount - 1 > currentIndex)
        {
            futureIndex = currentIndex + 1;
        }
        else if (currentIndex + 1 >= itemsCount)
        {
            futureIndex = 0;
        }

        UIElement? pastElement   = pastIndex == -1 ? null : collection[pastIndex];
        UIElement? futureElement = futureIndex == -1 ? null : collection[futureIndex];

        preloadGrid.Children.Clear();
        AssignToPreloadGridAndApplyPayload(pastElement);
        AssignToPreloadGridAndApplyPayload(futureElement);
        return;

        void AssignToPreloadGridAndApplyPayload(UIElement? element)
        {
            if (element is not FrameworkElement { ActualSize: { X: 0, Y: 0 } } elementFe)
            {
                return;
            }

            elementFe.SizeChanged += PreloadItem_OnLoaded;
            preloadGrid.Children.Add(elementFe);
        }
    }

    private static void PreloadItem_OnLoaded(object? sender, SizeChangedEventArgs args)
    {
        if (sender is not FrameworkElement { Parent: Grid parentGrid } element)
        {
            return;
        }

        element.SizeChanged -= PreloadItem_OnLoaded;
        parentGrid.Children.Remove(element);
    }

    #endregion

    #region Transition Switch Helper

    private static void SwitchUsingEdgeUIThemeTransition(
        ScrollContentPresenter presenter,
        IList<UIElement> items,
        EdgeUIThemeTransition paneThemeTransition,
        int index,
        bool isBackward)
    {
        UIElement currentElement = items[index];
        UIElement? lastElement   = presenter.Content as UIElement;

        EdgeTransitionLocation currentEdge = paneThemeTransition.Edge;
        if (isBackward)
        {
            currentEdge = GetReversedEdge(currentEdge);
        }

        if (lastElement?.Transitions.LastOrDefault() is EdgeUIThemeTransition lastElementTransition)
        {
            lastElementTransition.Edge = GetReversedEdge(currentEdge);
        }

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(new EdgeUIThemeTransition
        {
            Edge = currentEdge
        });
        presenter.Content = currentElement;
        currentElement.UpdateLayout();
        return;

        static EdgeTransitionLocation GetReversedEdge(EdgeTransitionLocation edge)
        {
            return edge switch
            {
                EdgeTransitionLocation.Right => EdgeTransitionLocation.Left,
                EdgeTransitionLocation.Bottom => EdgeTransitionLocation.Top,
                EdgeTransitionLocation.Left => EdgeTransitionLocation.Right,
                EdgeTransitionLocation.Top => EdgeTransitionLocation.Bottom,
                _ => throw new NotImplementedException()
            };
        }
    }

    private static void SwitchUsingPaneThemeTransition(
        ScrollContentPresenter presenter,
        IList<UIElement> items,
        PaneThemeTransition paneThemeTransition,
        int index,
        bool isBackward)
    {
        UIElement currentElement = items[index];
        UIElement? lastElement = presenter.Content as UIElement;

        EdgeTransitionLocation currentEdge = paneThemeTransition.Edge;
        if (isBackward)
        {
            currentEdge = GetReversedEdge(currentEdge);
        }

        if (lastElement?.Transitions.LastOrDefault() is PaneThemeTransition lastElementTransition)
        {
            lastElementTransition.Edge = GetReversedEdge(currentEdge);
        }

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(new PaneThemeTransition
        {
            Edge = currentEdge
        });
        presenter.Content = currentElement;
        currentElement.UpdateLayout();
        return;

        static EdgeTransitionLocation GetReversedEdge(EdgeTransitionLocation edge)
        {
            return edge switch
            {
                EdgeTransitionLocation.Right => EdgeTransitionLocation.Left,
                EdgeTransitionLocation.Bottom => EdgeTransitionLocation.Top,
                EdgeTransitionLocation.Left => EdgeTransitionLocation.Right,
                EdgeTransitionLocation.Top => EdgeTransitionLocation.Bottom,
                _ => throw new NotImplementedException()
            };
        }
    }

    private static void SwitchUsingOtherTransition(
        ScrollContentPresenter presenter, IList<UIElement> items, Transition transition, int index)
    {
        UIElement currentElement = items[index];
        UIElement? lastElement = presenter.Content as UIElement;

        if (lastElement != null)
        {
            lastElement.Transitions.Clear();
            lastElement.Transitions.Add(transition);
        }

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(transition);

        presenter.Content = currentElement;
    }

    #endregion

    #region Loaded and Unloaded

    private void PanelSlideshow_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PanelSlideshow_Loaded;
        Unloaded -= PanelSlideshow_Unloaded;

        PointerEntered -= PanelSlideshow_PointerEntered;
        PointerExited -= PanelSlideshow_PointerExited;

        KeyDown -= PanelSlideshow_KeyDown;
        PointerWheelChanged -= PanelSlideshow_OnPointerWheelChanged;

        _previousButton.Click -= PreviousButton_OnClick;
        _nextButton.Click -= NextButton_OnClick;

        // Deregister timer
        DisposeAndDeregisterTimer();
    }

    private void PanelSlideshow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize element on the current index.
        ItemIndex_OnChange(ItemIndex, -1);

        PointerEntered += PanelSlideshow_PointerEntered;
        PointerExited += PanelSlideshow_PointerExited;

        KeyDown += PanelSlideshow_KeyDown;
        PointerWheelChanged += PanelSlideshow_OnPointerWheelChanged;

        _previousButton.Click += PreviousButton_OnClick;
        _nextButton.Click += NextButton_OnClick;
    }

    #endregion

    #region Key Presses and Scroll

    private void PanelSlideshow_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.Up:
                PreviousButton_OnClick(sender, e);
                break;
            case VirtualKey.Right:
            case VirtualKey.Down:
                NextButton_OnClick(sender, e);
                break;
        }
    }

    private void PanelSlideshow_OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!e.Pointer.IsInRange ||
            sender is not UIElement element)
        {
            return;
        }

        PointerPoint pointer = e.GetCurrentPoint(element);
        int orientation = pointer.Properties.MouseWheelDelta;
        bool isNext = orientation < 0;

        if (isNext)
        {
            ItemIndex++;
        }
        else
        {
            ItemIndex--;
        }
    }

    #endregion

    #region PointerEntered and PointerExited

    private void PanelSlideshow_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _isMouseHover = true;
        VisualStateManager.GoToState(this, StateNamePointerOver, true);

        PauseSlideshow();
    }

    private void PanelSlideshow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _isMouseHover = false;
        VisualStateManager.GoToState(this, StateNameNormal, true);

        ResumeSlideshow();
    }

    #endregion

    #region Navigation Buttons

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs args) => ItemIndex--;

    private void NextButton_OnClick(object? sender, RoutedEventArgs args) => ItemIndex++;

    #endregion
}
