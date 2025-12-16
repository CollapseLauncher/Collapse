using CollapseLauncher.Extension;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.PanelSlideshow;

[TemplatePart(Name = TemplateNameRootGrid,             Type = typeof(Grid))]
[TemplatePart(Name = TemplateNamePresenterGrid,        Type = typeof(Grid))]
[TemplatePart(Name = TemplateNamePreviousButton,       Type = typeof(Button))]
[TemplatePart(Name = TemplateNameNextButton,           Type = typeof(Button))]
[TemplatePart(Name = TemplateNameCountdownProgressBar, Type = typeof(ProgressBar))]
[TemplateVisualState(GroupName = StateGroupNameCommon,               Name = StateNameNormal)]
[TemplateVisualState(GroupName = StateGroupNameCommon,               Name = StateNamePointerOver)]
[TemplateVisualState(GroupName = StateGroupNameCommon,               Name = StateNameDisabled)]
[TemplateVisualState(GroupName = StateGroupNameCountdownProgressBar, Name = StateNameCountdownProgressBarFadeIn)]
[TemplateVisualState(GroupName = StateGroupNameCountdownProgressBar, Name = StateNameCountdownProgressBarFadeOut)]
public partial class PanelSlideshow
{
    #region Constants

    private const string TemplateNameRootGrid               = "RootGrid";
    private const string TemplateNamePresenterGrid          = "PresenterGrid";
    private const string TemplateNamePreviousButton         = "PreviousButton";
    private const string TemplateNameNextButton             = "NextButton";
    private const string TemplateNameCountdownProgressBar   = "CountdownProgressBar";

    private const string StateGroupNameCommon                 = "CommonStates";
    private const string StateNameNormal                      = "Normal";
    private const string StateNamePointerOver                 = "PointerOver";
    private const string StateNameDisabled                    = "Disabled";
    private const string StateGroupNameCountdownProgressBar   = "CountdownProgressBarStates";
    private const string StateNameCountdownProgressBarFadeIn  = "CountdownProgressBarFadeIn";
    private const string StateNameCountdownProgressBarFadeOut = "CountdownProgressBarFadeOut";

    #endregion

    #region Fields

    private Grid                   _presenterGrid        = null!;
    private Button                 _previousButton       = null!;
    private Grid                   _previousButtonGrid   = null!;
    private Button                 _nextButton           = null!;
    private Grid                   _nextButtonGrid       = null!;
    private ProgressBar            _countdownProgressBar = null!;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template

    protected override void OnApplyTemplate()
    {
        _presenterGrid        = this.GetTemplateChild<Grid>(TemplateNamePresenterGrid);
        _previousButton       = this.GetTemplateChild<Button>(TemplateNamePreviousButton);
        _nextButton           = this.GetTemplateChild<Button>(TemplateNameNextButton);
        _countdownProgressBar = this.GetTemplateChild<ProgressBar>(TemplateNameCountdownProgressBar);

        _previousButtonGrid = (Grid)_previousButton.Parent;
        _nextButtonGrid = (Grid)_nextButton.Parent;
        ElementCompositionPreview.SetIsTranslationEnabled(_previousButtonGrid, true);
        ElementCompositionPreview.SetIsTranslationEnabled(_nextButtonGrid, true);

        Interlocked.Exchange(ref _isTemplateLoaded, true);

        base.OnApplyTemplate();
    }

    #endregion
}
