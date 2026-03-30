using CollapseLauncher.Extension;
using Hi3Helper;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Media.Playback;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    #region Fields

    private CancellationTokenSource? _videoPlayerPlayPauseCts;
    private long                     _videoPlayerPosRefreshLastTick = Environment.TickCount64;

    private enum VideoState
    {
        Paused,
        RequestedPlay,
        Playing,
        RequestedPause,
    }
    private VideoState _videoState;

    #endregion

    #region Play and Pause Control

    public void Play()
    {
        if (_lastBackgroundSourceType == MediaSourceType.Image)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _videoState, VideoState.RequestedPlay, VideoState.Paused) !=
            VideoState.Paused)
            return;

        CancellationTokenSource? lastCts = Interlocked.Exchange(ref _videoPlayerPlayPauseCts, new CancellationTokenSource());
        lastCts?.Cancel();
        lastCts?.Dispose();

        InitializeAndPlayVideoView(volumeFadeDurationMs: 150d,
                                   volumeFadeResolutionMs: 10d,
                                   token: _videoPlayerPlayPauseCts.Token);

        Interlocked.Exchange(ref _videoState, VideoState.Playing);
    }

    public void Pause()
    {
        if (_lastBackgroundSourceType == MediaSourceType.Image)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _videoState, VideoState.RequestedPause, VideoState.Playing) !=
            VideoState.Playing)
            return;

        if (!IsLoaded ||
            _videoPlayer == null! ||
            !_videoPlayer.CanPause)
        {
            Paused();
            return;
        }

        CancellationTokenSource? lastCts =
            Interlocked.Exchange(ref _videoPlayerPlayPauseCts, new CancellationTokenSource());
        lastCts?.Cancel();
        lastCts?.Dispose();

        if (CanUseStaticBackground)
        {
            BackgroundSource_UseStatic(this);
        }

        DisposeAndPauseVideoView(actionAfterPause: Paused,
                                 volumeFadeDurationMs: 150d,
                                 volumeFadeResolutionMs: 10d,
                                 // disposeVideoPlayer: CanUseStaticBackground,
                                 disposeRenderImageSource: false, // Do not dispose Image Source
                                 token: _videoPlayerPlayPauseCts.Token);
        return;

        void Paused()
        {
            Interlocked.Exchange(ref _videoState, VideoState.Paused);
        }
    }

    #endregion

    #region Fade In/Out Audio Control

    public void FadeInAudio(double            volumeFadeDurationMs   = 500d,
                            double            volumeFadeResolutionMs = 10d, 
                            CancellationToken coopToken              = default)
    {
        if (_videoPlayer == null!)
        {
            return;
        }

        StartVideoPlayerVolumeFade(Math.Max(0, _videoPlayer.Volume * 100d),
                                   AudioVolume,
                                   volumeFadeDurationMs,
                                   volumeFadeResolutionMs,
                                   coopToken: coopToken);
    }

    public void FadeOutAudio(Action?           actionAfterPause       = null,
                             double            volumeFadeDurationMs   = 500d,
                             double            volumeFadeResolutionMs = 10d,
                             CancellationToken coopToken              = default)
    {
        if (_videoPlayer == null!)
        {
            return;
        }

        StartVideoPlayerVolumeFade(Math.Min(AudioVolume, _videoPlayer.Volume * 100d),
                                   0,
                                   volumeFadeDurationMs,
                                   volumeFadeResolutionMs,
                                   true,
                                   onAfterAction: actionAfterPause,
                                   coopToken: coopToken);
    }

    private void StartVideoPlayerVolumeFade(double fromValue,
                                            double toValue,
                                            double durationMs,
                                            double resolutionMs,
                                            bool fadeOut = false,
                                            Action? onAfterAction = null,
                                            CancellationToken coopToken = default)
    {
        new Thread(() =>
                       StartVideoPlayerVolumeFadeCore(fromValue,
                                                      toValue,
                                                      durationMs,
                                                      resolutionMs,
                                                      fadeOut,
                                                      onAfterAction,
                                                      coopToken))
        {
            IsBackground = true
        }.Start();
    }

    public double ActualVolume
    {
        get => (double)GetValue(ActualVolumeProperty);
        set => SetValue(ActualVolumeProperty, value);
    }

    public static readonly DependencyProperty ActualVolumeProperty =
        DependencyProperty.Register(nameof(ActualVolume),
                                    typeof(double),
                                    typeof(LayeredBackgroundImage),
                                    new PropertyMetadata(0d, SetAudioVolumeBridge));

    private static void SetAudioVolumeBridge(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
        if (instance._videoPlayer == null!)
        {
            return;
        }

        DispatcherQueueExtensions.TryEnqueue(() => instance._videoPlayer.Volume = (double)e.NewValue);
    }

    private void StartVideoPlayerVolumeFadeCore(double fromValue,
                                                double toValue,
                                                double durationMs,
                                                double resolutionMs,
                                                bool fadeOut = false,
                                                Action? onAfterAction = null,
                                                CancellationToken coopToken = default)
    {
        try
        {
            CancellationTokenSource  currentCts = CancellationTokenSource.CreateLinkedTokenSource(coopToken);
            CancellationTokenSource? prevCts    = Interlocked.Exchange(ref _videoPlayerFadeCts, currentCts);
            prevCts?.Cancel();
            prevCts?.Dispose();

            CancellationToken currentToken = currentCts.Token;

            fromValue /= 100d;
            toValue /= 100d;

            double timeDelta = resolutionMs / durationMs;
            double step = Math.Max(fromValue, toValue) * timeDelta;
            step = fadeOut ? -step : step;

            Unsafe.SkipInit(out Timer timer); // HACK: Ignore Error CS0165
            timer = new Timer(Impl, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(resolutionMs));

            return;

            [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
            void Impl(object? state)
            {
                try
                {
                    // ReSharper disable once AccessToModifiedClosure
                    if (timer == null! ||
                        currentToken.IsCancellationRequested)
                    {
                        timer?.Dispose();
                        onAfterAction?.Invoke();
                        return;
                    }

                    durationMs -= resolutionMs;
                    fromValue += step;
                    if (durationMs < 0)
                    {
                        if (_videoPlayer != null!)
                        {
                            TrySetVolume(toValue);
                        }
                        timer.Dispose();
                        onAfterAction?.Invoke();
                        return;
                    }

                    if (_videoPlayer != null!)
                    {
                        TrySetVolume(fromValue);
                    }
                }
                catch (Exception e)
                {
                    timer.Dispose();
                    onAfterAction?.Invoke();
                    Logger.LogWriteLine($"[LayeredBackgroundImage::StartVideoPlayerVolumeFadeCore::Impl] {e}",
                                        LogType.Error,
                                        true);
                }
            }

            void TrySetVolume(double value)
            {
                try
                {
                    _videoPlayer.Volume = value;
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::StartVideoPlayerVolumeFadeCore::TrySetVolume] {e}",
                                        LogType.Error,
                                        true);
                }
            }
        }
        catch (Exception e)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::StartVideoPlayerVolumeFadeCore] {e}",
                                LogType.Error,
                                true);
        }
    }

    #endregion

    #region Duration Position Control

    [SkipLocalsInit]
    private void MediaDurationPosition_OnChangedBridge(MediaPlaybackSession sender, object args)
    {
        long thisTick = Environment.TickCount64;
        long lastTick = _videoPlayerPosRefreshLastTick;

        // Only updates a second at a time to avoid UI thread overhead due to extensive update.
        if (thisTick - lastTick <= 1000 ||
            Interlocked.CompareExchange(ref _videoPlayerPosRefreshLastTick,
                                        thisTick,
                                        lastTick) != lastTick)
        {
            return;
        }

        if (DispatcherQueue.IsObjectDisposed())
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => SetValue(MediaDurationPositionProperty, sender.Position));
    }

    private static void MediaDurationPosition_OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        try
        {
            LayeredBackgroundImage instance = (LayeredBackgroundImage)d;
            if (e.NewValue is not TimeSpan value)
            {
                return;
            }

            SaveMediaPosition(instance.BackgroundSource, value);
            bool isParamForSeek = value < TimeSpan.Zero; // Mark for seeking to video player if negated

            // Skip if the value is not negated (not for seeking)
            if (!isParamForSeek) return;

            value = value.Negate();
            if (value.TotalSeconds < 1) // Clamp to 1 second if clip is not zero but less than a second.
            {
                value = TimeSpan.Zero;
            }
            instance._videoPlayer.Position = value;
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[LayeredBackgroundImage::MediaDurationPosition_OnChanged] {ex}",
                                LogType.Error,
                                true);
        }
    }

    [SkipLocalsInit]
    private static bool SaveMediaPosition(object? source, TimeSpan timeSpan)
    {
        if (!TryGetSourceHashCode(source, out int hashCode))
        {
            return false;
        }

        ref TimeSpan span = ref CollectionsMarshal.GetValueRefOrAddDefault(SharedLastMediaPosition, hashCode, out _);
        span = timeSpan;

        return true;
    }

    #endregion
}
