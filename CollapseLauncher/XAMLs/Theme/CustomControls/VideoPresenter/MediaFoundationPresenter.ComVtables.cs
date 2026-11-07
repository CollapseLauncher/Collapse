using Hi3Helper.Win32.Native.Enums.D2D;
using Hi3Helper.Win32.Native.Enums.DXGI;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.Native.Structs.D3D;
using Hi3Helper.Win32.Native.Structs.DXGI;
using System;
using System.Numerics;
#pragma warning disable IDE0074

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;

public sealed partial class MediaFoundationPresenter
{
    private static unsafe class ComVtables
    {
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, ref readonly Guid, out nint, HResult> IDirect3DDxgiInterfaceAccess_GetInterface;
        public static delegate* unmanaged[Stdcall]<nint, nint, out nint, HResult> ICompositorInterop_CreateGraphicsDevice;
        public static delegate* unmanaged[Stdcall]<nint, nint, out nint, HResult> ICompositorSwapChainInterop_CreateCompositionSurfaceForSwapChain;
        public static delegate* unmanaged[Stdcall]<nint, nint, ref readonly Guid, out nint, out POINTL, HResult> ICompositionDrawingSurfaceInterop_BeginDraw;
        public static delegate* unmanaged[Stdcall]<nint, HResult> ICompositionDrawingSurfaceInterop_EndDraw;
        public static delegate* unmanaged[Stdcall]<nint, SIZEL, HResult> ICompositionDrawingSurfaceInterop_Resize;
        public static delegate* unmanaged[Stdcall]<nint, ref readonly D3D11_TEXTURE2D_DESC, nint, out nint, HResult> ID3D11Device_CreateTexture2D;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, out nint, void> ID3D11Device_GetImmediateContext;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, int, HResult> ID3D11Multithread_SetMultithreadProtected;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, nint, uint, int, int, uint, nint, uint, nint, void> ID3D11DeviceContext_CopySubresourceRegion;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, void> ID3D11DeviceContext_Flush;
        public static delegate* unmanaged[Stdcall]<nint, nint, ref readonly DXGI_SWAP_CHAIN_DESC1, nint, out nint, HResult> IDXGIFactory2_CreateSwapChainForComposition;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, uint, ref readonly Guid, out nint, HResult> IDXGISwapChain3_GetBuffer;
        public static delegate* unmanaged[Stdcall]<nint, uint, DXGI_PRESENT, HResult> IDXGISwapChain3_Present;
        public static delegate* unmanaged[Stdcall]<nint, uint, uint, uint, DXGI_FORMAT, uint, HResult> IDXGISwapChain_ResizeBuffers;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, ref readonly Matrix3x2, HResult> IDXGISwapChain2_SetMatrixTransform;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, DXGI_COLOR_SPACE_TYPE, out DXGI_SWAP_CHAIN_COLOR_SPACE_SUPPORT_FLAG, HResult> IDXGISwapChain3_CheckColorSpaceSupport;
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, DXGI_COLOR_SPACE_TYPE, HResult> IDXGISwapChain3_SetColorSpace1;

        public static void CacheDirect3DDxgiInterfaceAccess(nint self)
        {
            if (IDirect3DDxgiInterfaceAccess_GetInterface == null)
                IDirect3DDxgiInterfaceAccess_GetInterface = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, ref readonly Guid, out nint, HResult>)(*(*(void***)self + 3));
        }

        public static void CacheCompositorInterop(nint self)
        {
            if (ICompositorInterop_CreateGraphicsDevice == null)
                ICompositorInterop_CreateGraphicsDevice = (delegate* unmanaged[Stdcall]<nint, nint, out nint, HResult>)(*(*(void***)self + 3));
        }

        public static void CacheCompositorSwapChainInterop(nint self)
        {
            if (ICompositorSwapChainInterop_CreateCompositionSurfaceForSwapChain == null)
                ICompositorSwapChainInterop_CreateCompositionSurfaceForSwapChain = (delegate* unmanaged[Stdcall]<nint, nint, out nint, HResult>)(*(*(void***)self + 5));
        }

        public static void CacheCompositionDrawingSurfaceInterop(nint self)
        {
            if (ICompositionDrawingSurfaceInterop_BeginDraw == null)
                ICompositionDrawingSurfaceInterop_BeginDraw = (delegate* unmanaged[Stdcall]<nint, nint, ref readonly Guid, out nint, out POINTL, HResult>)(*(*(void***)self + 3));
            if (ICompositionDrawingSurfaceInterop_EndDraw == null)
                ICompositionDrawingSurfaceInterop_EndDraw = (delegate* unmanaged[Stdcall]<nint, HResult>)(*(*(void***)self + 4));
            if (ICompositionDrawingSurfaceInterop_Resize == null)
                ICompositionDrawingSurfaceInterop_Resize = (delegate* unmanaged[Stdcall]<nint, SIZEL, HResult>)(*(*(void***)self + 5));
        }

        public static void CacheD3D11Device(nint self)
        {
            if (ID3D11Device_CreateTexture2D == null)
                ID3D11Device_CreateTexture2D = (delegate* unmanaged[Stdcall]<nint, ref readonly D3D11_TEXTURE2D_DESC, nint, out nint, HResult>)(*(*(void***)self + 5));
            if (ID3D11Device_GetImmediateContext == null)
                ID3D11Device_GetImmediateContext = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, out nint, void>)(*(*(void***)self + 40));
        }

        public static void CacheD3D11Multithread(nint self)
        {
            if (ID3D11Multithread_SetMultithreadProtected == null)
                ID3D11Multithread_SetMultithreadProtected = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, int, HResult>)(*(*(void***)self + 5));
        }

        public static void CacheD3D11DeviceContext(nint self)
        {
            if (ID3D11DeviceContext_CopySubresourceRegion == null)
                ID3D11DeviceContext_CopySubresourceRegion = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, nint, uint, int, int, uint, nint, uint, nint, void>)(*(*(void***)self + 46));
            if (ID3D11DeviceContext_Flush == null)
                ID3D11DeviceContext_Flush = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, void>)(*(*(void***)self + 111));
        }

        public static void CacheDXGIFactory2(nint self)
        {
            if (IDXGIFactory2_CreateSwapChainForComposition == null)
                IDXGIFactory2_CreateSwapChainForComposition = (delegate* unmanaged[Stdcall]<nint, nint, ref readonly DXGI_SWAP_CHAIN_DESC1, nint, out nint, HResult>)(*(*(void***)self + 24));
        }

        public static void CacheDXGISwapChain3(nint self)
        {
            if (IDXGISwapChain3_Present == null)
                IDXGISwapChain3_Present = (delegate* unmanaged[Stdcall]<nint, uint, DXGI_PRESENT, HResult>)(*(*(void***)self + 8));
            if (IDXGISwapChain3_GetBuffer == null)
                IDXGISwapChain3_GetBuffer = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, uint, ref readonly Guid, out nint, HResult>)(*(*(void***)self + 9));
            if (IDXGISwapChain_ResizeBuffers == null)
                IDXGISwapChain_ResizeBuffers = (delegate* unmanaged[Stdcall]<nint, uint, uint, uint, DXGI_FORMAT, uint, HResult>)(*(*(void***)self + 13));
            if (IDXGISwapChain2_SetMatrixTransform == null)
                IDXGISwapChain2_SetMatrixTransform = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, ref readonly Matrix3x2, HResult>)(*(*(void***)self + 34));
            if (IDXGISwapChain3_CheckColorSpaceSupport == null)
                IDXGISwapChain3_CheckColorSpaceSupport = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, DXGI_COLOR_SPACE_TYPE, out DXGI_SWAP_CHAIN_COLOR_SPACE_SUPPORT_FLAG, HResult>)(*(*(void***)self + 37));
            if (IDXGISwapChain3_SetColorSpace1 == null)
                IDXGISwapChain3_SetColorSpace1 = (delegate* unmanaged[Stdcall, SuppressGCTransition]<nint, DXGI_COLOR_SPACE_TYPE, HResult>)(*(*(void***)self + 38));
        }
    }
}
