using CollapseLauncher.Extension;
using Hi3Helper;
using Microsoft.UI.Xaml;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;

public partial class LayeredBackgroundImage
{
    #region Fields

    private CancellationTokenSource? _videoPlayerPlayPauseCts;
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
                                 // Note by @neon-nyan:
                                 //
                                 // Video Player is expected to be disposed on pause (yes, it looked ugly when it started for the first time).
                                 // This is intended because I don't want to waste our user's 800 MB VRAM just to stupidly allocating
                                 // it on Video Player for nothing.
                                 //
                                 // If only MicroSLOP wanted to do their own job properly by deallocating their unmanaged memory for certain
                                 // amount of standby time (if they ever think of it), I would be fine to not disposing the Video Player.
                                 //
                                 // I COMPLETELY DISAGREE with keeping it NOT disposable, unless we have our own way to make the Video Player
                                 // deallocate their VRAM allocation when standby.
                                 //
                                 // I'm sorry for ranting on this comment but considering this is already nearly 2 months in the works.
                                 // It's just so frustrating to get this nearly ready to be shipped, only to find something is changed and
                                 // it broke things. I can't make any other decision unless it's other thing we might consider for future plan.
                                 // Unless if you guys wanted to fix every single line of these codes, I would be VERY HAPPY to see it.
                                 //
                                 // Now, I just want to be very honest with you guys.
                                 // If only I don't have a severe depression due to tragedy on my real life, I wouldn't be overthinking this
                                 // and this PR already ruined not only my overall work time, but also my own sanity and mental health.
                                 // (And yeah, this is what severe depression will do to you guys. Wish you won't have the same fate as mine)
                                 // 
                                 // Once again, sorry for ranting a bit and, Thank you for hearing this.
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

        UIElementExtensions.RunFunctionFromUIThread(() => instance._videoPlayer.Volume = (double)e.NewValue);
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
