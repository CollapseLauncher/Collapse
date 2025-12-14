using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System.Threading;
using Windows.Foundation;
using Windows.Media.Playback;

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

    private Visual     _parallaxGridVisual     = null!;
    private Compositor _parallaxGridCompositor = null!;

    private CanvasVirtualImageSource? _canvasImageSource;
    private CanvasDevice? _canvasDevice;

    private volatile CanvasRenderTarget? _canvasRenderTarget;
    private CanvasDrawingSession? _currentCanvasDrawingSession = null;
    private Rect _currentCanvasRenderSize = default;

    private float _canvasWidth;
    private float _canvasHeight;

    private MediaPlayer _videoPlayer = null!;

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

#if USENATIVESWAPCHAIN
        SizeChanged += LayeredBackgroundImage_SizeChanged;
#endif

        base.OnApplyTemplate();
    }

    private void SetupVideoPlayer()
    {
        DisposeVideoPlayer();
        _videoPlayer = new MediaPlayer
        {
            AutoPlay                  = false,
            IsLoopingEnabled          = true,
            IsVideoFrameServerEnabled = true,
            Volume                    = AudioVolume.GetClampedVolume(),
            IsMuted                   = !IsAudioEnabled
        };
        _videoPlayer.VideoFrameAvailable += VideoPlayer_VideoFrameAvailable;
    }

    private void InitializeCanvasBitmapSource(Image image, MediaPlaybackSession playbackSession)
    {
        DisposeAndInvalidateCanvas();

        float currentCanvasWidth = playbackSession.NaturalVideoWidth;
        float currentCanvasHeight = playbackSession.NaturalVideoHeight;
        float currentCanvasDpi = 96f * (float)XamlRoot.RasterizationScale;

        _canvasWidth = (float)(currentCanvasWidth * XamlRoot.RasterizationScale);
        _canvasHeight = (float)(currentCanvasHeight * XamlRoot.RasterizationScale);
        _currentCanvasRenderSize = new Rect(0, 0, _canvasWidth, _canvasHeight);

        _canvasDevice = CanvasDevice.GetSharedDevice();

        _canvasImageSource = new CanvasVirtualImageSource(_canvasDevice,
                                                   currentCanvasWidth,
                                                   currentCanvasHeight,
                                                   currentCanvasDpi,
                                                   CanvasAlphaMode.Premultiplied);
        _canvasRenderTarget = new CanvasRenderTarget(_canvasDevice,
                                               currentCanvasWidth,
                                               currentCanvasHeight,
                                               currentCanvasDpi);

        image.Source = _canvasImageSource.Source;

#if USENATIVESWAPCHAIN
        InitializeOrResizeCanvas(true);
#endif

        Interlocked.Exchange(ref _isResizingVideoCanvas, false);
    }

    private void DisposeAndInvalidateCanvas()
    {
        if (Interlocked.Exchange(ref _isResizingVideoCanvas, true))
        {
            return;
        }

        if (_canvasRenderTarget is { } previousCanvasBitmap)
        {
            previousCanvasBitmap.Dispose();
        }

        if (_canvasDevice is { } previousCanvasDevice)
        {
            previousCanvasDevice.Dispose();
        }
    }

    private void DisposeVideoPlayer()
    {
        if (_videoPlayer != null!)
        {
            _videoPlayer.VideoFrameAvailable -= VideoPlayer_VideoFrameAvailable;
            _videoPlayer.Dispose();
            Interlocked.Exchange(ref _videoPlayer!, null);
        }

        if (_currentCanvasDrawingSession is { } previousCanvasDrawingSession)
        {
            previousCanvasDrawingSession.Dispose();
            Interlocked.Exchange(ref _currentCanvasDrawingSession, null);
        }

        Interlocked.Exchange(ref _isDrawingVideoFrame, false);
    }

#if USENATIVESWAPCHAIN
    private void LayeredBackgroundImage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var newSize = e.NewSize;
        if (newSize != default)
        {
            InitializeOrResizeCanvas();
        }
    }

    private void InitializeOrResizeCanvas(bool ignoreCheck = false)
    {
        var size = ActualSize;
        if (size == default)
        {
            return;
        }

        if (!ignoreCheck && Interlocked.Exchange(ref _isResizingVideoCanvas, true))
        {
            return;
        }

        if (_swapChainPanel == null)
        {
            _swapChainPanel = new();
            _backgroundGrid.Children.Add(_swapChainPanel);
        }

        if (_swapChainContext != null)
        {
            _swapChainContext?.Dispose();
        }

        DXGI_SWAP_CHAIN_DESC1 description = default;
        description.Width = (uint)size.X;
        description.Height = (uint)size.Y;
        description.Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM;
        description.BufferCount = 2;
        description.BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT;
        description.SampleDesc.Count = 1;
        description.SwapEffect = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
        description.AlphaMode = DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_PREMULTIPLIED;

        _canvasWidth = description.Width;
        _canvasHeight = description.Height;

        SwapChainPanelHelper.InitializeD3D11Device(_swapChainPanel,
                                                   XamlRoot.RasterizationScale,
                                                   ref description,
                                                   out _swapChainContext!);

        if (!ignoreCheck)
        {
            Interlocked.Exchange(ref _isResizingVideoCanvas, false);
        }
    }
#endif

    #endregion
}

