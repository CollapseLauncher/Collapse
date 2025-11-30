using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BackgroundTest.CustomControl.NewPipsPager;

[TemplatePart(Name = TemplateNamePreviousPageButton,     Type = typeof(Button))]
[TemplatePart(Name = TemplateNameNextPageButton,         Type = typeof(Button))]
[TemplatePart(Name = TemplateNamePipsPagerScrollViewer,  Type = typeof(ScrollViewer))]
[TemplatePart(Name = TemplateNamePipsPagerItemsRepeater, Type = typeof(ItemsRepeater))]
public partial class NewPipsPager
{
    #region Constants

    private const string TemplateNamePreviousPageButton     = "PreviousPageButton";
    private const string TemplateNameNextPageButton         = "NextPageButton";
    private const string TemplateNamePipsPagerScrollViewer  = "PipsPagerScrollViewer";
    private const string TemplateNamePipsPagerItemsRepeater = "PipsPagerItemsRepeater";

    private const string ResourceNamePipsPagerButtonSize = "PipsPagerButtonSize";

    #endregion

    #region Fields

    private Button?        _previousPageButton;
    private Button?        _nextPageButton;
    private ScrollViewer?  _pipsPagerScrollViewer;
    private ItemsRepeater? _pipsPagerItemsRepeater;

    private double _pipsButtonSize;

    #endregion

    #region Apply Template Methods

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _previousPageButton      = this.GetTemplateChild<Button>(TemplateNamePreviousPageButton);
        _nextPageButton          = this.GetTemplateChild<Button>(TemplateNameNextPageButton);
        _pipsPagerScrollViewer   = this.GetTemplateChild<ScrollViewer>(TemplateNamePipsPagerScrollViewer);
        _pipsPagerItemsRepeater  = this.GetTemplateChild<ItemsRepeater>(TemplateNamePipsPagerItemsRepeater);

        ApplyNavigationButtonEvents();
        ApplyInitialTemplates();
        ApplyKeyPressEvents();
        ApplyItemsRepeaterEvents();

        ItemsCount_OnChange(this, ItemsCount);
        Loaded   += NewPipsPager_Loaded;
        Unloaded += NewPipsPager_Unloaded;

        Resources.TryGetValue(ResourceNamePipsPagerButtonSize, out object value);
        if (value is double size)
        {
            _pipsButtonSize = size;
        }
        else
        {
            _pipsButtonSize = 28d;
        }
    }

    private void ApplyInitialTemplates()
    {
        _pipsPagerItemsRepeater!.ItemsSource ??= _itemsDummy;
        Orientation_OnChange(this, Orientation);
    }

    private void ApplyNavigationButtonEvents()
    {
        _previousPageButton!.Click += PreviousPageButtonOnClick;
        _nextPageButton!.Click     += NextPageButtonOnClick;
    }

    private void ApplyKeyPressEvents() => KeyDown += OnKeyPressed;

    private void ApplyItemsRepeaterEvents()
    {
        _pipsPagerItemsRepeater!.SizeChanged += ItemsRepeaterMeasure_SizeChanged;
        _pipsPagerItemsRepeater.ElementPrepared += ItemsRepeater_ElementPrepared;
    }

    #endregion
}
