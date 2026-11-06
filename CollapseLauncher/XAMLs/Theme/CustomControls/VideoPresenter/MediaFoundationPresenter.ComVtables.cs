using Hi3Helper.Win32.Native.Enums.DXGI;
using Hi3Helper.Win32.Native.Structs;
using System;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;

public sealed partial class MediaFoundationPresenter
{
    private static unsafe class ComVtables
    {
        public static delegate* unmanaged[Stdcall, SuppressGCTransition]
            <nint, nint, ref readonly Guid, out nint, out POINTL, int> ICompositionDrawingSurfaceInterop_BeginDraw;

        public static delegate* unmanaged[Stdcall, SuppressGCTransition]
            <nint, int> ICompositionDrawingSurfaceInterop_EndDraw;

        public static delegate* unmanaged[Stdcall, SuppressGCTransition]
            <nint, nint, uint, int, int, uint, nint, uint, nint, void> ID3D11DeviceContext_CopySubresourceRegion;

        public static delegate* unmanaged[Stdcall, SuppressGCTransition]
            <nint, void> ID3D11DeviceContext_Flush;

        public static delegate* unmanaged[Stdcall, SuppressGCTransition]
            <nint, uint, ref readonly Guid, out nint, int> IDXGISwapChain3_GetBuffer;

        public static delegate* unmanaged[Stdcall, SuppressGCTransition]
            <nint, uint, DXGI_PRESENT, int> IDXGISwapChain3_Present;
    }
}
