using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan FadeOutDelay = FadeDuration * 0.1d;

    internal static async Task StartFadeReplaceAnimationAsync(
        UIElement? fromElement,
        UIElement? toElement,
        Action?    onCompleted = null)
    {
        Storyboard? fromStoryboard = null;
        Storyboard? toStoryboard   = null;

        if (fromElement != null)
        {
            fromStoryboard = CreateFadeOpacityStoryboard(fromElement,
                                                         1,
                                                         0,
                                                         FadeDuration,
                                                         FadeOutDelay);
        }

        if (toElement != null)
        {
            toStoryboard = CreateFadeOpacityStoryboard(toElement,
                                                       0,
                                                       1,
                                                       FadeDuration);
        }

        Task fromTcs = fromStoryboard?.BeginAsync() ?? Task.CompletedTask;
        Task toTcs   = toStoryboard?.BeginAsync() ?? Task.CompletedTask;

        await Task.WhenAll(fromTcs, toTcs);
        onCompleted?.Invoke();
    }

    private static Storyboard CreateFadeOpacityStoryboard(
        UIElement element,
        double    from,
        double    to,
        TimeSpan  duration,
        TimeSpan  delay = default)
    {
        Storyboard storyboard = new();
        DoubleAnimation opacityAnimation = new()
        {
            From      = from,
            To        = to,
            Duration  = duration,
            BeginTime = delay
        };

        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);

        return storyboard;
    }

    private static Task BeginAsync(this Storyboard sb)
    {
        TaskCompletionSource tcs = new();
        sb.Completed += SbOnCompleted;

        try
        {
            sb.Begin();
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }

        return tcs.Task;

        void SbOnCompleted(object? sender, object e)
        {
            sb.Completed -= SbOnCompleted;
            tcs.SetResult();
        }
    }
}
