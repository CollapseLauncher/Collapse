using CommunityToolkit.WinUI.Animations;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

internal static partial class LayeredBackgroundImageExtensions
{
    private static readonly TimeSpan FadeDuration = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan FadeOutDelay = TimeSpan.FromSeconds(0.125);

    internal static async Task StartFadeReplaceAnimationAsync(
        UIElement? fromElement,
        UIElement? toElement,
        Action?    onCompleted = null)
    {
        Storyboard? fromStoryboard = null;
        Storyboard? toStoryboard = null;

        if (fromElement != null)
        {
            fromStoryboard = CreateFadeOpacityStoryboard(fromElement, 1, 0, FadeDuration);
        }

        if (toElement != null)
        {
            toStoryboard = CreateFadeOpacityStoryboard(toElement, 0, 1, FadeDuration);
        }

        Storyboard? storyboardToSub = toStoryboard ?? fromStoryboard;
        if (onCompleted != null &&
            storyboardToSub != null)
        {
            storyboardToSub.Completed += StoryboardToSubOnCompleted;
        }

        Task fromTcs = fromStoryboard?.BeginAsync() ?? Task.CompletedTask;
        Task toTcs = toStoryboard?.BeginAsync() ?? Task.CompletedTask;

        await Task.WhenAll(fromTcs, toTcs);
        return;

        void StoryboardToSubOnCompleted(object? sender, object e)
        {
            if (sender is not Storyboard sb)
            {
                return;
            }

            sb.Completed -= StoryboardToSubOnCompleted;
            onCompleted.Invoke();
        }
    }

    private static Storyboard CreateFadeOpacityStoryboard(
        UIElement element,
        double from,
        double to,
        TimeSpan duration,
        TimeSpan delay = default)
    {
        Storyboard storyboard = new();
        DoubleAnimation opacityAnimation = new()
        {
            From = from,
            To = to,
            Duration = duration,
            BeginTime = delay,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(opacityAnimation, element);
        Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
        storyboard.Children.Add(opacityAnimation);

        return storyboard;
    }
}
