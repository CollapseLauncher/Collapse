using Hi3Helper;
using Microsoft.UI.Xaml;
using System;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Fields

    private CancellationTokenSource? _videoPlayerPlayPauseCts;

    #endregion

    #region Play and Pause Control

    public void Play()
    {
        CancellationTokenSource? lastCts = Interlocked.Exchange(ref _videoPlayerPlayPauseCts, new CancellationTokenSource());
        lastCts?.Cancel();
        lastCts?.Dispose();

        InitializeAndPlayVideoView(blockIfAlreadyPlayed: true,
                                   volumeFadeDurationMs: 150d,
                                   volumeFadeResolutionMs: 10d,
                                   token: _videoPlayerPlayPauseCts.Token);
    }

    public void Pause()
    {
        if (!IsLoaded ||
            _videoPlayer == null! ||
            !_videoPlayer.CanPause)
        {
            return;
        }

        CancellationTokenSource? lastCts = Interlocked.Exchange(ref _videoPlayerPlayPauseCts, new CancellationTokenSource());
        lastCts?.Cancel();
        lastCts?.Dispose();

        DisposeAndPauseVideoView(blockIfAlreadyPaused: true,
                                 volumeFadeDurationMs: 150d,
                                 volumeFadeResolutionMs: 10d,
                                 disposeRenderImageSource: false, // Do not dispose Image Source
                                 token: _videoPlayerPlayPauseCts.Token);
    }

    #endregion

    #region Duration Position Control

    private static void MediaDurationPosition_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
            if (e.NewValue is not TimeSpan value)
            {
                return;
            }

            if (instance._videoPlayer != null! &&
                instance._videoPlayer.CanSeek)
            {
                instance._videoPlayer.Position = value;
            }

            if (TryGetSourceHashCode(instance.BackgroundSource, out int hashCode))
            {
                SharedLastMediaPosition.AddOrUpdate(hashCode, _ => value, (_, _) => value);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::MediaDurationPosition_OnChanged] {ex}",
                                LogType.Error,
                                true);
        }
    }

    #endregion
}
