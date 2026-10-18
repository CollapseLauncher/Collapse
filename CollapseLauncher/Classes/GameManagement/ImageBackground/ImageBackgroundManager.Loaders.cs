using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Pages;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
// ReSharper disable IdentifierTypo
// ReSharper disable CheckNamespace
// ReSharper disable CommentTypo

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Fields

    private CancellationTokenSource? _imageLoadingTokenSource;

    private static readonly MinMaxToPercentConverter<double> MinMaxToPercentConverter = new(0d, 100d);

    private string? _lastBackgroundSource;

    #endregion

    private void LoadImage(CancellationToken token = default)
    {
        if (ImageContextSources.Count == 0 ||
            CurrentSelectedBackgroundIndex < 0)
        {
            return;
        }

        new Thread(Impl) { IsBackground = true }.Start();
        return;

        async void Impl()
        {
            Stopwatch? stopwatch = null;
            try
            {
                // -- Get context and invalidate previous CTS
                LayeredImageBackgroundContext? context = CurrentSelectedBackgroundContext;
                if (context == null)
                    return;

                // -- Invalidate last Cancellation Token (and thus, cancel all asynchronous routines)
                //    and then renew the token. Also toggle the background loading indicator.
                DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(() => IsBackgroundLoading = true );
                token = InvalidateAndRenewCancelToken(token);

                stopwatch = Stopwatch.StartNew();

                // -- Notify changes on context menu properties
                DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(CurrentSelectedBackgroundHasOverlayImage)));

                // -- Start clean-up if force reload requested
                if (context.ForceReload)
                {
                    context.DeleteCachedImagePath(context.OriginOverlayImagePath);
                    context.DeleteCachedImagePath(context.OriginBackgroundImagePath);
                    context.DeleteCachedImagePath(context.OriginPlaceholderImagePath);
                }

                // -- Get Cached path.
                string? downloadedOverlayUri = null;
                string? downloadedBackgroundUri = null;
                string? downloadedPlaceholderUri = null;

                Task overlayLoadTask     = context.GetCachedImagePathAsync(context.OriginOverlayImagePath,     token).GetResultFromAction(result => downloadedOverlayUri = result);
                Task backgroundLoadTask  = context.GetCachedImagePathAsync(context.OriginBackgroundImagePath,  token).GetResultFromAction(result => downloadedBackgroundUri = result);
                Task placeholderLoadTask = context.GetCachedImagePathAsync(context.OriginPlaceholderImagePath, token).GetResultFromAction(result => downloadedPlaceholderUri = result);

                await Task.WhenAll(overlayLoadTask,
                                   backgroundLoadTask,
                                   placeholderLoadTask);

                token.ThrowIfCancellationRequested();

                if (!string.IsNullOrEmpty(CurrentCachedBgKey))
                {
                    LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-Background", context.OriginBackgroundImagePath ?? "");
                    LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-StaticBackground", context.OriginPlaceholderImagePath ?? "");
                    LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-Overlay", context.OriginOverlayImagePath ?? "");
                }

                // -- Use UI thread and load image layer
                DispatcherQueueExtensions
                   .CurrentDispatcherQueue
                   .TryEnqueue(DispatcherQueuePriority.High,
                               AssignSource);

                return;

                async void AssignSource()
                {
                    try
                    {
                        string? backgroundToUse = (CurrentIsEnableBackgroundAutoPlay ? downloadedBackgroundUri : downloadedPlaceholderUri) ?? downloadedBackgroundUri;
                        string? lastBackgroundToUse = _lastBackgroundSource;

                        // -- Avoid spawning element while window is currently not visible
                        //    (for example: Minimized at start).
                        if (!WindowUtility.CurrentWindowIsVisible)
                        {
                            return;
                        }

                        // -- Avoid the same background to get loaded.
                        if (Interlocked.Exchange(ref _lastBackgroundSource, backgroundToUse) != lastBackgroundToUse &&
                            !context.ForceReload)
                        {
                            return;
                        }

                        // -- Reset force reload request.
                        bool forceReloadRequested = context.ForceReload;
                        context.ForceReload = false;

                        // -- Check media support and whether to force FFmpeg in some cases.
                        (bool isSupported, bool forceFFmpeg) = await CheckCodecOrSpawnDialog(backgroundToUse);
                        if (!isSupported)
                        {
                            return;
                        }

                        // -- Notify whether the background is a video or not.
                        CurrentBackgroundIsVideo = context.IsVideo;

                        // -- Create or Get element and set background.
                        LayeredBackgroundImage element = CreateOrGetLayerElement();
                        bool useFFmpeg = forceFFmpeg || (context.IsVideo && GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable);

                        // -- If Force Reload previously requested, rapidly nullify the background source.
                        if (forceReloadRequested)
                            element.BackgroundSource = null;

                        element.UseFfmpegDecoder = useFFmpeg;
                        element.BackgroundSource = backgroundToUse;
                        element.ForegroundSource = downloadedOverlayUri;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _ = SentryHelper.ExceptionHandlerAsync(ex);
                Logger.LogWriteLine($"[ImageBackgroundManager::LoadImage] {ex}",
                                    LogType.Error,
                                    true);
            }
            catch (OperationCanceledException)
            {
                // Suppress: cancellation is expected on game switch or rapid load cycles.
            }
            finally
            {
                stopwatch?.Stop();
                Logger.LogWriteLine($"Background image loading took: {stopwatch?.Elapsed.TotalSeconds} second(s)");

                DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(() => IsBackgroundLoading = false);
            }
        }
    }

    private CancellationToken InvalidateAndRenewCancelToken(CancellationToken coopToken = default)
    {
        CancellationTokenSource  newCts;
        CancellationTokenSource? lastCts =
            Interlocked.Exchange(ref _imageLoadingTokenSource,
                                 newCts = CancellationTokenSource.CreateLinkedTokenSource(coopToken));

        lastCts?.Cancel();
        lastCts?.Dispose();

        return newCts.Token;
    }

    private LayeredBackgroundImage CreateOrGetLayerElement()
    {
        if (CurrentBackgroundElement == null)
        {
            CurrentBackgroundElement = new LayeredBackgroundImage
            {
                ParallaxResetOnUnfocused  = false,
                BackgroundElevationPixels = 64d
            };

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.FfmpegDecoderModeProperty,
                                                  this,
                                                  nameof(GlobalFFmpegDecodingMode),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.ParallaxHoverSourceProperty,
                                                  this,
                                                  nameof(GlobalParallaxHoverSource),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.IsParallaxEnabledProperty,
                                                  this,
                                                  nameof(GlobalIsBackgroundParallaxEffectEnabled),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.ParallaxHorizontalShiftProperty,
                                                  this,
                                                  nameof(GlobalBackgroundParallaxPixelShift),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.ParallaxVerticalShiftProperty,
                                                  this,
                                                  nameof(GlobalBackgroundParallaxPixelShift),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.IsAudioEnabledProperty,
                                                  this,
                                                  nameof(GlobalBackgroundAudioEnabled),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.AudioVolumeProperty,
                                                  this,
                                                  nameof(GlobalBackgroundAudioVolume),
                                                  bindingMode: BindingMode.OneWay,
                                                  converter: MinMaxToPercentConverter);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.IsBackgroundElevatedProperty,
                                                  this,
                                                  nameof(IsBackgroundElevated),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.ForegroundOpacityProperty,
                                                  this,
                                                  nameof(ForegroundOpacity),
                                                  bindingMode: BindingMode.OneWay);

            CurrentBackgroundElement.BindProperty(LayeredBackgroundImage.SmokeOpacityProperty,
                                                  this,
                                                  nameof(SmokeOpacity),
                                                  bindingMode: BindingMode.OneWay);

            PresenterGrid?.Children.Clear();
            PresenterGrid?.Children.Add(CurrentBackgroundElement);
        }

        return CurrentBackgroundElement;
    }

    private async Task GetMediaAccentColor(object? context)
    {
        try
        {
            if (context is not (Uri asUri, bool useFfmpegForVideo, string configKey))
            {
                return;
            }

            Color color = await ColorPaletteUtility.GetMediaAccentColorFromAsync(asUri, useFfmpegForVideo)
                                                   .ConfigureAwait(false);

            if (color == default) return;

            string hex = $"{color.R:X2}{color.G:X2}{color.B:X2}";
            if (!string.IsNullOrEmpty(configKey))
            {
                string? savedHex = LauncherConfig.GetAppConfigValue($"{configKey}-AccentColor").ToString();
                if (savedHex == hex) return;
                LauncherConfig.SetAndSaveConfigValue($"{configKey}-AccentColor", hex);
            }

            ColorAccentChanged?.Invoke(color);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"GetMediaAccentColor failed: {ex}", LogType.Error, true);
            SentryHelper.ExceptionHandler(ex);
        }
    }
}