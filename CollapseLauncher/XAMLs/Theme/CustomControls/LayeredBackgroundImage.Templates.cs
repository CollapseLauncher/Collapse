using CollapseLauncher.Extension;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Numerics;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Constants

    private const string TemplateNameParallaxGrid    = "ParallaxGrid";
    private const string TemplateNameBackgroundGrid  = "BackgroundGrid";
    private const string TemplateNameForegroundGrid  = "ForegroundGrid";
    private const string TemplateNameElevateGrid     = "ElevateGrid";

    #endregion

    #region Fields

    // Container-level Grid
    private Grid _parallaxGrid    = null!;
    private Grid _elevateGrid     = null!;

    // Media Grid
    private Grid _backgroundGrid  = null!;
    private Grid _foregroundGrid  = null!;

    // Source Context Generation

    private ulong _sourceContextGeneration;

    private Visual     _parallaxGridVisual     = null!;
    private Compositor _parallaxGridCompositor = null!;

    private Visual     _elevateGridVisual     = null!;
    private Compositor _elevateGridCompositor = null!;

    private CubicBezierEasingFunction _parallaxEasingFunction;
    private CubicBezierEasingFunction _elevateEasingFunction;
    private bool                      _templateInitialized;
    private bool                      _requireOnLoadedCall = true;

    #endregion

    #region Apply Template Methods

    protected override void OnApplyTemplate()
    {
        if (Interlocked.Exchange(ref _templateInitialized, true))
            return;

        _parallaxGrid   = this.GetTemplateChild<Grid>(TemplateNameParallaxGrid);
        _backgroundGrid = this.GetTemplateChild<Grid>(TemplateNameBackgroundGrid);
        _foregroundGrid = this.GetTemplateChild<Grid>(TemplateNameForegroundGrid);
        _elevateGrid    = this.GetTemplateChild<Grid>(TemplateNameElevateGrid);

        _parallaxGridVisual     = ElementCompositionPreview.GetElementVisual(_parallaxGrid);
        _parallaxGridCompositor = _parallaxGridVisual.Compositor;

        _elevateGridVisual     = ElementCompositionPreview.GetElementVisual(_elevateGrid);
        _elevateGridCompositor = _elevateGridVisual.Compositor;

        _parallaxEasingFunction = _parallaxGridCompositor.CreateCubicBezierEasingFunction(new Vector2(.45f, .05f), new Vector2(.55f, .95f));
        _elevateEasingFunction = _parallaxGridCompositor.CreateCubicBezierEasingFunction(new Vector2(0.19f, 1), new Vector2(0.22f, 1));

        base.OnApplyTemplate();

        // Help the uncalled OnLoaded routine to be called after this.
        // 
        // Note to myself @neon-nyan:
        //   See the reason on the comments under the OnLoaded code in case you forgot :)
        if (_requireOnLoadedCall)
            LayeredBackgroundImage_OnLoaded(this, null!);
    }

    #endregion
}

