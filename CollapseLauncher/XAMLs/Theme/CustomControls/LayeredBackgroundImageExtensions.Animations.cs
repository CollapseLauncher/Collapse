using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan FadeOutDelay = FadeDuration * 0.5d;

    internal static Task StartFadeReplaceAnimationAsync(
        UIElement? fromElement,
        UIElement? toElement,
        Action?    onCompleted = null)
    {
        if (fromElement == null &&
            toElement == null)
        {
            onCompleted?.Invoke();
            return Task.CompletedTask;
        }

        TaskCompletionSource tcs = new();

        Storyboard mainStoryboard = new();
        mainStoryboard.Completed += MainStoryboardOnCompleted;

        CreateFadeOpacityTimeline(mainStoryboard, fromElement, 1, 0, FadeDuration, FadeOutDelay);
        CreateFadeOpacityTimeline(mainStoryboard, toElement,   0, 1, FadeDuration);
        mainStoryboard.Begin();
        return tcs.Task;

        void MainStoryboardOnCompleted(object? sender, object e)
        {
            mainStoryboard.Completed -= MainStoryboardOnCompleted;
            onCompleted?.Invoke();
            tcs.SetResult();
        }
    }

    private static void CreateFadeOpacityTimeline(
        Storyboard storyboard,
        UIElement? element,
        double     from,
        double     to,
        TimeSpan   duration,
        TimeSpan   delay = default)
    {
        if (element == null)
            return;

        DoubleAnimation opacityAnimation = new()
        {
            From                     = from,
            To                       = to,
            Duration                 = duration,
            BeginTime                = delay,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);
    }
}
