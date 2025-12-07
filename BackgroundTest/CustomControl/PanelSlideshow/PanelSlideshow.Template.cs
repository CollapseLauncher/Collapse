using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Threading;

namespace BackgroundTest.CustomControl.PanelSlideshow;

[TemplatePart(Name = TemplateNameRootGrid, Type = typeof(Grid))]
[TemplatePart(Name = TemplateNamePresenterScrollContent, Type = typeof(ScrollContentPresenter))]
[TemplatePart(Name = TemplateNamePreloadGrid, Type = typeof(Grid))]
[TemplatePart(Name = TemplateNamePreviousButton, Type = typeof(Button))]
[TemplatePart(Name = TemplateNameNextButton, Type = typeof(Button))]
public partial class PanelSlideshow
{
    #region Constants

    private const string TemplateNameRootGrid = "RootGrid";
    private const string TemplateNamePresenterScrollContent = "Presenter";
    private const string TemplateNamePreloadGrid = "PreloadGrid";
    private const string TemplateNamePreviousButton = "PreviousButton";
    private const string TemplateNameNextButton = "NextButton";

    #endregion

    #region Fields

    private ScrollContentPresenter _presenter = null!;
    private Grid _preloadGrid = null!;
    private Button _previousButton = null!;
    private Grid _previousButtonGrid = null!;
    private Button _nextButton = null!;
    private Grid _nextButtonGrid = null!;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _presenter = this.GetTemplateChild<ScrollContentPresenter>(TemplateNamePresenterScrollContent);
        _preloadGrid = this.GetTemplateChild<Grid>(TemplateNamePreloadGrid);
        _previousButton = this.GetTemplateChild<Button>(TemplateNamePreviousButton);
        _nextButton = this.GetTemplateChild<Button>(TemplateNameNextButton);

        _previousButtonGrid = (Grid)_previousButton.Parent;
        _nextButtonGrid = (Grid)_nextButton.Parent;
        ElementCompositionPreview.SetIsTranslationEnabled(_previousButtonGrid, true);
        ElementCompositionPreview.SetIsTranslationEnabled(_nextButtonGrid, true);

        Interlocked.Exchange(ref _isTemplateLoaded, true);

        Loaded += PanelSlideshow_Loaded;
        Unloaded += PanelSlideshow_Unloaded;
    }

    #endregion
}
