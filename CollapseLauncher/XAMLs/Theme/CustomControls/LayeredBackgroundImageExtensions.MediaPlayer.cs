using Hi3Helper;
using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Windows.Media.Playback;
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
            CancellationTokenSource currentCts   = CancellationTokenSource.CreateLinkedTokenSource(coopToken);
            CancellationToken       currentToken = currentCts.Token;

            fromValue /= 100d;
            toValue /= 100d;

            double timeDelta = resolutionMs / durationMs;
            double step      = Math.Abs(Math.Max(fromValue, toValue) * timeDelta);

            Unsafe.SkipInit(out Timer timer); // HACK: Ignore Error CS0165
            timer = new Timer(Impl, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(resolutionMs));

            return;

            void Impl(object? state)
            {
                try
                {
                    if (timer == null! ||
                        currentToken.IsCancellationRequested)
                    {
                        timer?.Dispose();
                        onCompleted?.Invoke();
                        return;
                    }

                    durationMs -= resolutionMs;
                    fromValue += step;
                    if (durationMs < 0)
                    {
                        if (mediaPlayer != null!)
                        {
                            TrySetVolume(toValue);
                        }
                        timer.Dispose();
                        onCompleted?.Invoke();
                        return;
                    }

                    if (mediaPlayer != null!)
                    {
                        TrySetVolume(fromValue);
                    }
                }
                catch (Exception e)
                {
                    timer.Dispose();
                    onCompleted?.Invoke();
                    Logger.LogWriteLine($"[LayeredBackgroundImage::VolumeFadeCore::Impl] {e}",
                                        LogType.Error,
                                        true);
                }
            }

            void TrySetVolume(double value)
            {
                try
                {
                    mediaPlayer.Volume = value;
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
