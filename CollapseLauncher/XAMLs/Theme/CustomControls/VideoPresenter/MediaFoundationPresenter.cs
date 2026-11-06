using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Enums.D2D;
using Hi3Helper.Win32.Native.Enums.D3D;
using Hi3Helper.Win32.Native.Enums.DXGI;
using Hi3Helper.Win32.Native.Interfaces.CompositorInterop;
using Hi3Helper.Win32.Native.Interfaces.D3D;
using Hi3Helper.Win32.Native.Interfaces.DXGI;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.Native.Structs.D3D;
using Hi3Helper.Win32.Native.Structs.DXGI;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.DirectX;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using Rect = Windows.Foundation.Rect;
#pragma warning disable CA1873
#pragma warning disable IDE0074

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;

/// <summary>
/// A XAML element that presents video frames through either a XAML-friendly drawing
/// surface or a composition swap chain.
/// Owns GPU resources only while loaded, visible, and intersecting its effective viewport.
/// </summary>
public sealed partial class MediaFoundationPresenter : FrameworkElement, IVideoFramePresenter
{
    private const int DXGI_ERROR_DEVICE_REMOVED = unchecked((int)0x887A0005);
    private const int DXGI_ERROR_DEVICE_HUNG    = unchecked((int)0x887A0006);
    private const int DXGI_ERROR_DEVICE_RESET   = unchecked((int)0x887A0007);

    private static int _loadedPresenterCount;

    private int RenderWidth;
    private int RenderHeight;

    private readonly Lock _renderLock = new();

    // Frame callbacks must not wait for the UI thread while holding _renderLock.
    // Keep the dependency properties' values here, protected by that same lock.
    private DirectXPixelFormat _pixelFormat = DirectXPixelFormat.B8G8R8A8UIntNormalized;
    private DirectXAlphaMode   _alphaMode   = DirectXAlphaMode.Premultiplied;
    private CompositionStretch _stretch     = CompositionStretch.UniformToFill;
    private bool               _useSwapChain;
    private Vector2            _layoutSize;

    private readonly DispatcherQueue _dispatcherQueue;

    private Compositor?                _compositor;
    private SpriteVisual?              _videoVisual;
    private CompositionSurfaceBrush?   _videoBrush;
    private CompositionDrawingSurface? _compositionSurface;
    private CompositionGraphicsDevice? _compositionGraphicsDevice;
    private ICompositionSurface?       _swapChainSurface;
    private CanvasDevice?              _canvasDevice;

    private nint              _drawingSurfaceInteropAbi;
    private nint              _swapChainAbi;
    private nint              _d3dDeviceAbi;
    private nint              _d3dContextAbi;
    private nint              _frameTexture;
    private IDirect3DSurface? _frameSurface;
    private nint              _frameSurfaceAbi;

    private          bool _disposed;
    private          bool _recreating;
    private          bool _isLoaded;
    private          bool _hasViewport;
    private          Rect _effectiveViewport;
    private readonly long _visibilityCallbackToken;

    public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
     nameof(Stretch),
     typeof(CompositionStretch),
     typeof(MediaFoundationPresenter),
     new PropertyMetadata(CompositionStretch.UniformToFill, OnStretchChanged));

    public static readonly DependencyProperty PixelFormatProperty = DependencyProperty.Register(
     nameof(PixelFormat),
     typeof(DirectXPixelFormat),
     typeof(MediaFoundationPresenter),
     new PropertyMetadata(DirectXPixelFormat.B8G8R8A8UIntNormalized, OnPresentationConfigurationChanged));

    public static readonly DependencyProperty AlphaModeProperty = DependencyProperty.Register(
     nameof(AlphaMode),
     typeof(DirectXAlphaMode),
     typeof(MediaFoundationPresenter),
     new PropertyMetadata(DirectXAlphaMode.Premultiplied, OnPresentationConfigurationChanged));

    public static readonly DependencyProperty UseSwapChainProperty = DependencyProperty.Register(
     nameof(UseSwapChain),
     typeof(bool),
     typeof(MediaFoundationPresenter),
     new PropertyMetadata(false, OnPresentationConfigurationChanged));

    /// <summary>
    /// The Scale of the video frame to be displayed. Defaults to: <see cref="CompositionStretch.UniformToFill"/>
    /// </summary>
    public CompositionStretch Stretch
    {
        get => (CompositionStretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>
    /// Format shared by the video frame texture and the selected presentation surface.
    /// Use <see cref="DirectXPixelFormat.R16G16B16A16Float"/> for sources that supply HDR frames as linear scRGB.
    /// Defaults to: <see cref="DirectXPixelFormat.B8G8R8A8UIntNormalized"/>
    /// </summary>
    public DirectXPixelFormat PixelFormat
    {
        get => (DirectXPixelFormat)GetValue(PixelFormatProperty);
        set => SetValue(PixelFormatProperty, value);
    }

    /// <summary>
    /// Alpha mode used by the video frame texture and its presentation buffers.
    /// To ignore the Alpha channel, use <see cref="DirectXAlphaMode.Ignore"/>.
    /// Defaults to: <see cref="DirectXAlphaMode.Premultiplied"/>
    /// </summary>
    public DirectXAlphaMode AlphaMode
    {
        get => (DirectXAlphaMode)GetValue(AlphaModeProperty);
        set => SetValue(AlphaModeProperty, value);
    }

    /// <summary>
    /// Whether frames are presented through a composition swap chain instead of a
    /// XAML-friendly composition drawing surface. This can be changed while the presenter is in use.
    /// Defaults to: <see langword="false"/>
    /// </summary>
    public bool UseSwapChain
    {
        get => (bool)GetValue(UseSwapChainProperty);
        set => SetValue(UseSwapChainProperty, value);
    }

    /// <summary>
    /// An instance to the Logger for trace or debug purposes.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// <see langword="true"/> while this element intersects its effective viewport and owns rendering resources.
    /// </summary>
    public bool IsPresentationActive { get; private set; }

    /// <summary>
    /// Raised on the UI thread when viewport visibility changes resource ownership.
    /// </summary>
    public event EventHandler? PresentationStateChanged;

    /// <summary>
    /// Creates a new instance of <see cref="MediaFoundationPresenter"/>.
    /// </summary>
    public MediaFoundationPresenter()
    {
        _dispatcherQueue         =  DispatcherQueue;
        Loaded                   += OnLoaded;
        Unloaded                 += OnUnloaded;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        SizeChanged              += OnSizeChanged;
        _visibilityCallbackToken =  RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);

        Interlocked.Increment(ref _loadedPresenterCount);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = true;
        using (_renderLock.EnterScope())
            _layoutSize = new Vector2((float)ActualWidth, (float)ActualHeight);
        // Wait for viewport information before allocating, including on the first load offscreen.
        UpdatePresentationState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _isLoaded = false;
        // Keep the last viewport: WinUI only reports changes, so reloading at the same
        // bounds may not produce another notification. _isLoaded still gates allocation.
        UpdatePresentationState();
    }

    private static void OnStretchChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        MediaFoundationPresenter presenter = (MediaFoundationPresenter)sender;
        using (presenter._renderLock.EnterScope())
        {
            presenter._stretch = (CompositionStretch)args.NewValue;
            presenter.UpdateSurfaceBrushTransform();
        }
    }

    private static void OnPresentationConfigurationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        MediaFoundationPresenter presenter = (MediaFoundationPresenter)sender;
        using (presenter._renderLock.EnterScope())
        {
            presenter._pixelFormat  = presenter.PixelFormat;
            presenter._alphaMode    = presenter.AlphaMode;
            presenter._useSwapChain = presenter.UseSwapChain;

            // Defer allocation until viewport reentry if currently inactive. The next
            // frame supplies the dimensions; never allocate a zero-sized frame texture.
            if (presenter is { _disposed: false, IsPresentationActive: true })
                presenter.RecreateDeviceResources();
        }
    }

    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
    {
        _effectiveViewport = args.EffectiveViewport;
        _hasViewport       = true;
        UpdatePresentationState();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        using (_renderLock.EnterScope())
        {
            _layoutSize = new Vector2((float)args.NewSize.Width, (float)args.NewSize.Height);
            UpdateSurfaceBrushTransform();
        }
        UpdatePresentationState();
    }

    private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property) => UpdatePresentationState();

    // Fill the offered layout slot. In an unconstrained panel the caller can set Width/Height.
    protected override Size MeasureOverride(Size availableSize) =>
        new(double.IsPositiveInfinity(availableSize.Width) ? 0 : availableSize.Width,
            double.IsPositiveInfinity(availableSize.Height) ? 0 : availableSize.Height);

    protected override Size ArrangeOverride(Size finalSize) => finalSize;

    /// <summary>Permanently releases this presenter. Call on the UI thread.</summary>
    public void Dispose()
    {
        using (_renderLock.EnterScope())
        {
            if (_disposed)
                return;

            _disposed                =  true;
            Loaded                   -= OnLoaded;
            Unloaded                 -= OnUnloaded;
            EffectiveViewportChanged -= OnEffectiveViewportChanged;
            SizeChanged              -= OnSizeChanged;
            UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityCallbackToken);
        }

        int remainingPresenterCount = Interlocked.Decrement(ref _loadedPresenterCount);
        Logger?.LogTrace("Total Remained MediaFoundationPresenter instances: {count} instances", remainingPresenterCount);

        UpdatePresentationState();
        PresentationStateChanged = null;
        Logger                   = null;
        Tag                      = null;
    }

    #region Update Frame and Device States Methods

    [SkipLocalsInit]
    private unsafe void UpdateSurfaceBrushTransform()
    {
        if (_videoBrush is null)
            return;

        if (!_useSwapChain)
        {
            _videoBrush.Stretch         = _stretch;
            _videoBrush.TransformMatrix = Matrix3x2.Identity;
            return;
        }

        // Swap-chain surfaces do not participate in automatic surface-brush stretching.
        // Apply sizing on the swap chain itself, before the composition surface clips it.
        _videoBrush.Stretch         = CompositionStretch.None;
        _videoBrush.TransformMatrix = Matrix3x2.Identity;
        if (RenderWidth <= 0 ||
            RenderHeight <= 0 ||
            _swapChainAbi == nint.Zero ||
            _layoutSize.X <= 0 ||
            _layoutSize.Y <= 0)
            return;

        Vector2 frameSize = new(RenderWidth, RenderHeight);
        Vector2 scale     = _layoutSize / frameSize;
        scale = _stretch switch
        {
            CompositionStretch.None          => Vector2.One,
            CompositionStretch.Uniform       => new Vector2(Math.Min(scale.X, scale.Y)),
            CompositionStretch.UniformToFill => new Vector2(Math.Max(scale.X, scale.Y)),
            _                                => scale
        };

        Vector2   offset    = (_layoutSize - frameSize * scale) * 0.5f;
        Matrix3x2 transform = Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(offset);
        // IDXGISwapChain2::SetMatrixTransform; Matrix3x2 matches DXGI_MATRIX_3X2_F.
        ComVtables.IDXGISwapChain2_SetMatrixTransform(_swapChainAbi, in transform).ThrowOnFailure();
    }

    [SkipLocalsInit]
    private void UpdatePresentationState()
    {
        Rect visibleBounds = _effectiveViewport;
        visibleBounds.Intersect(new Rect(0, 0, ActualWidth, ActualHeight));

        bool active = !_disposed && _isLoaded && _hasViewport && Visibility == Visibility.Visible &&
                      visibleBounds is { IsEmpty: false, Width: > 0, Height: > 0 };

        using (_renderLock.EnterScope())
        {
            if (IsPresentationActive == active)
                return;

            if (active)
                CreateCompositionResources();
            else
                ReleaseCompositionResources();

            IsPresentationActive = active;
        }

        // Playback owners may wait for their own frame callbacks; never notify under the render lock.
        PresentationStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void CalculateFrameSize(int requestedWidth, int requestedHeight)
    {
        if (requestedWidth <= 0 || requestedHeight <= 0)
            return;

        if (_frameSurface != null &&
            requestedWidth == RenderWidth &&
            requestedHeight == RenderHeight)
            return;

        ResizePresentationSurface(requestedWidth, requestedHeight);
    }

    #endregion

    #region Create and Release Device Methods

    [SkipLocalsInit]
    private void CreateCompositionResources()
    {
        using (_renderLock.EnterScope())
        {
            if (_disposed || _videoVisual != null)
                return;

            try
            {
                _compositor                          = ElementCompositionPreview.GetElementVisual(this).Compositor;
                _videoBrush                          = _compositor.CreateSurfaceBrush();
                _videoBrush.Stretch                  = Stretch;
                _videoBrush.HorizontalAlignmentRatio = 0.5f;
                _videoBrush.VerticalAlignmentRatio   = 0.5f;

                _videoVisual                        = _compositor.CreateSpriteVisual();
                _videoVisual.Brush                  = _videoBrush;
                _videoVisual.RelativeSizeAdjustment = Vector2.One;

                CreateDeviceResources();

                // This element owns the visual; no other XAML element's visual is replaced.
                ElementCompositionPreview.SetElementChildVisual(this, _videoVisual);
            }
            catch
            {
                ReleaseCompositionResources();
                throw;
            }
        }
    }

    private void ReleaseCompositionResources()
    {
        ElementCompositionPreview.SetElementChildVisual(this, null);
        ReleaseDeviceResources();
        Interlocked.Exchange(ref _videoVisual, null)?.Dispose();
        Interlocked.Exchange(ref _videoBrush,  null)?.Dispose();
        _compositor = null;
        _recreating = false;
    }

    [SkipLocalsInit]
    private unsafe void CreateDeviceResources()
    {
        using (_renderLock.EnterScope())
        {
            _canvasDevice = CanvasDevice.GetSharedDevice();
            _d3dDeviceAbi = CreateD3DDevice(_canvasDevice, Logger);
            if (_d3dDeviceAbi == nint.Zero)
                throw new InvalidOperationException("Could not obtain the native D3D11 device from the CanvasDevice.");

            ComVtables.CacheD3D11Device(_d3dDeviceAbi);
            ComVtables.ID3D11Device_GetImmediateContext(_d3dDeviceAbi, out _d3dContextAbi);
            ComVtables.CacheD3D11DeviceContext(_d3dContextAbi);

            // MediaPlayer and the compositor also use this immediate context.
            nint d3d11MultithreadAbi = nint.Zero;
            try
            {
                if (Marshal.QueryInterface(_d3dContextAbi,
                                           in ComMarshal<ID3D11Multithread>.IID,
                                           out d3d11MultithreadAbi) >= 0)
                {
                    ComVtables.CacheD3D11Multithread(d3d11MultithreadAbi);
                    ComVtables.ID3D11Multithread_SetMultithreadProtected(d3d11MultithreadAbi, 1);
                }
            }
            finally
            {
                ReleaseAbi(ref d3d11MultithreadAbi);
            }

            if (_useSwapChain)
            {
                // Create the swap chain with the first frame's actual dimensions.
                return;
            }

            _compositionGraphicsDevice = CreateCompositionGraphicsDevice(_compositor!, _d3dDeviceAbi, Logger);
            _compositionSurface = _compositionGraphicsDevice.CreateDrawingSurface(
             new Size(0, 0),
             _pixelFormat,
             _alphaMode);

            // QueryInterface creates the single native reference owned by this presenter.
            nint surfaceP = ((IWinRTObject)_compositionSurface).NativeObject.ThisPtr;
            int hr = Marshal.QueryInterface(surfaceP, in ComMarshal<ICompositionDrawingSurfaceInterop>.IID, out _drawingSurfaceInteropAbi);
            Marshal.ThrowExceptionForHR(hr);
            ComVtables.CacheCompositionDrawingSurfaceInterop(_drawingSurfaceInteropAbi);
            _videoBrush!.Surface = _compositionSurface;
        }
    }

    private void ReleaseDeviceResources()
    {
        _videoBrush?.Surface = null;
        ReleaseDrawingSurfaceFrame();
        ReleaseSwapChain();
        ReleaseAbi(ref _drawingSurfaceInteropAbi);

        Interlocked.Exchange(ref _compositionSurface,        null)?.Dispose();
        Interlocked.Exchange(ref _compositionGraphicsDevice, null)?.Dispose();

        ReleaseAbi(ref _d3dContextAbi);
        ReleaseAbi(ref _d3dDeviceAbi);
        _canvasDevice = null;

        RenderWidth = RenderHeight = 0;
    }

    [SkipLocalsInit]
    private static unsafe nint CreateD3DDevice(CanvasDevice canvasDevice, ILogger? logger)
    {
        nint canvasDeviceAbi = ((IWinRTObject)canvasDevice).NativeObject.ThisPtr;
        nint accessAbi       = nint.Zero;
        nint d3dDeviceAbi    = nint.Zero;

        try
        {
            int hr = Marshal.QueryInterface(canvasDeviceAbi,
                                            in ComMarshal<IDirect3DDxgiInterfaceAccess>.IID,
                                            out accessAbi);
            Marshal.ThrowExceptionForHR(hr);
            ComVtables.CacheDirect3DDxgiInterfaceAccess(accessAbi);

            ComVtables.IDirect3DDxgiInterfaceAccess_GetInterface(accessAbi,
                                                                 in ComMarshal<ID3D11Device>.IID,
                                                                 out d3dDeviceAbi).ThrowOnFailure();

            logger?.LogDebug("D3D11 Device Created!");
            return d3dDeviceAbi;
        }
        catch
        {
            ReleaseAbi(ref d3dDeviceAbi);
            throw;
        }
        finally
        {
            ReleaseAbi(ref accessAbi);
        }
    }

    [SkipLocalsInit]
    private static unsafe CompositionGraphicsDevice CreateCompositionGraphicsDevice(
        Compositor compositor,
        nint       d3dDeviceAbi,
        ILogger?   logger)
    {
        nint compositorAbi        = ((IWinRTObject)compositor).NativeObject.ThisPtr;
        nint compositorInteropAbi = nint.Zero;
        nint graphicsDeviceP      = nint.Zero;
        try
        {
            int hr = Marshal.QueryInterface(compositorAbi,
                                            in ComMarshal<ICompositorInterop>.IID,
                                            out compositorInteropAbi);
            Marshal.ThrowExceptionForHR(hr);
            ComVtables.CacheCompositorInterop(compositorInteropAbi);

            ComVtables.ICompositorInterop_CreateGraphicsDevice(compositorInteropAbi,
                                                               d3dDeviceAbi,
                                                               out graphicsDeviceP).ThrowOnFailure();

            logger?.LogDebug("D3D11 Graphics Created from the Compositor!");
            return MarshalInterface<CompositionGraphicsDevice>.FromAbi(graphicsDeviceP);
        }
        finally
        {
            ReleaseAbi(ref graphicsDeviceP);
            ReleaseAbi(ref compositorInteropAbi);
        }
    }

    #endregion

    #region Queue and Re-create Device Methods

    private void QueueDeviceRecreation()
    {
        if (_recreating || _disposed)
            return;

        _recreating = true;

        // Ignore a queued recovery if the element was unloaded (or reloaded) meanwhile.
        CompositionSurfaceBrush? brush = _videoBrush;
        if (!_dispatcherQueue.TryEnqueue(ImplRecreateCall))
        {
            _recreating = false;
        }

        return;

        void ImplRecreateCall()
        {
            using (_renderLock.EnterScope())
            {
                if (!_disposed && brush != null && ReferenceEquals(brush, _videoBrush))
                {
                    RecreateDeviceResources();
                }
            }
        }
    }

    private void RecreateDeviceResources()
    {
        using (_renderLock.EnterScope())
        {
            if (_disposed || !IsPresentationActive || _videoBrush == null)
                return;

            try
            {
                ReleaseDeviceResources();
                CreateDeviceResources();
            }
            catch
            {
                ReleaseDeviceResources();
                throw;
            }
            finally
            {
                _recreating = false;
            }
        }
    }

    #endregion

    #region Create, Release and Resize Frame Methods

    [SkipLocalsInit]
    private unsafe void CreateDrawingSurfaceFrame()
    {
        DXGI_FORMAT format = (DXGI_FORMAT)_pixelFormat;
        D3D11_TEXTURE2D_DESC desc = new()
        {
            Width      = (uint)RenderWidth,
            Height     = (uint)RenderHeight,
            MipLevels  = 1,
            ArraySize  = 1,
            Format     = format,
            SampleDesc = new DXGI_SAMPLE_DESC { Count = 1 },
            Usage      = D3D11_USAGE.D3D11_USAGE_DEFAULT,
            BindFlags  = D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE
        };

        nint textureP         = nint.Zero;
        nint dxgiSurfaceP     = nint.Zero;
        nint graphicsSurfaceP = nint.Zero;
        try
        {
            ComVtables.ID3D11Device_CreateTexture2D(_d3dDeviceAbi,
                                                             in desc,
                                                             nint.Zero,
                                                             out textureP).ThrowOnFailure();

            int hr = Marshal.QueryInterface(textureP, in ComMarshal<IDXGISurface>.IID, out dxgiSurfaceP);
            Marshal.ThrowExceptionForHR(hr);

            PInvoke.CreateDirect3D11SurfaceFromDXGISurface(dxgiSurfaceP, out graphicsSurfaceP).ThrowOnFailure();

            _frameTexture = textureP;
            textureP      = nint.Zero;

            _frameSurface = MarshalInterface<IDirect3DSurface>.FromAbi(graphicsSurfaceP);
            hr = Marshal.QueryInterface(graphicsSurfaceP, in ComMarshal<IDirect3DSurface>.IID, out _frameSurfaceAbi);
            Marshal.ThrowExceptionForHR(hr);

            Logger?.LogDebug("D3D11 Texture Created! {width}x{height} with format: {format}", RenderWidth, RenderHeight, desc.Format);
        }
        catch
        {
            ReleaseDrawingSurfaceFrame();
            throw;
        }
        finally
        {
            if (graphicsSurfaceP != nint.Zero) Marshal.Release(graphicsSurfaceP);
            if (dxgiSurfaceP != nint.Zero) Marshal.Release(dxgiSurfaceP);
            if (textureP != nint.Zero) Marshal.Release(textureP);
        }
    }

    private unsafe void ReleaseDrawingSurfaceFrame()
    {
        ReleaseAbi(ref _frameTexture);
        ReleaseAbi(ref _frameSurfaceAbi);

        Interlocked.Exchange(ref _frameSurface, null)?.Dispose();
        if (_d3dContextAbi != nint.Zero)
            ComVtables.ID3D11DeviceContext_Flush(_d3dContextAbi);
    }

    [SkipLocalsInit]
    private unsafe void CreateSwapChainFrame(int width, int height)
    {
        nint factoryAbi           = nint.Zero;
        nint swapChain1Abi        = nint.Zero;
        nint compositorInteropAbi = nint.Zero;
        nint surfaceAbi           = nint.Zero;

        try
        {
            PInvoke.CreateDXGIFactory2(0,
                                       in ComMarshal<IDXGIFactory2>.IID,
                                       out factoryAbi).ThrowOnFailure();
            ComVtables.CacheDXGIFactory2(factoryAbi);

            DXGI_SWAP_CHAIN_DESC1 desc = new()
            {
                Width       = (uint)width,
                Height      = (uint)height,
                Format      = (DXGI_FORMAT)_pixelFormat,
                SampleDesc  = new DXGI_SAMPLE_DESC { Count = 1 },
                BufferUsage = DXGI_USAGE.DXGI_USAGE_RENDER_TARGET_OUTPUT,
                BufferCount = 2,
                Scaling     = DXGI_SCALING.DXGI_SCALING_STRETCH,
                SwapEffect  = DXGI_SWAP_EFFECT.DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL,
                AlphaMode   = (DXGI_ALPHA_MODE)_alphaMode
            };

            ComVtables.IDXGIFactory2_CreateSwapChainForComposition(factoryAbi,
                                                                   _d3dDeviceAbi,
                                                                   in desc,
                                                                   nint.Zero,
                                                                   out swapChain1Abi).ThrowOnFailure();

            int hr = Marshal.QueryInterface(swapChain1Abi,
                                            in ComMarshal<IDXGISwapChain3>.IID,
                                            out _swapChainAbi);
            Marshal.ThrowExceptionForHR(hr);
            ComVtables.CacheDXGISwapChain3(_swapChainAbi);

            if (_pixelFormat == DirectXPixelFormat.R16G16B16A16Float)
            {
                // Linear scRGB (BT.709 primaries), not PQ: MediaPlayer has already converted
                // the source transfer function when copying into the FP16 frame texture.
                const DXGI_COLOR_SPACE_TYPE scRgb = DXGI_COLOR_SPACE_TYPE.DXGI_COLOR_SPACE_RGB_FULL_G10_NONE_P709;
                ComVtables.IDXGISwapChain3_CheckColorSpaceSupport(_swapChainAbi,
                                                                  scRgb,
                                                                  out DXGI_SWAP_CHAIN_COLOR_SPACE_SUPPORT_FLAG support).ThrowOnFailure();
                if (!support.HasFlag(DXGI_SWAP_CHAIN_COLOR_SPACE_SUPPORT_FLAG.PRESENT))
                    throw new NotSupportedException("The graphics device cannot present a linear scRGB swap chain.");

                ComVtables.IDXGISwapChain3_SetColorSpace1(_swapChainAbi, scRgb).ThrowOnFailure();
            }

            nint compositorAbi = ((IWinRTObject)_compositor!).NativeObject.ThisPtr;
            hr = Marshal.QueryInterface(compositorAbi,
                                        in ComMarshal<ICompositorSwapChainInterop>.IID,
                                        out compositorInteropAbi);
            Marshal.ThrowExceptionForHR(hr);
            ComVtables.CacheCompositorSwapChainInterop(compositorInteropAbi);

            ComVtables.ICompositorSwapChainInterop_CreateCompositionSurfaceForSwapChain(
                 compositorInteropAbi,
                 _swapChainAbi,
                 out surfaceAbi).ThrowOnFailure();
            _swapChainSurface    = MarshalInterface<ICompositionSurface>.FromAbi(surfaceAbi);
            _videoBrush!.Surface = _swapChainSurface;

            Logger?.LogDebug("D3D11 Swap Chain Created! {width}x{height} with format: {format}", width, height, desc.Format);
        }
        catch
        {
            ReleaseSwapChain();
            throw;
        }
        finally
        {
            ReleaseAbi(ref surfaceAbi);
            ReleaseAbi(ref compositorInteropAbi);
            ReleaseAbi(ref swapChain1Abi);
            ReleaseAbi(ref factoryAbi);
        }
    }

    private void ReleaseSwapChain()
    {
        _videoBrush?.Surface = null;

        ICompositionSurface? swapChainSurface = Interlocked.Exchange(ref _swapChainSurface, null);
        if (swapChainSurface is IWinRTObject winRtSurface)
            winRtSurface.NativeObject.Dispose();

        ReleaseAbi(ref _swapChainAbi);
    }

    [SkipLocalsInit]
    private unsafe void ResizePresentationSurface(
        int width,
        int height)
    {
        ReleaseDrawingSurfaceFrame();
        if (_useSwapChain && _swapChainAbi == nint.Zero)
        {
            CreateSwapChainFrame(width, height);
            RenderWidth  = width;
            RenderHeight = height;
            UpdateSurfaceBrushTransform();
            CreateDrawingSurfaceFrame();
            return;
        }

        (_swapChainAbi != nint.Zero
            ? ComVtables.IDXGISwapChain_ResizeBuffers(_swapChainAbi,
                                                      0,
                                                      (uint)width,
                                                      (uint)height,
                                                      (DXGI_FORMAT)_pixelFormat,
                                                      0)
            : ComVtables.ICompositionDrawingSurfaceInterop_Resize(_drawingSurfaceInteropAbi,
                                                                  new SIZEL { Width = width, Height = height })).ThrowOnFailure();

        RenderWidth  = width;
        RenderHeight = height;
        UpdateSurfaceBrushTransform();
        CreateDrawingSurfaceFrame();

        Logger?.LogDebug("Video Surface Resized: {width}x{height}", width, height);
    }

    #endregion

    #region Draw and Copy Methods

    [SkipLocalsInit]
    public void Draw(Direct3DSurfaceConsumer surfaceConsumer,
                     int                     canvasWidth,
                     int                     canvasHeight)
    {
        using Lock.Scope renderScope = _renderLock.EnterScope();
        try
        {
            if (_disposed || _recreating || _d3dDeviceAbi == nint.Zero ||
                canvasWidth <= 0 || canvasHeight <= 0)
                return;

            CalculateFrameSize(canvasWidth, canvasHeight);
            if (_frameSurface == null)
                return;

            surfaceConsumer(_frameSurface);
            if (_swapChainAbi != nint.Zero)
            {
                PresentSwapChainFrame();
                return;
            }

            PresentDrawingSurfaceFrame();
        }
        catch (Exception ex) when(IsDeviceLost(ex.HResult))
        {
            QueueDeviceRecreation();
        }
    }

    [SkipLocalsInit]
    [SuppressGCTransition]
    public unsafe void DrawUnsafe(
        Direct3DSurfaceConsumerUnsafe surfaceConsumerUnsafe,
        int                           canvasWidth,
        int                           canvasHeight)
    {
        using Lock.Scope renderScope = _renderLock.EnterScope();
        try
        {
            if (_disposed || _recreating || _d3dDeviceAbi == nint.Zero ||
                canvasWidth <= 0 || canvasHeight <= 0)
                return;

            CalculateFrameSize(canvasWidth, canvasHeight);
            if (_frameSurfaceAbi == nint.Zero)
                return;

            surfaceConsumerUnsafe(_frameSurfaceAbi);
            if (_swapChainAbi != nint.Zero)
            {
                PresentSwapChainFrame();
                return;
            }

            PresentDrawingSurfaceFrame();
        }
        catch (Exception ex) when (IsDeviceLost(ex.HResult))
        {
            QueueDeviceRecreation();
        }
    }

    [SkipLocalsInit]
    private static bool IsDeviceLost(int hr)
        => hr is DXGI_ERROR_DEVICE_REMOVED
              or DXGI_ERROR_DEVICE_HUNG
              or DXGI_ERROR_DEVICE_RESET;

    private static void ReleaseAbi(ref nint abi)
    {
        nint ownedReference = Interlocked.Exchange(ref abi, nint.Zero);
        if (ownedReference != nint.Zero)
            Marshal.Release(ownedReference);
    }

    [SkipLocalsInit]
    public CanvasRenderTarget? CopyCurrentFrame()
    {
        using Lock.Scope renderScope = _renderLock.EnterScope();
        if (_canvasDevice == null || _frameSurface == null)
            return null;

        Windows.Graphics.DirectX.DirectXPixelFormat pixelFormat = (Windows.Graphics.DirectX.DirectXPixelFormat)PixelFormat;
        CanvasAlphaMode alphaMode = _alphaMode switch
        {
            DirectXAlphaMode.Ignore        => CanvasAlphaMode.Ignore,
            DirectXAlphaMode.Premultiplied => CanvasAlphaMode.Premultiplied,
            DirectXAlphaMode.Straight      => throw new NotSupportedException(),

            _ => CanvasAlphaMode.Premultiplied
        };

        CanvasRenderTarget renderTarget = new(_canvasDevice, RenderWidth, RenderHeight, 96f, pixelFormat, alphaMode);
        using CanvasBitmap source = CanvasBitmap.CreateFromDirect3D11Surface(_canvasDevice, _frameSurface, 96, alphaMode);
        renderTarget.CopyPixelsFromBitmap(source);
        return renderTarget;
    }

    #endregion

    #region Present Frame
    [SkipLocalsInit]
    private unsafe void PresentSwapChainFrame()
    {
        nint backBuffer = 0;
        try
        {
            // Release before Present/ResizeBuffers.
            ComVtables.IDXGISwapChain3_GetBuffer(_swapChainAbi,
                                                 0,
                                                 in ComMarshal<ID3D11Texture2D>.IID,
                                                 out backBuffer).ThrowOnFailure();
            ComVtables.ID3D11DeviceContext_CopySubresourceRegion(_d3dContextAbi, backBuffer, 0, 0, 0, 0, _frameTexture, 0, nint.Zero);
        }
        finally
        {
            ReleaseAbi(ref backBuffer);
        }

        // Present flushes the copy and submits the frame to the composition swap chain.
        ComVtables.IDXGISwapChain3_Present(_swapChainAbi, 0, 0).ThrowOnFailure();
    }

    [SkipLocalsInit]
    private unsafe void PresentDrawingSurfaceFrame()
    {
        ComVtables.ICompositionDrawingSurfaceInterop_BeginDraw(_drawingSurfaceInteropAbi,
                                                               nint.Zero,
                                                               in ComMarshal<IDXGISurface>.IID,
                                                               out nint updateP,
                                                               out POINTL offset).ThrowOnFailure();

        nint updateTextureP = nint.Zero;
        try
        {
            int hr = Marshal.QueryInterface(updateP, in ComMarshal<ID3D11Texture2D>.IID, out updateTextureP);
            Marshal.ThrowExceptionForHR(hr);

            ComVtables.ID3D11DeviceContext_CopySubresourceRegion(_d3dContextAbi, updateTextureP, 0, offset.x, offset.y, 0, _frameTexture, 0, nint.Zero);
            ComVtables.ID3D11DeviceContext_Flush(_d3dContextAbi);
        }
        finally
        {
            // The update texture belongs to this BeginDraw/EndDraw pair only.
            ReleaseAbi(ref updateTextureP);
            ReleaseAbi(ref updateP);
            ComVtables.ICompositionDrawingSurfaceInterop_EndDraw(_drawingSurfaceInteropAbi).ThrowOnFailure();
        }
    }
    #endregion
}
