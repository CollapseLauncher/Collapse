using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;
using Windows.Foundation;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.NewPipsPager;

public partial class NewPipsPager
{
    #region Events

    public event TypedEventHandler<NewPipsPager, ChangedStructItemArgs<int>>? ItemsCountChanged;
    public event TypedEventHandler<NewPipsPager, ChangedStructItemArgs<int>>? ItemIndexChanged;

    #endregion

    #region Properties

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

    public NewPipsPagerSelectionMode SelectionMode
    {
        get => (NewPipsPagerSelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
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

    public Style PreviousButtonStyle
    {
        get => (Style)GetValue(PreviousButtonStyleProperty);
        set => SetValue(PreviousButtonStyleProperty, value);
    }

    public Style NextButtonStyle
    {
        get => (Style)GetValue(NextButtonStyleProperty);
        set => SetValue(NextButtonStyleProperty, value);
    }

    public Style NormalPipButtonStyle
    {
        get => (Style)GetValue(NormalPipButtonStyleProperty);
        set => SetValue(NormalPipButtonStyleProperty, value);
    }

    public Style SelectedPipButtonStyle
    {
        get => (Style)GetValue(SelectedPipButtonStyleProperty);
        set => SetValue(SelectedPipButtonStyleProperty, value);
    }

    #endregion

    #region Fields

    private readonly Lock _atomicLock = new();

    private int[] _itemsDummy = [];

    #endregion

    #region Dependency Change Methods

    private static void UpdatePreviousButtonVisualState(NewPipsPager pager)
    {
        UpdateButtonVisualState(pager,
                                pager._previousPageButton,
                                pager.ItemIndex > 0,
                                NavButtonStatePreviousPageButtonCollapsed,
                                NavButtonStatePreviousPageButtonVisible,
                                NavButtonStatePreviousPageButtonHidden,
                                pager.PreviousNavigationButtonMode);
    }

    private static void UpdateNextButtonVisualState(NewPipsPager pager)
    {
        UpdateButtonVisualState(pager,
                                pager._nextPageButton,
                                pager.ItemIndex + 1 < pager.ItemsCount,
                                NavButtonStateNextPageButtonCollapsed,
                                NavButtonStateNextPageButtonVisible,
                                NavButtonStateNextPageButtonHidden,
                                pager.NextNavigationButtonMode);
    }

    private static void UpdateButtonVisualState(
        NewPipsPager instance,
        Button? button,
        bool conditionForAutoVisible,
        string stateNameForCollapsed,
        string stateNameForVisible,
        string stateNameForHidden,
        NewPipsPagerNavigationMode mode)
    {
        if (button == null)
        {
            return;
        }

        if (mode == NewPipsPagerNavigationMode.Hidden)
        {
            VisualStateManager.GoToState(instance, stateNameForCollapsed, true);
            return;
        }

        VisualStateManager.GoToState(instance, stateNameForVisible, true);
        button.IsEnabled = true;
        if (mode == NewPipsPagerNavigationMode.Visible)
        {
            return;
        }

        if (conditionForAutoVisible)
        {
            return;
        }

        button.IsEnabled = false;
        VisualStateManager.GoToState(instance, stateNameForHidden, true);
    }

    private static void UpdateAndBringSelectedPipToView(NewPipsPager pager, int newIndex, int oldIndex)
    {
        if (pager.UpdateSelectedPipStyle(newIndex, oldIndex) is not { } asButton)
        {
            return;
        }

        BringIntoViewOptions options = new()
        {
            AnimationDesired = true
        };
        if (pager.Orientation == Orientation.Horizontal)
        {
            options.HorizontalAlignmentRatio = 0.5d;
        }
        else
        {
            options.VerticalAlignmentRatio = 0.5d;
        }
        asButton.StartBringIntoView(options);
    }

    private Button? UpdateSelectedPipStyle(int newIndex, int oldIndex)
    {
        ItemsRepeater? repeater = _pipsPagerItemsRepeater;

        if (!_isTemplateLoaded ||
            repeater?.ItemsSourceView == null)
        {
            return null;
        }

        int  childCount       = repeater.ItemsSourceView.Count;
        bool isUpdateNewChild = newIndex >= 0 && newIndex < childCount;
        bool isUpdateOldChild = oldIndex >= 0 && oldIndex < childCount;

        if (ItemsCount == 0)
        {
            return null;
        }

        Button? newIndexPipButton = repeater.GetOrCreateElement(newIndex) as Button;
        Button? oldIndexPipButton = repeater.TryGetElement(oldIndex) as Button;

        try
        {
            if (isUpdateOldChild)
            {
                AssignPipButtonStyle(oldIndexPipButton, NormalPipButtonStyle);
            }

            if (isUpdateNewChild)
            {
                return AssignPipButtonStyle(newIndexPipButton, SelectedPipButtonStyle);
            }

            return newIndexPipButton;
        }
        finally
        {
            if (newIndex != oldIndex)
            {
                ItemIndexChanged?.Invoke(this, new ChangedStructItemArgs<int>(oldIndex, newIndex));
            }
        }
    }

    private static Button? AssignPipButtonStyle(Button? button, Style? style)
    {
        if (button is null)
        {
            return button;
        }

        button.Style = style;
        button.UpdateLayout();
        VisualStateManager.GoToState(button, PipButtonStateNormal, true);
        return button;
    }

    private static Style? TryGetStyle(string styleName)
    {
        if (Application.Current.Resources.TryGetValue(styleName, out object styleObj) &&
            styleObj is Style asStyle)
        {
            return asStyle;
        }

        return null;
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
    public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(nameof(SelectionMode), typeof(bool), typeof(NewPipsPager), new PropertyMetadata(NewPipsPagerSelectionMode.Click));
    public static readonly DependencyProperty PreviousNavigationButtonModeProperty = DependencyProperty.Register(nameof(PreviousNavigationButtonMode), typeof(NewPipsPagerNavigationMode), typeof(NewPipsPager), new PropertyMetadata(NewPipsPagerNavigationMode.Auto, PreviousNavigationButtonMode_OnChange));
    public static readonly DependencyProperty NextNavigationButtonModeProperty = DependencyProperty.Register(nameof(NextNavigationButtonMode), typeof(NewPipsPagerNavigationMode), typeof(NewPipsPager), new PropertyMetadata(NewPipsPagerNavigationMode.Auto, NextNavigationButtonMode_OnChange));

    public static readonly DependencyProperty PreviousButtonStyleProperty = DependencyProperty.Register(nameof(PreviousButtonStyle), typeof(Style), typeof(NewPipsPager), new PropertyMetadata(null!));
    public static readonly DependencyProperty NextButtonStyleProperty = DependencyProperty.Register(nameof(NextButtonStyle), typeof(Style), typeof(NewPipsPager), new PropertyMetadata(null!));
    public static readonly DependencyProperty NormalPipButtonStyleProperty = DependencyProperty.Register(nameof(NormalPipButtonStyle), typeof(Style), typeof(NewPipsPager), new PropertyMetadata(null!));
    public static readonly DependencyProperty SelectedPipButtonStyleProperty = DependencyProperty.Register(nameof(SelectedPipButtonStyle), typeof(Style), typeof(NewPipsPager), new PropertyMetadata(null!));

    #endregion
}
