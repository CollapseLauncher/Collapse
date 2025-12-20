using Hi3Helper.Data;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Windows.System;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.PanelSlideshow;

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

        IList<UIElement> elements = Items;

        try
        {
            // Just add to Grid if oldIndex is -1 (initialization on startup)
            if (oldIndex == -1 && Items.FirstOrDefault() is { } firstElement)
            {
                firstElement.Transitions.Add(new PopupThemeTransition());
                _presenterGrid.Children.Add(firstElement);
                return;
            }

            bool isBackward = (newIndex < oldIndex && !(newIndex == 0 && oldIndex == Items.Count - 1)) ||
                              (newIndex == Items.Count - 1 && oldIndex == 0);

            switch (ItemTransition)
            {
                case PaneThemeTransition paneThemeTransition:
                    SwitchUsingPaneThemeTransition(_presenterGrid, elements, paneThemeTransition, newIndex, isBackward);
                    return;
                case EdgeUIThemeTransition edgeUIThemeTransition:
                    SwitchUsingEdgeUIThemeTransition(_presenterGrid, elements, edgeUIThemeTransition, newIndex, isBackward);
                    return;
                default:
                    SwitchUsingOtherTransition(_presenterGrid, elements, ItemTransition, newIndex);
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
        double         duration  = e.NewValue.TryGetDouble();
        if (double.IsNaN(duration))
        {
            duration = 0;
        }

        slideshow.RestartTimer(duration);
    }

    private static void ItemTemplate_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow thisPanel = (PanelSlideshow)d;
        if (!thisPanel.IsLoaded)
        {
            return;
        }

        ItemsSource_OnChange(thisPanel, thisPanel.ItemsSource);
    }

    private static void ItemsSource_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow thisPanel = (PanelSlideshow)d;
        if (!thisPanel.IsLoaded)
        {
            return;
        }

        ItemsSource_OnChange(thisPanel, thisPanel.ItemsSource);
    }

    private static void ItemsSource_OnChange(PanelSlideshow thisElement, object? source)
    {
        if (thisElement._lastItemsSource == source)
        {
            return;
        }

        thisElement.Items.Clear();
        thisElement.Items = [];
        try
        {
            if (source == null)
            {
                return;
            }

            if (source is IEnumerable<UIElement> uiElements)
            {
                thisElement.Items.AddRange(uiElements);
                return;
            }

            if (source is IEnumerable enumerable &&
                thisElement.ItemTemplate is { } dataTemplate)
            {
                List<UIElement> batchedElement = [];
                foreach (object context in enumerable)
                {
                    UIElement element = dataTemplate.GetElement(new ElementFactoryGetArgs
                    {
                        Data = context
                    });

                    if (element is FrameworkElement asFrameworkElement)
                    {
                        asFrameworkElement.DataContext = context;
                    }

                    // batchedElement.Add(element);
                    thisElement.Items.Add(element);
                }

                return;
            }

            throw new InvalidOperationException("Cannot load items as ItemsSource or ItemTemplate was invalid.");
        }
        finally
        {
            thisElement._lastItemsSource = source;
        }
    }

    #endregion

    #region Transition Switch Helper

    private static void SwitchUsingEdgeUIThemeTransition(
        Grid presenterGrid,
        IList<UIElement> items,
        EdgeUIThemeTransition paneThemeTransition,
        int index,
        bool isBackward)
    {
        UIElement currentElement = items[index];
        UIElement? lastElement = presenterGrid.Children.FirstOrDefault();

        EdgeTransitionLocation currentEdge = paneThemeTransition.Edge;
        if (!isBackward)
        {
            currentEdge = GetReversedEdge(currentEdge);
        }

        if (lastElement?.Transitions.LastOrDefault() is EdgeUIThemeTransition lastElementTransition)
        {
            lastElementTransition.Edge = GetReversedEdge(currentEdge);
        }

        presenterGrid.Children.Clear();

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(new EdgeUIThemeTransition
        {
            Edge = currentEdge
        });
        presenterGrid.Children.Add(currentElement);
        currentElement.UpdateLayout();
    }

    private static void SwitchUsingPaneThemeTransition(
        Grid presenterGrid,
        IList<UIElement> items,
        PaneThemeTransition paneThemeTransition,
        int index,
        bool isBackward)
    {
        UIElement currentElement = items[index];
        UIElement? lastElement = presenterGrid.Children.FirstOrDefault();

        EdgeTransitionLocation currentEdge = paneThemeTransition.Edge;
        if (!isBackward)
        {
            currentEdge = GetReversedEdge(currentEdge);
        }

        if (lastElement?.Transitions.LastOrDefault() is PaneThemeTransition lastElementTransition)
        {
            lastElementTransition.Edge = GetReversedEdge(currentEdge);
        }

        presenterGrid.Children.Clear();

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(new PaneThemeTransition
        {
            Edge = currentEdge
        });
        presenterGrid.Children.Add(currentElement);
        currentElement.UpdateLayout();
    }

    private static void SwitchUsingOtherTransition(
        Grid presenterGrid, IList<UIElement> items, Transition transition, int index)
    {
        UIElement currentElement = items[index];
        UIElement? lastElement = presenterGrid.Children.FirstOrDefault();

        if (lastElement != null)
        {
            lastElement.Transitions.Clear();
            lastElement.Transitions.Add(transition);
        }

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(transition);

        presenterGrid.Children.Clear();
        presenterGrid.Children.Add(currentElement);
        presenterGrid.UpdateLayout();
        currentElement.UpdateLayout();
    }

    private static EdgeTransitionLocation GetReversedEdge(EdgeTransitionLocation edge)
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

    #endregion

    #region Loaded and Unloaded

    private void PanelSlideshow_Unloaded(object sender, RoutedEventArgs e)
    {
        _presenterGrid?.Children.Clear();

        _presenterGrid?.Loaded -= PanelSlideshow_Loaded;
        _presenterGrid?.Unloaded -= PanelSlideshow_Unloaded;

        PointerEntered -= PanelSlideshow_PointerEntered;
        PointerExited -= PanelSlideshow_PointerExited;

        KeyDown -= PanelSlideshow_KeyDown;
        PointerWheelChanged -= PanelSlideshow_OnPointerWheelChanged;

        _previousButton?.Click -= PreviousButton_OnClick;
        _nextButton?.Click -= NextButton_OnClick;

        // Deregister timer
        DisposeAndDeregisterTimer();
    }

    private void PanelSlideshow_Loaded(object sender, RoutedEventArgs e)
    {
        // Setup Items if ItemTemplate, ItemsSource and backed item list is empty
        if (ItemsSource != null &&
            ItemTemplate != null)
        {
            ItemsSource_OnChange(this, ItemsSource);
        }

        // Initialize element on the current index.
        ItemIndex_OnChange(ItemIndex, -1);

        PointerEntered += PanelSlideshow_PointerEntered;
        PointerExited += PanelSlideshow_PointerExited;

        KeyDown += PanelSlideshow_KeyDown;
        PointerWheelChanged += PanelSlideshow_OnPointerWheelChanged;

        _previousButton?.Click += PreviousButton_OnClick;
        _nextButton?.Click += NextButton_OnClick;
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
