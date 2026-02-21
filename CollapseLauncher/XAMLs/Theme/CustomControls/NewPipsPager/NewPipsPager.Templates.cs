using CollapseLauncher.Extension;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.NewPipsPager;

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

    private const string PipButtonStateNormal = "Normal";

    private const string NavButtonStatePreviousPageButtonCollapsed = "PreviousPageButtonCollapsed";
    private const string NavButtonStatePreviousPageButtonVisible   = "PreviousPageButtonVisible";
    private const string NavButtonStatePreviousPageButtonHidden    = "PreviousPageButtonHidden";
    private const string NavButtonStateNextPageButtonCollapsed     = "NextPageButtonCollapsed";
    private const string NavButtonStateNextPageButtonVisible       = "NextPageButtonVisible";
    private const string NavButtonStateNextPageButtonHidden        = "NextPageButtonHidden";

    #endregion

    #region Fields

    private Button        _previousPageButton     = null!;
    private Button        _nextPageButton         = null!;
    private ScrollViewer  _pipsPagerScrollViewer  = null!;
    private ItemsRepeater _pipsPagerItemsRepeater = null!;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template Methods

    protected override void OnApplyTemplate()
    {
        if (Interlocked.Exchange(ref _isTemplateLoaded, true))
        {
            return;
        }

        base.OnApplyTemplate();

        _previousPageButton     = this.GetTemplateChild<Button>(TemplateNamePreviousPageButton);
        _nextPageButton         = this.GetTemplateChild<Button>(TemplateNameNextPageButton);
        _pipsPagerScrollViewer  = this.GetTemplateChild<ScrollViewer>(TemplateNamePipsPagerScrollViewer);
        _pipsPagerItemsRepeater = this.GetTemplateChild<ItemsRepeater>(TemplateNamePipsPagerItemsRepeater);

        Loaded   += NewPipsPager_Loaded;
        Unloaded += NewPipsPager_Unloaded;

        ApplyNavigationButtonEvents();
        ApplyInitialTemplates();
        ApplyKeyPressEvents();
        ApplyItemsRepeaterEvents();
    }

    private void ApplyInitialTemplates()
    {
        _pipsPagerItemsRepeater.ItemsSource ??= _itemsDummy;
        Orientation_OnChange(this, Orientation);
    }

    private void ApplyNavigationButtonEvents()
    {
        _previousPageButton.Click += PreviousPageButton_OnClick;
        _nextPageButton.Click     += NextPageButton_OnClick;
    }

    private void UnapplyNavigationButtonEvents()
    {
        if (!_previousPageButton.IsObjectDisposed())
        {
            _previousPageButton.Click -= PreviousPageButton_OnClick;
        }

        if (!_nextPageButton.IsObjectDisposed())
        {
            _nextPageButton.Click -= NextPageButton_OnClick;
        }
    }

    private void ApplyKeyPressEvents()
    {
        KeyDown             += KeyboardKeys_Pressed;
        PointerWheelChanged += ScrollViewer_OnPointerWheelChanged;
    }

    private void UnapplyKeyPressEvents()
    {
        KeyDown             -= KeyboardKeys_Pressed;
        PointerWheelChanged -= ScrollViewer_OnPointerWheelChanged;
    }

    private void ApplyItemsRepeaterEvents()
    {
        _pipsPagerItemsRepeater.ElementPrepared += ItemsRepeater_ElementPrepared;
        _pipsPagerItemsRepeater.SizeChanged     += ItemsRepeater_OnSizeChanged;
    }

    private void UnapplyItemsRepeaterEvents()
    {
        if (!_pipsPagerItemsRepeater.IsObjectDisposed())
        {
            _pipsPagerItemsRepeater.ElementPrepared -= ItemsRepeater_ElementPrepared;
        }
    }

    #endregion
}
