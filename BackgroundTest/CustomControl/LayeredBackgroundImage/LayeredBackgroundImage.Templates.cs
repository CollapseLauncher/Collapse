using Hi3Helper.Win32.Native.Interfaces.DXGI;
using Hi3Helper.Win32.WinRT.SwapChainPanelHelper;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Threading;
using Windows.Media.Playback;

using CanvasRect = Hi3Helper.Win32.Native.Structs.Rect;

namespace BackgroundTest.CustomControl.LayeredBackgroundImage;
public partial class LayeredBackgroundImage
{
    #region Constants

    private const string TemplateNameParallaxGrid    = "ParallaxGrid";
    private const string TemplateNamePlaceholderGrid = "PlaceholderGrid";
    private const string TemplateNameBackgroundGrid  = "BackgroundGrid";
    private const string TemplateNameForegroundGrid  = "ForegroundGrid";

    private const string StateNamePlaceholderStateHidden = "PlaceholderStateHidden";

    #endregion

    #region Fields

    private Grid _parallaxGrid = null!;
    private Grid _placeholderGrid = null!;
    private Grid _backgroundGrid = null!;
    private Grid _foregroundGrid = null!;

    private Visual _parallaxGridVisual = null!;
    private Compositor _parallaxGridCompositor = null!;

    private D3DDeviceContext? _canvasD3DDeviceContext;
    private ISurfaceImageSourceNativeWithD2D? _canvasSurfaceImageSourceNative;
    private nint _canvasSurfaceImageSourceNativePtr = nint.Zero;
    private SurfaceImageSource? _canvasSurfaceImageSource;

    private CanvasDevice? _canvasDevice = null!;
    private int _canvasWidth;
    private int _canvasHeight;
    private CanvasRect _canvasRenderArea;

    private MediaPlayer _videoPlayer = null!;
    private nint _videoPlayerPtr = nint.Zero;

    private bool _isTemplateLoaded;

    #endregion

    #region Apply Template Methods

    protected override void OnApplyTemplate()
    {
        _parallaxGrid = this.GetTemplateChild<Grid>(TemplateNameParallaxGrid);
        _placeholderGrid = this.GetTemplateChild<Grid>(TemplateNamePlaceholderGrid);
        _backgroundGrid = this.GetTemplateChild<Grid>(TemplateNameBackgroundGrid);
        _foregroundGrid = this.GetTemplateChild<Grid>(TemplateNameForegroundGrid);

        ElementCompositionPreview.SetIsTranslationEnabled(_parallaxGrid, true);

        _parallaxGridVisual     = ElementCompositionPreview.GetElementVisual(_parallaxGrid);
        _parallaxGridCompositor = _parallaxGridVisual.Compositor;

        Interlocked.Exchange(ref _isTemplateLoaded, true);

        base.OnApplyTemplate();
    }

    #endregion
}

