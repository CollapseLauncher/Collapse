using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Pages;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Data;
using PhotoSauce.MagicScaler;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;
// ReSharper disable IdentifierTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Fields

    private CancellationTokenSource? _imageLoadingTokenSource;

    private static readonly MinMaxToPercentConverter<double> MinMaxToPercentConverter = new(0d, 100d);

    #endregion

    private void LoadImageAtIndex(int index, bool forceReload = false, CancellationToken token = default)
    {
        if (ImageContextSources.Count <= index ||
            index < 0)
        {
            return;
        }

        if (IsBackgroundLoading)
        {
            CancelCurrentBackgroundLoad();
        }

        // Only show loading indicator when a download is required.
        if (!CheckIsContextCached(ImageContextSources[index]))
        {
            IsBackgroundLoading = true;
        }

        new Thread(() =>
        {
            LoadImageAtIndexCore(index, forceReload, token).ConfigureAwait(false);
        })
        {
            IsBackground = true
        }.Start();
    }

    private void CancelCurrentBackgroundLoad()
    {
        if (Interlocked.Exchange(ref _imageLoadingTokenSource, null) is { } lastCts)
        {
            lastCts.Cancel();
            lastCts.Dispose();
        }
        IsBackgroundLoading = false;
    }

    private async Task LoadImageAtIndexCore(int index, bool forceReload = false, CancellationToken token = default)
    {
        Stopwatch? stopwatch = null;
        try
        {
            stopwatch = Stopwatch.StartNew();

            if (Interlocked.Exchange(ref _imageLoadingTokenSource,
                                     CancellationTokenSource.CreateLinkedTokenSource(token)) is { } lastCts)
            {
                await lastCts.CancelAsync();
                lastCts.Dispose();
            }

            token = _imageLoadingTokenSource.Token;
            bool isUseFFmpeg = GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable;

            // -- Notify changes on context menu properties
            DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(() => OnPropertyChanged(nameof(CurrentSelectedBackgroundHasOverlayImage)));

            // -- Get context and invalidate previous CTS
            LayeredImageBackgroundContext context = ImageContextSources[index];

            // -- Download overlay.
            Unsafe.SkipInit(out Uri? downloadedOverlayUri);
            if (Uri.TryCreate(context.OverlayImagePath, UriKind.Absolute, out Uri? overlayImageUri))
            {
                downloadedOverlayUri      = await GetLocalOrDownloadedFilePath(overlayImageUri, token).ConfigureAwait(false);
                (downloadedOverlayUri, _) = await GetNativeOrDecodedImagePath(downloadedOverlayUri, token).ConfigureAwait(false);
            }

            // -- Download background.
            Unsafe.SkipInit(out Uri? downloadedBackgroundUri);
            if (Uri.TryCreate(context.BackgroundImagePath, UriKind.Absolute, out Uri? backgroundImageUri))
            {
                downloadedBackgroundUri      = await GetLocalOrDownloadedFilePath(backgroundImageUri, token).ConfigureAwait(false);
                (downloadedBackgroundUri, _) = await GetNativeOrDecodedImagePath(downloadedBackgroundUri, token).ConfigureAwait(false);
            }

            // -- Download static background.
            Unsafe.SkipInit(out Uri? downloadedBackgroundStaticUri);
            if (Uri.TryCreate(context.BackgroundImageStaticPath, UriKind.Absolute, out Uri? backgroundStaticImageUri))
            {
                downloadedBackgroundStaticUri      = await GetLocalOrDownloadedFilePath(backgroundStaticImageUri, token).ConfigureAwait(false);
                (downloadedBackgroundStaticUri, _) = await GetNativeOrDecodedImagePath(downloadedBackgroundStaticUri, token).ConfigureAwait(false);
            }

            // Try to use static bg URL if normal bg is not available.
            downloadedBackgroundUri ??= downloadedBackgroundStaticUri;
            if (downloadedBackgroundUri == null)
            {
                if (!context.IsCustom)
                {
                    OnBackgroundLoadFailed?.Invoke();
                }
                return;
            }

            token.ThrowIfCancellationRequested();

            // -- Check for codec support (Also spawn dialog to install either native WIC/MediaFoundation decoder or using Ffmpeg decoder)
            (bool isSupported, bool isVideo, bool forceFFmpeg) = await CheckCodecOrSpawnDialog(downloadedBackgroundUri);
            if (!isSupported) return;

            // -- Force the use of FFmpeg if necessary.
            if (forceFFmpeg) isUseFFmpeg = true;

            // -- Get upscaled image file if Waifu2X is enabled
            if (GlobalIsWaifu2XEnabled && !isVideo)
            {
                downloadedOverlayUri          = await TryGetScaledWaifu2XImagePath(downloadedOverlayUri, token).ConfigureAwait(false);
                downloadedBackgroundUri       = await TryGetScaledWaifu2XImagePath(downloadedBackgroundUri, token).ConfigureAwait(false);
                downloadedBackgroundStaticUri = await TryGetScaledWaifu2XImagePath(downloadedBackgroundStaticUri, token).ConfigureAwait(false);
            }

            // Try to force loading static image if autoplay is not enabled and context has
            // static background in it.
            if (!CurrentIsEnableBackgroundAutoPlay && downloadedBackgroundStaticUri != null)
            {
                downloadedBackgroundUri = downloadedBackgroundStaticUri;
            }

            // Read accent color from the source that is actually displayed.
            Uri? accentSourceUri = downloadedBackgroundStaticUri ?? downloadedBackgroundUri ?? downloadedBackgroundUri;
            await GetMediaAccentColor((accentSourceUri, isUseFFmpeg, CurrentCachedBgKey)).ConfigureAwait(false);

            if (!context.IsCustom &&
                !string.IsNullOrEmpty(CurrentCachedBgKey))
            {
                LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-Background",       context.OriginBackgroundImagePath ?? "");
                LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-StaticBackground", context.OriginBackgroundStaticImagePath ?? "");
                LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-Overlay",          context.OriginOverlayImagePath ?? "");
            }

            // -- Use UI thread and load image layer
            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(DispatcherQueuePriority.High,
                           () => SpawnImageLayer(downloadedOverlayUri,
                                                 downloadedBackgroundUri,
                                                 forceFFmpeg,
                                                 forceReload));

            GlobalIsFFmpegCurrentlyUsed = isVideo && isUseFFmpeg;
            CurrentBackgroundIsVideo    = isVideo;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = SentryHelper.ExceptionHandlerAsync(ex);
            Logger.LogWriteLine($"[ImageBackgroundManager::LoadImageAtIndex] {ex}",
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
            if (!token.IsCancellationRequested)
            {
                IsBackgroundLoading = false;
            }
        }
    }

    private void SpawnImageLayer(Uri? overlayFilePath,
                                 Uri? backgroundFilePath,
                                 bool forceFFmpeg,
                                 bool forceReload = false)
    {
        LayeredBackgroundImage element = CreateOrGetLayerElement();

        element.UseFfmpegDecoder = forceFFmpeg || (GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable);
        if (forceReload || !IsSameLocalFile(element.BackgroundSource, backgroundFilePath))
            element.BackgroundSource = backgroundFilePath;

        if (forceReload || !IsSameLocalFile(element.ForegroundSource, overlayFilePath))
            element.ForegroundSource = overlayFilePath;
    }

    private static bool IsSameLocalFile(object? currentSource, Uri? newFilePath)
    {
        if (newFilePath == null) return currentSource == null;
        string newPath = newFilePath.IsFile ? newFilePath.LocalPath : newFilePath.OriginalString;
        string? currentPath = currentSource switch
        {
            Uri uri => uri.IsFile ? uri.LocalPath : uri.OriginalString,
            string str => str,
            _ => null
        };
        return string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase);
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

    private static bool TryGetUpscaledFilePath(string     inputFilePath,
                                               out string upscaledFilePath)
    {
        upscaledFilePath = Path.Combine(LauncherConfig.AppGameImgCachedFolder, $"scaled_{Path.GetFileNameWithoutExtension(inputFilePath)}.png");
        if (LayeredBackgroundImage.SupportedVideoExtensionsLookup.Contains(Path.GetExtension(inputFilePath))) // Ignore if file is a video.
        {
            upscaledFilePath = inputFilePath;
            return true;
        }

        FileInfo outputFileInfo = new(upscaledFilePath);
        return outputFileInfo is { Exists: true, Length: > 0 };
    }

    private static async Task<Uri?> TryGetScaledWaifu2XImagePath(Uri? uri, CancellationToken token)
    {
        if (uri == null)
        {
            return null;
        }

        if (!uri.IsFile)
        {
            throw new InvalidOperationException("You must provide a local file path!");
        }

        // Ignore if the input file is a video
        string inputFilePath = uri.LocalPath;
        if (TryGetUpscaledFilePath(inputFilePath, out string outputFilePath))
        {
            return new Uri(outputFilePath);
        }

        FileInfo outputFileInfo = new(outputFilePath);
        outputFileInfo.Directory?.Create();

        await Task.Run(Impl, token);
        outputFileInfo.Refresh();

        try
        {
            token.ThrowIfCancellationRequested();
            return new Uri(outputFileInfo.FullName);
        }
        catch
        {
            outputFileInfo.TryDeleteFile();
            throw;
        }

        void Impl()
        {
            using ProcessingPipeline pipeline =
                MagicImageProcessor
                   .BuildPipeline(inputFilePath,
                                  ProcessImageSettings.Default);

            using FileStream outputFileStream = outputFileInfo.Open(FileMode.Create,
                                                                    FileAccess.Write,
                                                                    FileShare.ReadWrite);

            pipeline.AddTransform(new Waifu2XTransform(ImageLoaderHelper.Waifu2X));
            pipeline.WriteOutput(outputFileStream);
        }
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