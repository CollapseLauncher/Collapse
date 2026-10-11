using System;
using Windows.Graphics.DirectX.Direct3D11;
using Microsoft.UI.Xaml;

namespace CollapseLauncher.XAMLs.Theme.CustomControls.VideoPresenter;

public delegate void Direct3DSurfaceConsumer(IDirect3DSurface surface);
public delegate void Direct3DSurfaceConsumerUnsafe(nint surfaceAbi);

public interface IVideoFramePresenter : IDisposable
{
    Visibility Visibility { get; set; }

    void Draw(Direct3DSurfaceConsumer surfaceConsumer,
              int                     canvasWidth,
              int                     canvasHeight);

    void DrawUnsafe(Direct3DSurfaceConsumerUnsafe surfaceConsumerUnsafe,
                    int                           canvasWidth,
                    int                           canvasHeight);
}
