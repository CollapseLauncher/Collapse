using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Animation;
using System.Collections.Generic;
using System.Linq;

namespace BackgroundTest.CustomControl.PanelSlideshow;

public partial class PanelSlideshow
{
    #region Events

    private static void Items_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Reset the index on applying media items
        PanelSlideshow slideshow = (PanelSlideshow)d;
        slideshow.ItemIndex = 0;
    }

    private static void ItemIndex_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow slideshow = (PanelSlideshow)d;
        int newIndex = (int)e.NewValue;
        int oldIndex = (int)e.OldValue;
        ItemIndex_OnChange(slideshow, newIndex, oldIndex);
    }

    private static void ItemIndex_OnChange(PanelSlideshow slideshow, int newIndex, int oldIndex)
    {
        if (slideshow.Items is null || slideshow.Items.Count == 0)
        {
            return;
        }

        if (newIndex < 0 || newIndex >= slideshow.Items.Count)
        {
            return;
        }

        // Just add to Grid if oldIndex is -1 (initialization on startup)
        if (oldIndex == -1 && slideshow.Items.FirstOrDefault() is UIElement firstElement)
        {
            slideshow._presenter.Content = firstElement;
            return;
        }

        bool isBackward = newIndex < oldIndex &&
                          (oldIndex - 1 == newIndex) ||
                          (newIndex == slideshow.Items.Count - 1 && oldIndex == 0);
        if (slideshow.ItemTransition is PaneThemeTransition paneThemeTransition)
        {
            SwitchUsingPaneThemeTransition(slideshow._presenter, slideshow.Items, paneThemeTransition, newIndex, isBackward);
            return;
        }

        if (slideshow.ItemTransition is EdgeUIThemeTransition edgeUIThemeTransition)
        {
            SwitchUsingEdgeUIThemeTransition(slideshow._presenter, slideshow.Items, edgeUIThemeTransition, newIndex, isBackward);
            return;
        }

        SwitchUsingOtherTransition(slideshow._presenter, slideshow.Items, slideshow.ItemTransition, newIndex);
    }

    private static void ItemTransition_OnChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        PanelSlideshow slideshow = (PanelSlideshow)d;
        ItemTransition_OnChange(slideshow.Items, (Transition)e.NewValue);
    }

    private static void ItemTransition_OnChange(IList<UIElement> elements, Transition transition)
    {
        foreach (UIElement element in elements)
        {
            element.Transitions.Clear();
            element.Transitions.Add(transition);
        }
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
        UIElement? lastElement = presenter.Content as UIElement;

        EdgeTransitionLocation currentEdge = paneThemeTransition.Edge;
        if (isBackward)
        {
            currentEdge = GetReversedEdge(currentEdge);
        }

        if (lastElement is not null &&
            lastElement.Transitions.LastOrDefault() is EdgeUIThemeTransition lastElementTransition)
        {
            lastElementTransition.Edge = GetReversedEdge(currentEdge);
        }

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(new EdgeUIThemeTransition()
        {
            Edge = currentEdge
        });
        presenter.Content = currentElement;
        currentElement.UpdateLayout();

        static EdgeTransitionLocation GetReversedEdge(EdgeTransitionLocation edge)
        {
            return edge switch
            {
                EdgeTransitionLocation.Right => EdgeTransitionLocation.Left,
                EdgeTransitionLocation.Bottom => EdgeTransitionLocation.Top,
                EdgeTransitionLocation.Left => EdgeTransitionLocation.Right,
                EdgeTransitionLocation.Top => EdgeTransitionLocation.Bottom,
                _ => throw new System.NotImplementedException()
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

        if (lastElement is not null &&
            lastElement.Transitions.LastOrDefault() is PaneThemeTransition lastElementTransition)
        {
            lastElementTransition.Edge = GetReversedEdge(currentEdge);
        }

        currentElement.Transitions.Clear();
        currentElement.Transitions.Add(new PaneThemeTransition()
        {
            Edge = currentEdge
        });
        presenter.Content = currentElement;
        currentElement.UpdateLayout();

        static EdgeTransitionLocation GetReversedEdge(EdgeTransitionLocation edge)
        {
            return edge switch
            {
                EdgeTransitionLocation.Right => EdgeTransitionLocation.Left,
                EdgeTransitionLocation.Bottom => EdgeTransitionLocation.Top,
                EdgeTransitionLocation.Left => EdgeTransitionLocation.Right,
                EdgeTransitionLocation.Top => EdgeTransitionLocation.Bottom,
                _ => throw new System.NotImplementedException()
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
    }

    private void PanelSlideshow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize element on the current index.
        ItemIndex_OnChange(this, ItemIndex, -1);
    }

    #endregion
}
