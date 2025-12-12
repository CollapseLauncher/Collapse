using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Threading;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;

[TemplatePart(Name = TemplateNameParallaxGrid,    Type = typeof(Grid))]
[TemplatePart(Name = TemplateNamePlaceholderGrid, Type = typeof(Grid))]
[TemplatePart(Name = TemplateNameBackgroundGrid,  Type = typeof(Grid))]
[TemplatePart(Name = TemplateNameForegroundGrid,  Type = typeof(Grid))]
public partial class LayeredBackgroundImage
{
    #region Constants

    private const string TemplateNameParallaxGrid      = "ParallaxGrid";
    private const string TemplateNamePlaceholderGrid   = "PlaceholderGrid";
    private const string TemplateNameBackgroundGrid    = "BackgroundGrid";
    private const string TemplateNameForegroundGrid    = "ForegroundGrid";

    private const string StateNamePlaceholderStateHidden = "PlaceholderStateHidden";

    #endregion

    #region Fields

    private Grid _parallaxGrid    = null!;
    private Grid _placeholderGrid = null!;
    private Grid _backgroundGrid  = null!;
    private Grid _foregroundGrid  = null!;

    private Visual     _parallaxGridVisual     = null!;
    private Compositor _parallaxGridCompositor = null!;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template Methods

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _parallaxGrid    = this.GetTemplateChild<Grid>(TemplateNameParallaxGrid);
        _placeholderGrid = this.GetTemplateChild<Grid>(TemplateNamePlaceholderGrid);
        _backgroundGrid  = this.GetTemplateChild<Grid>(TemplateNameBackgroundGrid);
        _foregroundGrid  = this.GetTemplateChild<Grid>(TemplateNameForegroundGrid);

        ElementCompositionPreview.SetIsTranslationEnabled(_parallaxGrid, true);

        _parallaxGridVisual     = ElementCompositionPreview.GetElementVisual(_parallaxGrid);
        _parallaxGridCompositor = _parallaxGridVisual.Compositor;

        Interlocked.Exchange(ref _isTemplateLoaded, true);

        Loaded   += LayeredBackgroundImage_OnLoaded;
        Unloaded += LayeredBackgroundImage_OnUnloaded;
    }

    #endregion
}

