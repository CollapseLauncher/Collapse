using CollapseLauncher.Extension;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Constants

    private const string TemplateNameParallaxGrid    = "ParallaxGrid";
    private const string TemplateNamePlaceholderGrid = "PlaceholderGrid";
    private const string TemplateNameBackgroundGrid  = "BackgroundGrid";
    private const string TemplateNameForegroundGrid  = "ForegroundGrid";
    private const string TemplateNameElevateGrid     = "ElevateGrid";

    private const string StateNamePlaceholderStateHidden = "PlaceholderStateHidden";

    #endregion

    #region Fields

    private Grid _parallaxGrid    = null!;
    private Grid _placeholderGrid = null!;
    private Grid _backgroundGrid  = null!;
    private Grid _foregroundGrid  = null!;
    private Grid _elevateGrid     = null!;

    private Visual     _parallaxGridVisual     = null!;
    private Compositor _parallaxGridCompositor = null!;

    private Visual     _elevateGridVisual     = null!;
    private Compositor _elevateGridCompositor = null!;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template Methods

    protected override void OnApplyTemplate()
    {
        _parallaxGrid    = this.GetTemplateChild<Grid>(TemplateNameParallaxGrid);
        _placeholderGrid = this.GetTemplateChild<Grid>(TemplateNamePlaceholderGrid);
        _backgroundGrid  = this.GetTemplateChild<Grid>(TemplateNameBackgroundGrid);
        _foregroundGrid  = this.GetTemplateChild<Grid>(TemplateNameForegroundGrid);
        _elevateGrid     = this.GetTemplateChild<Grid>(TemplateNameElevateGrid);

        _parallaxGridVisual     = ElementCompositionPreview.GetElementVisual(_parallaxGrid);
        _parallaxGridCompositor = _parallaxGridVisual.Compositor;

        _elevateGridVisual     = ElementCompositionPreview.GetElementVisual(_elevateGrid);
        _elevateGridCompositor = _elevateGridVisual.Compositor;

        Interlocked.Exchange(ref _isTemplateLoaded, true);

        base.OnApplyTemplate();
    }

    #endregion
}

