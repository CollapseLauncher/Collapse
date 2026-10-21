using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using System;
using System.Numerics;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage
{
    private void ElevateGrid_ToggleEnable(bool isEnable)
    {
        ElevateGrid_CalculateCenterPoint();
        ElevateGrid_StartElevationAnimation(isEnable);
    }

    private void ElevateGrid_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool isCurrentlyEnabled = IsBackgroundElevated;
        if (!isCurrentlyEnabled)
        {
            return;
        }

        ElevateGrid_CalculateCenterPoint();
        ElevateGrid_StartElevationAnimation(true);
    }

    private void ElevateGrid_CalculateCenterPoint()
    {
        _elevateGridVisual.CenterPoint = new Vector3((float)_elevateGrid.RenderSize.Width / 2,
                                                     (float)_elevateGrid.RenderSize.Height / 2,
                                                     0);
    }

    private void ElevateGrid_StartElevationAnimation(bool enable)
    {
        double elevationOffset = enable
            ? BackgroundElevationPixels
            : 0d;

        StartElementElevateOffsetAnimation(_elevateGridCompositor,
                                           _elevateGridVisual,
                                           _elevateGrid,
                                           elevationOffset,
                                           elevationOffset,
                                           0d,
                                           0d,
                                           500d,
                                           _elevateEasingFunction);
    }

    private static void StartElementElevateOffsetAnimation(
        Compositor                 compositor,
        Visual                     visual,
        UIElement                  element,
        double                     offsetX,
        double                     offsetY,
        double                     centerPointX,
        double                     centerPointY,
        double                     durationMs,
        CompositionEasingFunction? easingFunction    = null,
        bool                       useAnimatedOffset = true)
    {
        // Move opposite to center point
        float tx = (float)(-centerPointX * offsetX);
        float ty = (float)(-centerPointY * offsetY);

        // Scale with x2 as it counts with each side of axis (Left, Right) (Top, Bottom)
        Vector2 size    = element.ActualSize;
        double  sizeToX = size.X + Math.Abs(offsetX) * 2;
        double  sizeToY = size.Y + Math.Abs(offsetY) * 2;

        double addScaleX = sizeToX / size.X;
        double addScaleY = sizeToY / size.Y;

        // Gets the stronger axis
        float factorScale = (float)Math.Max(addScaleX, addScaleY);

        StartElementElevateAnimation(compositor,
                                     visual,
                                     Vector3.Zero with { X = tx, Y = ty },
                                     Vector3.One with { X = factorScale, Y = factorScale },
                                     durationMs,
                                     easingFunction,
                                     useAnimatedOffset);
    }

    private static void StartElementElevateAnimation(
        Compositor                 compositor,
        Visual                     visual,
        Vector3                    offset,
        Vector3                    scale,
        double                     duration,
        CompositionEasingFunction? easingFunction    = null,
        bool                       useAnimatedOffset = true)
    {
        const string targetTranslation = "Offset";
        const string targetScale       = "Scale";

        if (compositor == null!)
        {
            return;
        }

        // Scale
        Vector3KeyFrameAnimation scaleAnim = CreateVector3Keyframe(scale, targetScale);
        visual.StartAnimation(targetScale, scaleAnim);

        if (useAnimatedOffset)
        {
            // Move
            Vector3KeyFrameAnimation offsetAnim = CreateVector3Keyframe(offset, targetTranslation);
            visual.StartAnimation(targetTranslation, offsetAnim);
            return;
        }

        // Directly set visual offset
        visual.Offset = offset;
        return;

        Vector3KeyFrameAnimation CreateVector3Keyframe(
            Vector3 valueTo,
            string  targetProperty)
        {
            Vector3KeyFrameAnimation keyframe = compositor.CreateVector3KeyFrameAnimation();
            keyframe.InsertKeyFrame(1, valueTo, easingFunction);
            keyframe.Target   = targetProperty;
            keyframe.Duration = TimeSpan.FromMilliseconds(duration);

            return keyframe;
        }
    }
}
