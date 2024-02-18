using Hi3Helper;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using System;
using System.Threading.Tasks;

namespace CollapseLauncher.Helper.Animation
{
    internal static class AnimationHelper
    {
        internal static async Task StartAnimation(this UIElement element, TimeSpan duration, params KeyFrameAnimation[] animBase)
        {
            foreach (KeyFrameAnimation anim in animBase)
            {
                if (element.DispatcherQueue.HasThreadAccess)
                {
                    anim.Duration = duration;
                    element.StartAnimation(anim);
                }
                else
                    element.DispatcherQueue.TryEnqueue(() =>
                    {
                        anim.Duration = duration;
                        element.StartAnimation(anim);
                    });
            }
            await Task.Delay(duration);
            foreach (var item in animBase)
            {
                if (element.DispatcherQueue.HasThreadAccess)
                    element.StopAnimation(item);
                else
                    element.DispatcherQueue.TryEnqueue(() =>
                        element.StopAnimation(item));
            }
        }

        internal static void EnableImplicitAnimation(this UIElement element, bool recursiveAssignment = false, CompositionEasingFunction easingFunction = null)
        {
            try
            {
                Visual rootFrameVisual = ElementCompositionPreview.GetElementVisual(element);
                Compositor compositor = rootFrameVisual.Compositor;

                ImplicitAnimationCollection animationCollection = rootFrameVisual.ImplicitAnimations != null ? rootFrameVisual.ImplicitAnimations : compositor.CreateImplicitAnimationCollection();

                Vector2KeyFrameAnimation sizeKeyframeAnimation = compositor.CreateVector2KeyFrameAnimation();
                Vector3KeyFrameAnimation offsetKeyframeAnimation = compositor.CreateVector3KeyFrameAnimation();
                Vector3KeyFrameAnimation scaleKeyframeAnimation = compositor.CreateVector3KeyFrameAnimation();

                sizeKeyframeAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easingFunction);
                sizeKeyframeAnimation.Duration = TimeSpan.FromMilliseconds(250);
                sizeKeyframeAnimation.Target = "Size";

                offsetKeyframeAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easingFunction);
                offsetKeyframeAnimation.Duration = TimeSpan.FromMilliseconds(250);
                offsetKeyframeAnimation.Target = "Offset";

                scaleKeyframeAnimation.InsertExpressionKeyFrame(1.0f, "this.FinalValue", easingFunction);
                scaleKeyframeAnimation.Duration = TimeSpan.FromMilliseconds(250);
                scaleKeyframeAnimation.Target = "Scale";

                animationCollection.TryAddImplicitCollectionAnimation("Size", sizeKeyframeAnimation);
                animationCollection.TryAddImplicitCollectionAnimation("Offset", offsetKeyframeAnimation);
                animationCollection.TryAddImplicitCollectionAnimation("Scale", scaleKeyframeAnimation);

                rootFrameVisual.ImplicitAnimations = animationCollection;
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[AnimationHelper::EnableImplicitAnimation()] Error has occurred while assigning Implicit Animation to the element!\r\n{ex}", LogType.Error, true);
            }

            if (element is Grid grid && recursiveAssignment)
            {
                foreach (UIElement childrenElement in grid.Children)
                {
                    childrenElement.EnableImplicitAnimation();
                }
            }
            if (element is Panel panel && recursiveAssignment)
            {
                foreach (UIElement childrenElement in panel.Children)
                {
                    childrenElement.EnableImplicitAnimation();
                }
            }
        }

        internal static Compositor GetElementCompositor(this UIElement element)
        {
            Visual rootFrameVisual = ElementCompositionPreview.GetElementVisual(element);
            Compositor compositor = rootFrameVisual.Compositor;
            return compositor;
        }

        private static void TryAddImplicitCollectionAnimation(this ImplicitAnimationCollection collection, string key, ICompositionAnimationBase animation)
        {
            if (!collection.ContainsKey(key))
            {
                collection[key] = animation;
            }
        }
    }
}
