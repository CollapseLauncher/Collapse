using Microsoft.Graphics.Canvas;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    public void ToggleMute(bool isMute)
    {
        if (!TryGetBackgroundSourceContext(out _, out MediaPlayer? player))
            return;

        if (isMute)
        {
            player.IsMuted = true;
            return;
        }

        player.IsMuted = false;
    }

    public void SetVolume(double volume)
    {
        if (!TryGetBackgroundSourceContext(out SourceContext? sourceContext, out _))
            return;

        sourceContext.SetVolume(volume);
    }

    public void Play()
    {
        if (!TryGetBackgroundSourceContext(out SourceContext? sourceContext, out MediaPlayer? player))
            return;

        player.Play();
        MediaPlayer_RaiseStateUpdate(player);
        sourceContext.SetVolume(AudioVolume);
    }

    public void Pause()
    {
        if (!TryGetBackgroundSourceContext(out SourceContext? sourceContext, out MediaPlayer? player))
            return;

        // Ignore if the player is already in paused, closed or stopped state.
        if (player.CurrentState
            is MediaPlayerState.Paused
            or MediaPlayerState.Closed
            or MediaPlayerState.Stopped)
            return;

        // Use SourceContext's SetVolume onCompleted event to perform fade-out then pause action.
        sourceContext.SetVolume(0d, () =>
        {
            player.Pause();
            MediaPlayer_RaiseStateUpdate(player);
        });
    }

    public void RestoreViewAllocation()
    {
        LoadAllPresenters();
    }

    public void ReleaseViewAllocation()
    {
        UnloadAllPresenters();
    }

    public async Task<CanvasRenderTarget?> CopyCurrentFrame(bool includeBackground, bool includeForeground)
    {
        CanvasRenderTarget? backgroundCanvas = null;
        CanvasRenderTarget? foregroundCanvas = null;

        try
        {
            if (includeForeground &&
                TryGetPresenterSourceContextFromParent(_foregroundGrid, out SourceContext? foregroundSourceContext))
                foregroundCanvas = await foregroundSourceContext.CopyCurrentFrame();

            if (includeBackground &&
                TryGetPresenterSourceContextFromParent(_backgroundGrid, out SourceContext? backgroundSourceContext))
                backgroundCanvas = await backgroundSourceContext.CopyCurrentFrame();

            if (backgroundCanvas == null && foregroundCanvas == null)
                return null;

            BitmapSize backgroundSize = backgroundCanvas?.SizeInPixels ?? default;
            BitmapSize foregroundSize = foregroundCanvas?.SizeInPixels ?? default;
            float      maxWidth       = Math.Max(backgroundSize.Width,  foregroundSize.Width);
            float      maxHeight      = Math.Max(backgroundSize.Height, foregroundSize.Height);

            DirectXPixelFormat pixelFormat = (DirectXPixelFormat)Math.Max((int)(backgroundCanvas?.Format ?? 0),
                                                                          (int)(foregroundCanvas?.Format ?? 0));

            CanvasDevice       canvasDevice = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget canvasTarget = new(canvasDevice, maxWidth, maxHeight, 96, pixelFormat, CanvasAlphaMode.Premultiplied);

            using CanvasDrawingSession cds = canvasTarget.CreateDrawingSession();
            DrawUniformFill(cds, backgroundCanvas, maxWidth, maxHeight);
            DrawUniformFill(cds, foregroundCanvas, maxWidth, maxHeight);

            return canvasTarget;
        }
        finally
        {
            backgroundCanvas?.Dispose();
            foregroundCanvas?.Dispose();
        }

        static void DrawUniformFill(
            CanvasDrawingSession ds,
            CanvasBitmap?        source,
            float                destWidth,
            float                destHeight)
        {
            if (source == null)
                return;

            float srcWidth  = source.SizeInPixels.Width;
            float srcHeight = source.SizeInPixels.Height;

            float scale = Math.Max(destWidth / srcWidth, destHeight / srcHeight);

            float drawWidth  = srcWidth * scale;
            float drawHeight = srcHeight * scale;

            float offsetX = (destWidth - drawWidth) * 0.5f;
            float offsetY = (destHeight - drawHeight) * 0.5f;

            Rect destRect = new(offsetX, offsetY, drawWidth, drawHeight);

            ds.DrawImage(source,
                         destRect,
                         new Rect(0, 0, srcWidth, srcHeight),
                         1f,
                         CanvasImageInterpolation.HighQualityCubic,
                         CanvasComposite.SourceOver);
        }
    }

    private void MediaPlayer_RaiseStateUpdate(MediaPlayer player)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            SetValue(MediaStateProperty, player.CurrentState);
        });
    }
}
