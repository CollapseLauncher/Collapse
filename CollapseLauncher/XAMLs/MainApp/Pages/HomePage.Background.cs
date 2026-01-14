using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Pages;

public sealed partial class HomePage
{
    #region Background element events
    private void MultiBackgroundPipsPagerGridHoverArea_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (MultiBackgroundPipsPagerGrid.Tag is true ||
            IsInMultiBackgroundPipsPagerGridHoverArea())
        {
            return;
        }

        ToggleMultiBackgroundPipsPagerGridHoverState(MultiBackgroundPipsPagerGrid, true);
    }

    private void MultiBackgroundPipsPagerGridHoverArea_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (MultiBackgroundPipsPagerGridViewMoreBtn.Flyout is MenuFlyout { IsOpen: true } menuFlyout)
        {
            menuFlyout.Closed += MultiBackgroundPipsPagerGridHoverArea_OnFlyoutClosed;
            return;
        }
        ToggleMultiBackgroundPipsPagerGridHoverState(MultiBackgroundPipsPagerGrid, false);
    }

    private void MultiBackgroundPipsPagerGridHoverArea_OnFlyoutClosed(object? sender, object e)
    {
        MenuFlyout flyout = (MenuFlyout)sender!;
        flyout.Closed -= MultiBackgroundPipsPagerGridHoverArea_OnFlyoutClosed;

        if (IsInMultiBackgroundPipsPagerGridHoverArea())
        {
            return;
        }

        ToggleMultiBackgroundPipsPagerGridHoverState(MultiBackgroundPipsPagerGrid, false);
    }

    // ReSharper disable once AsyncVoidMethod
    private async void MultiBackgroundPipsPagerGrid_OnLoaded(object sender, RoutedEventArgs e)
    {
        await Task.Delay(1000);
        if (IsInMultiBackgroundPipsPagerGridHoverArea())
        {
            return;
        }

        ToggleMultiBackgroundPipsPagerGridHoverState(MultiBackgroundPipsPagerGrid, false);
    }

    private bool IsInMultiBackgroundPipsPagerGridHoverArea()
    {
        PInvoke.GetCursorPos(out POINTL pointerPos).ThrowOnFailure();
        PInvoke.ScreenToClient(WindowUtility.CurrentWindowPtr, ref pointerPos).ThrowOnFailure();

        Vector2 gridSize = MultiBackgroundPipsPagerGrid.ActualSize;
        double scaleDpi = WindowUtility.CurrentWindowMonitorScaleFactor;
        Point gridPos = MultiBackgroundPipsPagerGrid
                       .TransformToVisual(null)
                       .TransformPoint(default);

        gridPos = new Point(gridPos.X * scaleDpi,
                            gridPos.Y * scaleDpi);

        bool isInArea = pointerPos.x > gridPos.X &&
                        pointerPos.x < gridPos.X + gridSize.X &&
                        pointerPos.y > gridPos.Y &&
                        pointerPos.y < gridPos.Y + gridSize.Y;

        return isInArea;
    }

    private static void ToggleMultiBackgroundPipsPagerGridHoverState(Panel grid, bool show)
    {
        grid.Tag = show;
        Visual visual = ElementCompositionPreview.GetElementVisual(grid);
        Compositor compositor = visual.Compositor;

        double duration = 200d;

        CompositionAnimationGroup animGroup = compositor.CreateAnimationGroup();
        CompositionEasingFunction? function = compositor.TryCreateEasingFunction(EasingType.Quintic);

        // Scale
        float scaleFrom = !show ? 1f : 0.90f;
        float scaleTo = show ? 1f : 0.90f;
        Vector3KeyFrameAnimation scaleAnim = compositor.CreateVector3KeyFrameAnimation();
        scaleAnim.Duration = TimeSpan.FromMilliseconds(duration);
        scaleAnim.InsertKeyFrame(0f, new Vector3(scaleFrom), function);
        scaleAnim.InsertKeyFrame(1f, new Vector3(scaleTo), function);
        scaleAnim.Target = "Scale";

        // Move
        float xOffsetFrom = (float)grid.ActualWidth * (1f - scaleFrom) / 2f;
        float xOffsetTo = (float)grid.ActualWidth * (1f - scaleTo) / 2f;
        Vector3KeyFrameAnimation translateAnim = compositor.CreateVector3KeyFrameAnimation();
        translateAnim.Duration = scaleAnim.Duration;
        translateAnim.InsertKeyFrame(0f, new Vector3(xOffsetFrom, !show ? -grid.Translation.Y : -((float)grid.ActualHeight / 2), 0), function);
        translateAnim.InsertKeyFrame(1f, new Vector3(xOffsetTo, show ? -grid.Translation.Y : -((float)grid.ActualHeight / 2), 0), function);
        translateAnim.Target = "Translation";

        // Opacity
        ScalarKeyFrameAnimation opacityAnim = compositor.CreateScalarKeyFrameAnimation();
        opacityAnim.Duration = scaleAnim.Duration;
        opacityAnim.InsertKeyFrame(0f, !show ? 1f : 0f, function);
        opacityAnim.InsertKeyFrame(1f, show ? 1f : 0f, function);
        opacityAnim.Target = "Opacity";

        animGroup.Add(translateAnim);
        animGroup.Add(scaleAnim);
        animGroup.Add(opacityAnim);

        visual.StartAnimationGroup(animGroup);

        grid.Tag = show;
    }

    private void TeachingTipHoverableGrid_OnClosed(TeachingTip sender, TeachingTipClosedEventArgs args)
    {
    }
    #endregion

    #region Background context menu commands

    public ICommand BackgroundImageContextMenuCopyImageToClipboardCommand { get; } = new RelayCommand<int>(OnBackgroundImageContextMenuCopyImageToClipboardCommand);
    private static void OnBackgroundImageContextMenuCopyImageToClipboardCommand(int frameType)
    {
        ClipboardUtility.CopyCurrentFrameToClipboard(ImageBackgroundManager.Shared, (FrameToCopyType)frameType);
    }

    #endregion
}
