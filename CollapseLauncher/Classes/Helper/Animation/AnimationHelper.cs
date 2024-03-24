using CommunityToolkit.WinUI.Controls;
using Hi3Helper;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper.Animation
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    internal static class AnimationHelper
    {
        internal static async Task StartAnimation(this UIElement element, TimeSpan duration, params KeyFrameAnimation[] animBase)
        {
            foreach (KeyFrameAnimation anim in animBase!)
            {
                if (element!.DispatcherQueue!.HasThreadAccess)
                {
                    anim!.Duration = duration;
                    element.StartAnimation(anim);
                }
                else
                    element.DispatcherQueue.TryEnqueue(() =>
                    {
                        anim!.Duration = duration;
                        element.StartAnimation(anim);
                    });
            }
            await Task.Delay(duration);
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

        internal static void EnableImplicitAnimation(this UIElement element,
                                                     bool recursiveAssignment = false,
                                                     CompositionEasingFunction easingFunction = null)
        {
            try
            {
                Visual rootFrameVisual = ElementCompositionPreview.GetElementVisual(element);
                Compositor compositor = rootFrameVisual!.Compositor;

                ImplicitAnimationCollection animationCollection =
                    rootFrameVisual.ImplicitAnimations != null ?
                        rootFrameVisual.ImplicitAnimations : compositor!.CreateImplicitAnimationCollection();

                Vector2KeyFrameAnimation sizeKeyframeAnimation = compositor!.CreateVector2KeyFrameAnimation();
                Vector3KeyFrameAnimation offsetKeyframeAnimation = compositor!.CreateVector3KeyFrameAnimation();
                Vector3KeyFrameAnimation scaleKeyframeAnimation = compositor!.CreateVector3KeyFrameAnimation();

                sizeKeyframeAnimation!.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easingFunction);
                sizeKeyframeAnimation!.Duration = TimeSpan.FromMilliseconds(250);
                sizeKeyframeAnimation!.Target = "Size";

                offsetKeyframeAnimation!.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easingFunction);
                offsetKeyframeAnimation!.Duration = TimeSpan.FromMilliseconds(250);
                offsetKeyframeAnimation!.Target = "Offset";

                scaleKeyframeAnimation!.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easingFunction);
                scaleKeyframeAnimation!.Duration = TimeSpan.FromMilliseconds(250);
                scaleKeyframeAnimation!.Target = "Scale";

                animationCollection.TryAddImplicitCollectionAnimation("Size", sizeKeyframeAnimation);
                animationCollection.TryAddImplicitCollectionAnimation("Offset", offsetKeyframeAnimation);
                animationCollection.TryAddImplicitCollectionAnimation("Scale", scaleKeyframeAnimation);

                rootFrameVisual.ImplicitAnimations = animationCollection;

                EnableElementVisibilityAnimation(compositor!, rootFrameVisual!, element!);
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[AnimationHelper::EnableImplicitAnimation()] Error has occurred while assigning Implicit Animation to the element!\r\n{ex}", LogType.Error, true);
            }

            if (!recursiveAssignment) return;

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

        private static void EnableElementVisibilityAnimation(Compositor compositor, Visual elementVisual, UIElement element)
        {
            TimeSpan animDur = TimeSpan.FromSeconds(0.25d);

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

        private static void TryAddImplicitCollectionAnimation(this ImplicitAnimationCollection collection,
                                                              string key, ICompositionAnimationBase animation)
        {
            if (!collection!.ContainsKey(key!))
            {
                collection[key] = animation;
            }
        }
    }
}
