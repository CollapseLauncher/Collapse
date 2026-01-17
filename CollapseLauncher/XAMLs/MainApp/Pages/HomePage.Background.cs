using CollapseLauncher.Dialogs;
using CollapseLauncher.GameManagement.ImageBackground;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Statics;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Animations;
using Hi3Helper;
using Hi3Helper.EncTool;
using Hi3Helper.Win32.FileDialogCOM;
using Hi3Helper.Win32.ManagedTools;
using Hi3Helper.Win32.Native.LibraryImport;
using Hi3Helper.Win32.Native.Structs;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Foundation;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.Pages;

public sealed partial class HomePage
{
    #region Background element events

    private CancellationTokenSource? _multiBackgroundGridHoverCts;

    private async void MultiBackgroundPipsPagerGridHoverArea_OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        try
        {
            CancellationTokenSource? lastCts = Interlocked.Exchange(ref _multiBackgroundGridHoverCts, new CancellationTokenSource());
            lastCts?.Dispose();

            if (MultiBackgroundPipsPagerGrid.Tag is true ||
                _multiBackgroundGridHoverCts.IsCancellationRequested)
            {
                return;
            }

            await Task.Delay(250, _multiBackgroundGridHoverCts.Token);

            if (MultiBackgroundPipsPagerGrid.Tag is true ||
                _multiBackgroundGridHoverCts.IsCancellationRequested)
            {
                return;
            }

            ToggleMultiBackgroundPipsPagerGridHoverState(MultiBackgroundPipsPagerGrid, true);
        }
        catch
        {
            // ignored
        }
    }

    private void MultiBackgroundPipsPagerGridHoverArea_OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _multiBackgroundGridHoverCts?.Cancel();
        if (MultiBackgroundPipsPagerGrid.Tag is not true)
        {
            return;
        }

        if (MultiBackgroundPipsPagerGridViewMoreBtn.Flyout is { IsOpen: true } menuFlyout)
        {
            menuFlyout.Closed += MultiBackgroundPipsPagerGridHoverArea_OnFlyoutClosed;
            return;
        }

        if (MultiBackgroundPipsPagerGridAudioToggleBtn.Flyout is { IsOpen: true } audioToggleFlyout)
        {
            audioToggleFlyout.Closed += MultiBackgroundPipsPagerGridHoverArea_OnFlyoutClosed;
            return;
        }

        ToggleMultiBackgroundPipsPagerGridHoverState(MultiBackgroundPipsPagerGrid, false);
    }

    private void MultiBackgroundPipsPagerGridHoverArea_OnFlyoutClosed(object? sender, object e)
    {
        FlyoutBase flyout = (FlyoutBase)sender!;
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

    private bool IsInMultiBackgroundPipsPagerGridHoverArea(PointerRoutedEventArgs? args = null)
    {
        Vector2 gridSize = MultiBackgroundPipsPagerGrid.ActualSize;
        Point gridPos = MultiBackgroundPipsPagerGrid
                       .TransformToVisual(null)
                       .TransformPoint(default);
        POINTL pointerPos;

        if (args != null)
        {
            Point cursorPos = args.GetCurrentPoint(MultiBackgroundPipsPagerGrid).Position;
            pointerPos = new POINTL
            {
                x = (int)cursorPos._x,
                y = (int)cursorPos._y
            };
        }
        else
        {
            PInvoke.GetCursorPos(out pointerPos).ThrowOnFailure();
            PInvoke.ScreenToClient(WindowUtility.CurrentWindowPtr, ref pointerPos).ThrowOnFailure();
        }

        double xFrom = gridPos.X;
        double xTo   = xFrom + gridSize.X;
        double yFrom = gridPos.Y;
        double yTo   = yFrom + gridSize.Y;

        bool isInArea = pointerPos.x >= xFrom &&
                        pointerPos.x <= xTo &&
                        pointerPos.y >= yFrom &&
                        pointerPos.y <= yTo;

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
    }
    #endregion

    #region Background context menu commands

    public ICommand BackgroundImageContextMenuCopyUrlToClipboardCommand { get; } = new RelayCommand<object?>(OnBackgroundImageContextMenuCopyUrlToClipboardCommand);
    private static void OnBackgroundImageContextMenuCopyUrlToClipboardCommand(object? sourcePath)
    {
        Uri? sourceUri = sourcePath as Uri;
        if (sourcePath is string asString &&
            !Uri.TryCreate(asString, UriKind.Absolute, out sourceUri))
        {
            return;
        }

        if (sourceUri == null)
        {
            return;
        }

        Clipboard.CopyStringToClipboard(sourceUri.IsFile
                                        ? sourceUri.LocalPath
                                        : sourceUri.AbsoluteUri);
    }

    public ICommand BackgroundImageContextMenuCopyImageToClipboardCommand { get; } = new RelayCommand<int>(OnBackgroundImageContextMenuCopyImageToClipboardCommand);
    private static void OnBackgroundImageContextMenuCopyImageToClipboardCommand(int frameType)
    {
        ClipboardUtility.CopyCurrentFrameToClipboard(ImageBackgroundManager.Shared, (FrameToCopyType)frameType);
    }

    public ICommand BackgroundImageContextMenuSetCustomParallaxPixelsCommand { get; } = new RelayCommand(OnBackgroundImageContextMenuSetCustomParallaxPixelsCommand);
    private static async void OnBackgroundImageContextMenuSetCustomParallaxPixelsCommand()
    {
        try
        {
            await SimpleDialogs.Dialog_SelectCustomBackgroundParallaxPixels();
        }
        catch
        {
            // ignored
        }
    }

    public ICommand BackgroundImageContextMenuSaveCommand { get; } = new RelayCommand<object>(OnBackgroundImageContextMenuSaveCommand);
    private static async void OnBackgroundImageContextMenuSaveCommand(object? onlyCurrentObj)
    {
        try
        {
            PresetConfig? presetConfig = GamePropertyVault.GetCurrentGameProperty().GamePreset;
            if (presetConfig == null!)
            {
                return;
            }

            if (onlyCurrentObj is not string asString ||
                !bool.TryParse(asString, out bool onlyCurrent))
            {
                return;
            }

            string folderSave = await FileDialogNative
               .GetFolderPicker(onlyCurrent
                                    ? Locale.Lang._HomePage.BgContextMenu_FolderSelectSaveCurrentBg
                                    : Locale.Lang._HomePage.BgContextMenu_FolderSelectSaveAllBg);
            if (string.IsNullOrEmpty(folderSave))
            {
                return;
            }

            string dirPath = Path.Combine(folderSave, $"CollapseBackground-{presetConfig.ProfileName}");
            Directory.CreateDirectory(dirPath);

            List<LayeredImageBackgroundContext> contexts = onlyCurrent && ImageBackgroundManager.Shared.CurrentSelectedBackgroundContext is {} currentContext
                ? [currentContext]
                : ImageBackgroundManager.Shared.ImageContextSources.ToList();

            foreach ((int Index, LayeredImageBackgroundContext Item) context in contexts.Index())
            {
                await CopyFromPath(context.Item.OriginOverlayImagePath,    dirPath, false, context.Index);
                await CopyFromPath(context.Item.OriginBackgroundImagePath, dirPath, true,  context.Index);
            }

            using Process proc = new();
            proc.StartInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName        = MainPage.ExplorerPath,
                Arguments       = dirPath
            };
            proc.Start();

            return;

            static async Task CopyFromPath(string? path, string dirPath, bool isBackground, int index)
            {
                if (string.IsNullOrEmpty(path))
                {
                    return;
                }

                dirPath = Path.Combine(dirPath, $"{index + 1:00}");
                Directory.CreateDirectory(dirPath);

                string fileName = isBackground
                    ? "Background_"
                    : "Overlay_";
                fileName += Path.GetFileName(path);
                string filePath = Path.Combine(dirPath, fileName);

                Uri                    uri          = new(path);
                await using Stream     sourceStream = await GetStreamFromLocalOrRemote(uri);
                await using FileStream targetStream = File.Create(filePath);

                await sourceStream.CopyToAsync(targetStream);
            }

            static async Task<Stream> GetStreamFromLocalOrRemote(Uri path)
            {
                if (path.IsFile)
                {
                    return File.Open(path.LocalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }

                HttpClient client = FallbackCDNUtil.GetGlobalHttpClient(true);
                return await BridgedNetworkStream.CreateStream(client, path);
            }
        }
        catch
        {
            // ignored
        }
    }

    #endregion

    #region Button Clicks

    private void MultiBackgroundPipsPagerGridAudioToggleBtn_OnClick(object sender, RoutedEventArgs e)
    {
        CurrentBackgroundManager.GlobalBackgroundAudioEnabled = !CurrentBackgroundManager.GlobalBackgroundAudioEnabled;
    }
    
    #endregion
}
