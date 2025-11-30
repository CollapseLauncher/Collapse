using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using Windows.Foundation;
using Windows.System;

namespace BackgroundTest.CustomControl.NewPipsPager;

public partial class NewPipsPager
{
    #region Size Measure Override

    protected override Size MeasureOverride(Size parentSize)
    {
        if (_previousPageButton == null ||
            _nextPageButton == null ||
            _pipsPagerScrollViewer == null ||
            _pipsPagerItemsRepeater == null)
        {
            return base.MeasureOverride(parentSize);
        }

        Thickness selfMargin = Margin;
        Vector2 itemsRepeaterWidth = _pipsPagerScrollViewer.ActualSize;

        Vector2 buttonPrevSize = _previousPageButton.ActualSize;
        Thickness buttonPrevMargin = _previousPageButton.Margin;

        Vector2 buttonNextSize = _nextPageButton.ActualSize;
        Thickness buttonNextMargin = _nextPageButton.Margin;

        if (Orientation == Orientation.Horizontal)
        {
            _pipsPagerScrollViewer.MaxHeight = double.PositiveInfinity;
            double repeaterWidth = parentSize.Width - (buttonPrevSize.X + buttonPrevMargin.Left + buttonPrevMargin.Right) -
                                   (buttonNextSize.X + buttonNextMargin.Left + buttonNextMargin.Right) -
                                   (selfMargin.Left + selfMargin.Right);

            repeaterWidth = Math.Floor(repeaterWidth / _pipsButtonSize) * _pipsButtonSize;

            int widthNth = (int)Math.Floor(repeaterWidth / _pipsButtonSize);
            if (widthNth % 2 == 0)
            {
                repeaterWidth = (widthNth - 1) * _pipsButtonSize;
            }

            if (repeaterWidth > itemsRepeaterWidth.X)
            {
                repeaterWidth = double.PositiveInfinity;
            }
            repeaterWidth = Math.Max(0, repeaterWidth);

            _pipsPagerScrollViewer.MaxWidth = repeaterWidth;
        }
        else
        {
            _pipsPagerScrollViewer.MaxWidth = double.PositiveInfinity;
            double repeaterHeight = parentSize.Height - (buttonPrevSize.Y + buttonPrevMargin.Top + buttonPrevMargin.Bottom) -
                                    (buttonNextSize.Y + buttonNextMargin.Top + buttonNextMargin.Bottom) -
                                    (selfMargin.Top + selfMargin.Bottom);

            repeaterHeight = Math.Floor(repeaterHeight / _pipsButtonSize) * _pipsButtonSize;

            int heightNth = (int)Math.Floor(repeaterHeight / _pipsButtonSize);
            if (heightNth % 2 == 0)
            {
                repeaterHeight = (heightNth - 1) * _pipsButtonSize;
            }

            if (repeaterHeight > itemsRepeaterWidth.Y)
            {
                repeaterHeight = double.PositiveInfinity;
            }
            repeaterHeight = Math.Max(0, repeaterHeight);

            _pipsPagerScrollViewer.MaxHeight = repeaterHeight;
        }

        Size selfSize = base.MeasureOverride(parentSize);
        return selfSize;
    }

    #endregion

    #region UI Events - Navigation Buttons

    private void PreviousPageButtonOnClick(object sender, RoutedEventArgs e) => ItemIndex--;

    private void NextPageButtonOnClick(object sender, RoutedEventArgs e) => ItemIndex++;

    private void OnKeyPressed(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Left:
            case VirtualKey.Up:
                PreviousPageButtonOnClick(sender, e);
                break;
            case VirtualKey.Right:
            case VirtualKey.Down:
                NextPageButtonOnClick(sender, e);
                break;
        }
    }

    #endregion

    #region ItemsRepeater

    private void ItemsRepeaterMeasure_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Parent is not FrameworkElement element)
        {
            return;
        }

        Size parentSize = element.DesiredSize;
        MeasureOverride(parentSize);
    }

    private void ItemsRepeater_ElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is Button asButton)
        {
            asButton.Click += ItemsRepeater_ItemOnClick;
        }
    }

    private void ItemsRepeater_ItemOnClick(object? sender, RoutedEventArgs args)
    {
        if (sender is not Button { Tag: int pipButtonIndex })
        {
            return;
        }

        ItemIndex = pipButtonIndex; 
    }

    #endregion

    #region Loaded and Unloaded

    private void NewPipsPager_Unloaded(object sender, RoutedEventArgs e)
    {
    }

    private void NewPipsPager_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateAndBringSelectedPipToView(this, ItemIndex, -1);
    }

    #endregion
}
