using Hi3Helper;
using Hi3Helper.CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper.Animation
{
    [Flags]
    public enum VisualPropertyType
    {
        None = 0,
        Opacity = 1 << 0,
        Offset = 1 << 1,
        Scale = 1 << 2,
        Size = 1 << 3,
        RotationAngleInDegrees = 1 << 4,
        All = ~0
    }

    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal static class AnimationHelper
    {
        internal static async void StartAnimationDetached(this UIElement element, TimeSpan duration, params KeyFrameAnimation[] animBase)
        {
            foreach (KeyFrameAnimation anim in animBase!)
            {
                if (element?.DispatcherQueue?.HasThreadAccess ?? false)
                {
                    anim!.Duration = duration;
                    element.StartAnimation(anim);
                }
                else
                    element?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        anim!.Duration = duration;
                        element.StartAnimation(anim);
                    });
            }
            await Task.Delay(duration);
        }

        internal static async Task StartAnimation(this UIElement element, TimeSpan duration, params KeyFrameAnimation[] animBase)
        {
            CompositionAnimationGroup animGroup = null;
            if (element.DispatcherQueue?.HasThreadAccess ?? false)
            {
                animGroup = CompositionTarget.GetCompositorForCurrentThread().CreateAnimationGroup();
            }
            else
            {
                element.DispatcherQueue.TryEnqueue(() =>
                {
                    animGroup = CompositionTarget.GetCompositorForCurrentThread().CreateAnimationGroup();
                });
            }

            using (animGroup)
            {
                foreach (KeyFrameAnimation anim in animBase!)
                {
                    if (element?.DispatcherQueue?.HasThreadAccess ?? false)
                    {
                        anim.Duration = duration;
                        anim.StopBehavior = AnimationStopBehavior.LeaveCurrentValue;
                        animGroup.Add(anim);
                    }
                    else
                        element?.DispatcherQueue?.TryEnqueue(() =>
                        {
                            anim.Duration = duration;
                            anim.StopBehavior = AnimationStopBehavior.LeaveCurrentValue;
                            animGroup.Add(anim);
                        });
                }
                if (element?.DispatcherQueue?.HasThreadAccess ?? false)
                {
                    element.StartAnimation(animGroup);
                }
                else
                    element?.DispatcherQueue?.TryEnqueue(() =>
                    {
                        element.StartAnimation(animGroup);
                    });
                await Task.Delay(duration);
            }
        }

        internal static void EnableImplicitAnimation(bool recursiveAssignment = false,
                                                     CompositionEasingFunction easingFunction = null,
                                                     params UIElement[] elements)
        {
            foreach (UIElement element in elements) element.EnableImplicitAnimation(recursiveAssignment, easingFunction);
        }

        internal static void EnableImplicitAnimation(this Page page,
                                                     bool recursiveAssignment = false,
                                                     CompositionEasingFunction easingFunction = null)
        {
            EnableImplicitAnimation(page.Content, recursiveAssignment, easingFunction);
        }

        internal static void EnableSingleImplicitAnimation(this UIElement element,
                                                     VisualPropertyType type,
                                                     CompositionEasingFunction easingFunction = null)
        {
            Visual rootFrameVisual = ElementCompositionPreview.GetElementVisual(element);
            Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();

            ImplicitAnimationCollection animationCollection =
                rootFrameVisual.ImplicitAnimations != null ?
                    rootFrameVisual.ImplicitAnimations : compositor!.CreateImplicitAnimationCollection();

            KeyFrameAnimation animation = CreateAnimationByType(compositor, type, 250, 0, easingFunction);
            animationCollection[type.ToString()] = animation;

            rootFrameVisual.ImplicitAnimations = animationCollection;
        }

        internal static void EnableImplicitAnimation(this UIElement element,
                                                     bool recursiveAssignment = false,
                                                     CompositionEasingFunction easingFunction = null)
        {
            try
            {
                Visual rootFrameVisual = ElementCompositionPreview.GetElementVisual(element);
                Compositor compositor = CompositionTarget.GetCompositorForCurrentThread();

                ImplicitAnimationCollection animationCollection =
                    rootFrameVisual.ImplicitAnimations != null ?
                        rootFrameVisual.ImplicitAnimations : compositor!.CreateImplicitAnimationCollection();

                foreach (VisualPropertyType type in Enum.GetValues<VisualPropertyType>())
                {
                    KeyFrameAnimation animation = CreateAnimationByType(compositor, type, 250, 0, easingFunction);

                    if (animation != null)
                    {
                        animationCollection[type.ToString()] = animation;
                    }
                }

                rootFrameVisual.ImplicitAnimations = animationCollection;
                element.EnableElementVisibilityAnimation();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[AnimationHelper::EnableImplicitAnimation()] Error has occurred while assigning Implicit Animation to the element!\r\n{ex}", LogType.Error, true);
            }

            if (!recursiveAssignment) return;

            if (element is Button button && button.Content is UIElement buttonContent)
                buttonContent.EnableImplicitAnimation(recursiveAssignment, easingFunction);

            if (element is Page page && page.Content is UIElement pageContent)
                pageContent.EnableImplicitAnimation(recursiveAssignment, easingFunction);

            if (element is NavigationView navigationView && navigationView.Content is UIElement navigationViewContent)
                navigationViewContent.EnableImplicitAnimation(recursiveAssignment, easingFunction);

            if (element is Panel panel)
                foreach (UIElement childrenElement in panel.Children!)
                    childrenElement.EnableImplicitAnimation(recursiveAssignment, easingFunction);

            if (element is ScrollViewer scrollViewer && scrollViewer.Content is UIElement elementInner)
                elementInner.EnableImplicitAnimation(recursiveAssignment, easingFunction);

            if (element is ContentControl contentControl && (element is SettingsCard || element is Expander) && contentControl.Content is UIElement contentControlInner)
            {
                contentControlInner.EnableImplicitAnimation(true, easingFunction);

                if (contentControl is Expander expander && expander.Header is UIElement expanderHeader)
                    expanderHeader.EnableImplicitAnimation(true, easingFunction);
            }

            if (element is InfoBar infoBar && infoBar.Content is UIElement infoBarInner)
                infoBarInner.EnableImplicitAnimation(true, easingFunction);
        }

        private static KeyFrameAnimation CreateAnimationByType(Compositor compositor, VisualPropertyType type,
            double duration = 800, double delay = 0, CompositionEasingFunction easing = null)
        {
            KeyFrameAnimation animation;

            switch (type)
            {
                case VisualPropertyType.Offset:
                case VisualPropertyType.Scale:
                    animation = compositor.CreateVector3KeyFrameAnimation();
                    break;
                case VisualPropertyType.Size:
                    animation = compositor.CreateVector2KeyFrameAnimation();
                    break;
                case VisualPropertyType.Opacity:
                case VisualPropertyType.RotationAngleInDegrees:
                    animation = compositor.CreateScalarKeyFrameAnimation();
                    break;
                default:
                    return null;
            }

            animation.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easing);
            animation.Duration = TimeSpan.FromMilliseconds(duration);
            animation.DelayTime = TimeSpan.FromMilliseconds(delay);
            animation.Target = type.ToString();

            return animation;
        }

        public static void EnableElementVisibilityAnimation(this UIElement element, Compositor compositor = null)
        {
            TimeSpan animDur = TimeSpan.FromSeconds(0.25d);
            compositor ??= CompositionTarget.GetCompositorForCurrentThread();

            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            ScalarKeyFrameAnimation HideOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            ScalarKeyFrameAnimation ShowOpacityAnimation = compositor.CreateScalarKeyFrameAnimation();

            HideOpacityAnimation.InsertKeyFrame(1.0f, 0.0f);
            HideOpacityAnimation.Duration = animDur;
            HideOpacityAnimation.Target = "Opacity";

            ShowOpacityAnimation.InsertKeyFrame(1.0f, 1.0f);
            ShowOpacityAnimation.Duration = animDur;
            ShowOpacityAnimation.Target = "Opacity";

            CompositionAnimationGroup HideAnimationGroup = compositor.CreateAnimationGroup();
            CompositionAnimationGroup ShowAnimationGroup = compositor.CreateAnimationGroup();

            HideAnimationGroup.Add(HideOpacityAnimation);
            ShowAnimationGroup.Add(ShowOpacityAnimation);

            ElementCompositionPreview.SetImplicitHideAnimation(element, HideAnimationGroup);
            ElementCompositionPreview.SetImplicitShowAnimation(element, ShowAnimationGroup);
        }

        internal static Compositor GetElementCompositor(this UIElement element)
        {
            Visual rootFrameVisual = ElementCompositionPreview.GetElementVisual(element);
            Compositor compositor = rootFrameVisual!.Compositor;
            return compositor;
        }
    }
}
