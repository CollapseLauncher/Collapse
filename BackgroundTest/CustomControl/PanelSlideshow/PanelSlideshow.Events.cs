using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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
        if (slideshow.Items is null || slideshow.Items.Count == 0 || slideshow._presenter == null || !slideshow.IsLoaded)
        {
            return;
        }

        if (newIndex < 0 || newIndex >= slideshow.Items.Count)
        {
            return;
        }

        // Preload to invisible grid. This preload is necessary to avoid
        // the element jumping off the element due to size and offset being unset.
        PreloadPastOrFutureItem(slideshow.Items, slideshow._preloadGrid, newIndex, slideshow.Items.Count);

        // Just add to Grid if oldIndex is -1 (initialization on startup)
        if (oldIndex == -1 && slideshow.Items.FirstOrDefault() is UIElement firstElement)
        {
            slideshow._preloadGrid.Children.Remove(firstElement);
            slideshow._presenter.Content = firstElement;
            slideshow._presenter.Transitions.Add(slideshow.ItemTransition);
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

    #region Element Preload Helper

    private static void PreloadPastOrFutureItem(IList<UIElement> collection, Grid preloadGrid, int currentIndex, int itemsCount)
    {
        if (currentIndex < 0)
        {
            return;
        }

        int pastIndex = -1;
        if (currentIndex == 0 && itemsCount >= 3)
        {
            pastIndex = itemsCount - 1;
        }
        else if (currentIndex > 0)
        {
            pastIndex = currentIndex - 1;
        }

        int futureIndex = -1;
        if (currentIndex >= 0 && itemsCount - 1 > currentIndex)
        {
            futureIndex = currentIndex + 1;
        }
        else if (currentIndex + 1 >= itemsCount)
        {
            futureIndex = 0;
        }

        UIElement? pastElement   = pastIndex   == -1 ? null : collection[pastIndex];
        UIElement? futureElement = futureIndex == -1 ? null : collection[futureIndex];

        preloadGrid.Children.Clear();
        AssignToPreloadGridAndApplyPayload(pastElement);
        AssignToPreloadGridAndApplyPayload(futureElement);

        void AssignToPreloadGridAndApplyPayload(UIElement? element)
        {
            if (element is not FrameworkElement elementFe ||
                elementFe.ActualSize is not { X: 0, Y: 0 })
            {
                return;
            }

            elementFe.SizeChanged += PreloadItem_OnLoaded;
            preloadGrid.Children.Add(elementFe);
        }
    }

    private static void PreloadItem_OnLoaded(object? sender, SizeChangedEventArgs args)
    {
        if (sender is not FrameworkElement element ||
            element.Parent is not Grid parentGrid)
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

        PointerEntered -= PanelSlideshow_PointerEntered;
        PointerExited -= PanelSlideshow_PointerExited;

        KeyDown -= PanelSlideshow_KeyDown;
        PointerWheelChanged -= PanelSlideshow_OnPointerWheelChanged;

        _previousButton.Click -= PreviousButton_OnClick;
        _nextButton.Click -= NextButton_OnClick;
    }

    private void PanelSlideshow_Loaded(object sender, RoutedEventArgs e)
    {
        // Initialize element on the current index.
        ItemIndex_OnChange(this, ItemIndex, -1);

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
        VisualStateManager.GoToState(this, "PointerOver", true);

        if (_previousButtonGrid == null ||
            _nextButtonGrid == null)
        {
            return;
        }

        StartElevateShadowAnim(_previousButtonGrid);
        StartElevateShadowAnim(_nextButtonGrid);

        static void StartElevateShadowAnim(UIElement element)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var comp = visual.Compositor;

            var ani = comp.CreateVector3KeyFrameAnimation();
            ani.InsertKeyFrame(0f, new Vector3(0, 0, 0));
            ani.InsertKeyFrame(1f, new Vector3(0, 0, 32));
            ani.Duration = TimeSpan.FromSeconds(0.25);

            visual.StartAnimation("Translation", ani);
        }
    }

    private void PanelSlideshow_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        VisualStateManager.GoToState(this, "Normal", true);

        if (_previousButtonGrid == null ||
            _nextButtonGrid == null)
        {
            return;
        }

        StartDeelevateShadowAnim(_previousButtonGrid);
        StartDeelevateShadowAnim(_nextButtonGrid);

        static void StartDeelevateShadowAnim(UIElement element)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var comp = visual.Compositor;

            var ani = comp.CreateVector3KeyFrameAnimation();
            ani.InsertKeyFrame(0f, new Vector3(0, 0, 32));
            ani.InsertKeyFrame(1f, new Vector3(0, 0, 0));
            ani.Duration = TimeSpan.FromSeconds(0.25);

            visual.StartAnimation("Translation", ani);
        }
    }

    #endregion

    #region Navigation Buttons

    private void PreviousButton_OnClick(object? sender, RoutedEventArgs args) => ItemIndex--;

    private void NextButton_OnClick(object? sender, RoutedEventArgs args) => ItemIndex++;

    #endregion
}
