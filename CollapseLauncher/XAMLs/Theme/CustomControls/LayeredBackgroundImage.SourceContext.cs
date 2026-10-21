using CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;
using Hi3Helper;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.Interfaces.D3D;
using Hi3Helper.Win32.Native.Interfaces.DXGI;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Hi3Helper.Win32.Native.Structs.DXGI;
using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Media.Playback;
using WinRT;
// ReSharper disable InconsistentNaming

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private readonly Dictionary<TimeSpan, TimeSpan> LastMediaSourcePosition = [];

    public partial class SourceContext : IDisposable
    {
        private static readonly ConcurrentDictionary<SourceContext, bool> LoadedContexts = [];

        private static readonly Guid IID_IMediaPlayer5 = new("cfe537fd-f86a-4446-bf4d-c8e792b7b4b3");

        public ulong Generation;

        public object? Source          { get; set; }
        public bool    IsDisposeSource { get; set; }

        public LayeredBackgroundImage ParentElement { get; init; }

        private nint _playerAbi;
        public MediaPlayer? Player
        {
            get;
            set
            {
                nint oldPlayerAbi = Interlocked.Exchange(ref _playerAbi, nint.Zero);
                if (oldPlayerAbi != nint.Zero) Marshal.Release(oldPlayerAbi);

                if (value == null)
                {
                    field?.MediaOpened -= PlayerOnUpdateCanvasSize;
                }
                else
                {
                    value.MediaOpened += PlayerOnUpdateCanvasSize;
                }

                (value as IWinRTObject)?.NativeObject?.TryAs(IID_IMediaPlayer5, out _playerAbi);
                field = value;
            }
        }

        public TypedEventHandler<MediaPlayer, object>?          CurrentStateChangedCallback { get; set; }
        public TypedEventHandler<MediaPlaybackSession, object>? PositionChangedCallback     { get; set; }


        public object? MediaSource { get; set; }

        public MediaFoundationPresenter? PlayerPresenter
        {
            get;
            set
            {
                if (value == null)
                {
                    Player?.VideoFrameAvailable -= PlayerOnVideoFrameAvailable;
                }
                else
                {
                    Player?.VideoFrameAvailable += PlayerOnVideoFrameAvailable;
                }

                field = value;
            }
        }

        public CancellationTokenSource SourceLoadingCts = new();

        private CancellationTokenSource? _volumeChangedCts;
        private bool                     _disposed;

        private int _cachedPlayerWidth;
        private int _cachedPlayerHeight;

        public SourceContext(LayeredBackgroundImage parentElement, ulong generation)
        {
            LoadedContexts.TryAdd(this, true);
            ParentElement = parentElement;
            Generation    = generation;
        }

        private void PlayerOnUpdateCanvasSize(MediaPlayer sender, object args)
        {
            _cachedPlayerWidth  = (int)sender.PlaybackSession.NaturalVideoWidth;
            _cachedPlayerHeight = (int)sender.PlaybackSession.NaturalVideoHeight;
        }

        private void PlayerOnVideoFrameAvailable(MediaPlayer sender, object args)
        {
            PlayerPresenter?.DrawUnsafe(DrawConsumer, _cachedPlayerWidth, _cachedPlayerHeight);
            return;

            unsafe void DrawConsumer(nint surfaceAbi)
                => ((delegate* unmanaged[MemberFunction]<nint, nint, int>)(*(*(void***)_playerAbi + 10)))(_playerAbi, surfaceAbi);
        }

        public void RegisterPlayerCallbacks(
            TypedEventHandler<MediaPlayer, object>          currentStateChangedCallback,
            TypedEventHandler<MediaPlaybackSession, object> positionChangedCallback)
        {
            if (Player is not { } player)
            {
                return;
            }

            // -- Un-register previous callbacks before assigning a new one.
            UnregisterPlayerCallbacks();

            player.PlaybackSession.PositionChanged += PositionChangedCallback     = positionChangedCallback;
            player.CurrentStateChanged             += CurrentStateChangedCallback = currentStateChangedCallback;
        }

        public void UnregisterPlayerCallbacks()
        {
            if (Player is not { } player)
            {
                return;
            }

            if (CurrentStateChangedCallback != null) player.CurrentStateChanged         -= CurrentStateChangedCallback;
            if (PositionChangedCallback != null) player.PlaybackSession.PositionChanged -= PositionChangedCallback;
        }

        public void SetVolume(double volume, Action? onCompleted = null)
        {
            if (Player is null)
            {
                onCompleted?.Invoke();
                return;
            }

            if (Player.IsMuted)
            {
                Player.Volume = volume;
                onCompleted?.Invoke();
                return;
            }

            CancellationToken token = CancellationToken.None;
            if (Interlocked.Exchange(ref _volumeChangedCts, new CancellationTokenSource()) is { } lastCts)
            {
                lastCts.Cancel();
                lastCts.Dispose();
                token = _volumeChangedCts.Token;
            }

            Player.SetVolumeFadeCore(Player.Volume, volume, onCompleted: onCompleted, coopToken: token);
        }

        public void SavePlayerLastPosition()
        {
            if (Player is not { } player)
            {
                return;
            }

            ParentElement.LastMediaSourcePosition[player.NaturalDuration] = player.Position;
        }

        public TimeSpan LoadPlayerLastPosition()
        {
            if (Player is not { } player)
            {
                return TimeSpan.Zero;
            }

            return ParentElement.LastMediaSourcePosition.GetValueOrDefault(player.NaturalDuration);
        }

        public async Task<CanvasRenderTarget?> CopyCurrentFrame()
            => PlayerPresenter?.CopyCurrentFrame() ??
               await TryCopyImageFrameFromBitmapSource();

        private async Task<CanvasRenderTarget?> TryCopyImageFrameFromBitmapSource()
        {
            if (MediaSource is not BitmapImage bitmapImage)
            {
                return null;
            }

            if (Source is string localPath &&
                Uri.TryCreate(localPath, UriKind.Absolute, out Uri? localPathUri) &&
                localPathUri.IsFile)
            {
                return await TryCopyImageFrameFromLocalUri(localPathUri);
            }

            nint bitmapImageAbi = ((IWinRTObject)bitmapImage).NativeObject.ThisPtr;
            Unsafe.SkipInit(out IDirect3DDxgiInterfaceAccess? access);
            Unsafe.SkipInit(out IDXGISurface? dxgiSurface);
            Unsafe.SkipInit(out nint accessAbi);
            Unsafe.SkipInit(out nint dxgiSurfaceAbi);
            Unsafe.SkipInit(out nint graphicsSurfaceAbi);

            try
            {
                HResult hr = Marshal.QueryInterface(bitmapImageAbi, typeof(IDirect3DDxgiInterfaceAccess).GUID, out accessAbi);
                if (!hr || !ComMarshal<IDirect3DDxgiInterfaceAccess>
                       .TryCreateComObjectFromReference(bitmapImageAbi,
                                                        out access,
                                                        out _,
                                                        releaseReference: false,
                                                        useUnique: true))
                {
                    return null;
                }

                access.GetInterface(typeof(IDXGISurface).GUID, out dxgiSurfaceAbi);
                hr = PInvoke.CreateDirect3D11SurfaceFromDXGISurface(dxgiSurfaceAbi, out graphicsSurfaceAbi);

                if (!ComMarshal<IDXGISurface>
                       .TryCreateComObjectFromReference(graphicsSurfaceAbi,
                                                        out dxgiSurface,
                                                        out _,
                                                        releaseReference: false,
                                                        useUnique: true))
                {
                    return null;
                }

                dxgiSurface.GetDesc(out DXGI_SURFACE_DESC dxgiSurfaceDesc);

                if (!hr) return null;

                using IDirect3DSurface surface = MarshalInterface<IDirect3DSurface>.FromAbi(graphicsSurfaceAbi);

                CanvasDevice       device                  = CanvasDevice.GetSharedDevice();
                using CanvasBitmap canvasBitmapFromSurface = CanvasBitmap.CreateFromDirect3D11Surface(device, surface);
                CanvasRenderTarget target = new(device,
                                                dxgiSurfaceDesc.Width,
                                                dxgiSurfaceDesc.Height,
                                                96,
                                                (DirectXPixelFormat)dxgiSurfaceDesc.Format,
                                                CanvasAlphaMode.Premultiplied);

                target.CopyPixelsFromBitmap(canvasBitmapFromSurface);
                return target;
            }
            finally
            {
                if (graphicsSurfaceAbi != nint.Zero) Marshal.Release(graphicsSurfaceAbi);
                if (dxgiSurfaceAbi != nint.Zero) Marshal.Release(dxgiSurfaceAbi);
                ComMarshal.FinalRelease(dxgiSurface);
                ComMarshal.FinalRelease(access);
                if (accessAbi != nint.Zero) Marshal.Release(accessAbi);
            }

            static async Task<CanvasRenderTarget?> TryCopyImageFrameFromLocalUri(Uri uri)
            {
                if (!uri.IsFile ||
                    !File.Exists(uri.LocalPath))
                {
                    return null;
                }

                CanvasDevice       device = CanvasDevice.GetSharedDevice();
                using CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, uri.LocalPath);
                CanvasRenderTarget target = new(device,
                                                bitmap.SizeInPixels.Width,
                                                bitmap.SizeInPixels.Height,
                                                bitmap.Dpi,
                                                bitmap.Format,
                                                bitmap.AlphaMode);

                target.CopyPixelsFromBitmap(bitmap);
                return target;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, true))
                return;

            LoadedContexts.TryRemove(this, out _);
            Console.WriteLine($"Total Remained SourceContext instances: {LoadedContexts.Count} instances", LogType.Debug);

            GC.SuppressFinalize(this); // Early suppress finalization here.

            _volumeChangedCts?.Cancel();
            _volumeChangedCts?.Dispose();
            _volumeChangedCts = null;

            SourceLoadingCts.Cancel();
            SourceLoadingCts.Dispose();
            SourceLoadingCts = null!;

            Player?.MediaOpened         -= PlayerOnUpdateCanvasSize;
            Player?.VideoFrameAvailable -= PlayerOnVideoFrameAvailable;
            Player?.Pause();

            UnregisterPlayerCallbacks();
            SavePlayerLastPosition();

            // Skidaddle Skidoddle, your COM object is now a noodle!
            // -- Early dispose the underneath ABI earlier for MediaPlayer and supposedly,
            //    WinRT's MediaSource objects.
            nint oldPlayerAbi = Interlocked.Exchange(ref _playerAbi, nint.Zero);
            if (oldPlayerAbi != nint.Zero) Marshal.Release(oldPlayerAbi);
            DisposeObject(Player);
            Player = null;

            DisposeObject(MediaSource);
            MediaSource = null;

            if (IsDisposeSource)
            {
                DisposeObject(Source);
                Source = null;
            }

            DisposeObject(PlayerPresenter);
            PlayerPresenter = null;
        }

        private static void DisposeObject(object? obj)
        {
            (obj as IDisposable)?.Dispose();
        }
    }
}
