using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;

namespace BackgroundTest.CustomControl.PanelSlideshow;

[TemplatePart(Name = TemplateNameRootGrid, Type = typeof(Grid))]
public partial class PanelSlideshow
{
    #region Constants

    private const string TemplateNameRootGrid = "RootGrid";
    private const string TemplateNamePresenterScrollContent = "Presenter";

    #endregion

    #region Fields

    private Grid _rootGrid = null!;
    private ScrollContentPresenter _presenter = null!;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _rootGrid = this.GetTemplateChild<Grid>(TemplateNameRootGrid);
        _presenter = this.GetTemplateChild<ScrollContentPresenter>(TemplateNamePresenterScrollContent);

        Interlocked.Exchange(ref _isTemplateLoaded, true);

        Loaded += PanelSlideshow_Loaded;
        Unloaded += PanelSlideshow_Unloaded;
    }

    #endregion
}
