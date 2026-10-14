using Hi3Helper;
using System;
using System.Threading;
using System.Timers;
using Windows.Media.Playback;
using Timer = System.Timers.Timer;

// ReSharper disable AccessToModifiedClosure

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    private const double VolumeFadeDuration   = 500d; // milliseconds
    private const double VolumeFadeResolution = 10d;  // milliseconds

    internal static void SetVolumeFadeCore(
        this MediaPlayer  mediaPlayer,
        double            fromValue,
        double            toValue,
        double            durationMs   = VolumeFadeDuration,
        double            resolutionMs = VolumeFadeResolution,
        Action?           onCompleted  = null,
        CancellationToken coopToken    = default)
    {
        try
        {
            bool isFadeOut = fromValue > toValue;

            double timeDelta = resolutionMs / durationMs;
            double step      = Math.Abs(Math.Max(fromValue, toValue) * timeDelta);

            if (isFadeOut)
                step *= -1;

            if (coopToken.IsCancellationRequested || mediaPlayer == null!)
            {
                onCompleted?.Invoke();
                return;
            }

            Timer timer = new(TimeSpan.FromMilliseconds(resolutionMs));
            coopToken.Register(timer.Dispose);
            timer.Elapsed += Impl;
            timer.Start();

            return;

            void Impl(object? sender, ElapsedEventArgs args)
            {
                if (coopToken.IsCancellationRequested || mediaPlayer == null!)
                {
                    timer.Stop();
                    timer.Dispose();
                    return;
                }

                try
                {
                    fromValue += step;
                    TrySetVolume(fromValue);

                    if ((isFadeOut && fromValue <= toValue) ||
                        (!isFadeOut && fromValue >= toValue))
                    {
                        timer.Stop();
                        timer.Dispose();
                        onCompleted?.Invoke();
                    }
                    durationMs -= resolutionMs;
                }
                catch (Exception ex)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VolumeFadeCore::Impl] {ex}",
                                        LogType.Error,
                                        true);
                }
            }

            void TrySetVolume(double value)
            {
                try
                {
                    mediaPlayer.Volume = isFadeOut
                        ? Math.Max(value, toValue)
                        : Math.Min(value, toValue);
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VolumeFadeCore::TrySetVolume] {e}",
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
}
