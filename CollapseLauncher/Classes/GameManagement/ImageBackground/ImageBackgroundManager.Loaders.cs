using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Pages;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using PhotoSauce.MagicScaler;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
// ReSharper disable IdentifierTypo

// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Fields

    private CancellationTokenSource? _imageLoadingTokenSource;

    #endregion

    private void LoadImageAtIndex(int index, CancellationToken token)
    {
        if (ImageContextSources.Count <= index ||
            index < 0 ||
            IsBackgroundLoading)
        {
            return;
        }

        IsBackgroundLoading = true;
        new Thread(() => LoadImageAtIndexCore(index, token).ConfigureAwait(false))
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest
        }.UnsafeStart();
    }

    private async Task LoadImageAtIndexCore(int index, CancellationToken token)
    {
        Stopwatch? stopwatch = null;
        try
        {
            bool isUseFFmpeg = GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable;
            stopwatch = Stopwatch.StartNew();

            // -- Notify changes on context menu properties
            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(() => OnPropertyChanged(nameof(CurrentSelectedBackgroundHasOverlayImage)));

            // -- Get context and invalidate previous CTS
            LayeredImageBackgroundContext context = ImageContextSources[index];
            if (Interlocked.Exchange(ref _imageLoadingTokenSource,
                                     CancellationTokenSource.CreateLinkedTokenSource(token)) is { } lastCts)
            {
                await lastCts.CancelAsync();
                lastCts.Dispose();
            }

            token = _imageLoadingTokenSource.Token;

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
            if (downloadedBackgroundUri == null) // If no background file is available, return.
            {
                return;
            }

            // -- Get upscaled image file if Waifu2X is enabled
            if (GlobalIsWaifu2XEnabled)
            {
                downloadedOverlayUri          = await TryGetScaledWaifu2XImagePath(downloadedOverlayUri, token).ConfigureAwait(false);
                downloadedBackgroundUri       = await TryGetScaledWaifu2XImagePath(downloadedBackgroundUri, token).ConfigureAwait(false);
                downloadedBackgroundStaticUri = await TryGetScaledWaifu2XImagePath(downloadedBackgroundStaticUri, token).ConfigureAwait(false);
            }

            token.ThrowIfCancellationRequested();

            // -- Check for codec support (Also spawn dialog to install either native WIC/MediaFoundation decoder or using Ffmpeg decoder)
            (bool isSupported, bool isVideo) = await CheckCodecOrSpawnDialog(downloadedBackgroundUri);
            if (!isSupported)
            {
                return;
            }

            // -- Read Color Accent information from current background context.
            new Thread(GetMediaAccentColor)
            {
                IsBackground = true
            }.Start((downloadedBackgroundUri, isUseFFmpeg));

            // -- Use UI thread and load image layer
            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(() => SpawnImageLayer(downloadedOverlayUri,
                                                 downloadedBackgroundUri,
                                                 downloadedBackgroundStaticUri,
                                                 isVideo,
                                                 context));

            GlobalIsFFmpegCurrentlyUsed = isVideo && isUseFFmpeg;
        }
        catch (Exception ex)
        {
            _ = SentryHelper.ExceptionHandlerAsync(ex);
            Logger.LogWriteLine($"[ImageBackgroundManager::LoadImageAtIndex] {ex}",
                                LogType.Error,
                                true);
        }
        finally
        {
            stopwatch?.Stop();
            Logger.LogWriteLine($"Background image loading took: {stopwatch?.Elapsed.TotalSeconds} second(s)");
            IsBackgroundLoading = false;
        }
    }

    private void SpawnImageLayer(Uri? overlayFilePath,
                                 Uri? backgroundFilePath,
                                 Uri? backgroundStaticFilePath,
                                 bool isVideo,
                                 LayeredImageBackgroundContext context)
    {
        LayeredBackgroundImage layerElement = new()
        {
            BackgroundSource          = backgroundFilePath,
            BackgroundStaticSource    = backgroundStaticFilePath,
            ForegroundSource          = overlayFilePath,
            UseFfmpegDecoder          = GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable,
            Tag                       = context,
            ParallaxResetOnUnfocused  = false,
            BackgroundElevationPixels = 64d
        };

        CurrentBackgroundElement = layerElement;

        if (!CurrentIsEnableCustomImage &&
            !GlobalIsEnableCustomImage)
        {
            layerElement.BindProperty(LayeredBackgroundImage.IsVideoAutoplayProperty,
                                      this,
                                      nameof(CurrentIsEnableBackgroundAutoPlay),
                                      bindingMode: BindingMode.OneWay,
                                      converter: StaticConverter<MediaAutoplayWindowOverrideConverter>.Shared);
        }
        else
        {
            layerElement.IsVideoAutoplay = WindowUtility.CurrentWindowIsVisible;
        }

        layerElement.BindProperty(LayeredBackgroundImage.ParallaxHoverSourceProperty,
                                  this,
                                  nameof(GlobalParallaxHoverSource),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.IsParallaxEnabledProperty,
                                  this,
                                  nameof(GlobalIsBackgroundParallaxEffectEnabled),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.ParallaxHorizontalShiftProperty,
                                  this,
                                  nameof(GlobalBackgroundParallaxPixelShift),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.ParallaxVerticalShiftProperty,
                                  this,
                                  nameof(GlobalBackgroundParallaxPixelShift),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.IsAudioEnabledProperty,
                                  this,
                                  nameof(GlobalBackgroundAudioEnabled),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.AudioVolumeProperty,
                                  this,
                                  nameof(GlobalBackgroundAudioVolume),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.IsBackgroundElevatedProperty,
                                  this,
                                  nameof(IsBackgroundElevated),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.ForegroundOpacityProperty,
                                  this,
                                  nameof(ForegroundOpacity),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.SmokeOpacityProperty,
                                  this,
                                  nameof(SmokeOpacity),
                                  bindingMode: BindingMode.OneWay);

        layerElement.BindProperty(LayeredBackgroundImage.UseImageCacheProperty,
                                  context,
                                  nameof(context.ForceReload),
                                  bindingMode: BindingMode.OneWay,
                                  converter: StaticConverter<InverseBooleanConverter>.Shared);

        layerElement.ImageLoaded       += LayerElementOnLoaded;
        layerElement.CanvasSizeChanged += LayerElementCanvasSizeChanged;
        PresenterGrid?.Children.Add(layerElement);

        layerElement.Tag = isVideo;
    }

    private void LayerElementCanvasSizeChanged(LayeredBackgroundImage layerElement, Size size)
    {
        CurrentElementWidth  = size.Width;
        CurrentElementHeight = size.Height;
    }

    private void LayerElementOnLoaded(LayeredBackgroundImage layerElement)
    {
        layerElement.Transitions.Add(new PopupThemeTransition());
        layerElement.ImageLoaded -= LayerElementOnLoaded;

        if (PresenterGrid?.Children.Count > 1)
        {
            UIElement? lastElement = PresenterGrid?.Children.LastOrDefault();
            foreach (UIElement element in PresenterGrid?.Children.Where(element => element != lastElement) ?? [])
            {
                PresenterGrid?.Children.Remove(element);
                if (element is LayeredBackgroundImage asLayeredImage)
                {
                    asLayeredImage.CanvasSizeChanged -= LayerElementCanvasSizeChanged;
                }
            }
        }

        if (CurrentIsEnableBackgroundAutoPlay && WindowUtility.CurrentWindowIsVisible)
        {
            Play(false);
        }

        CurrentBackgroundIsVideo = CurrentSelectedBackgroundContext?.IsVideo ?? false;
        if (layerElement.Tag is bool isDisplayControl)
        {
            CurrentBackgroundIsSeekable = isDisplayControl;
        }
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

            pipeline.AddTransform(new Waifu2XTransform(ImageLoaderHelper._waifu2X));
            pipeline.WriteOutput(outputFileStream);
        }
    }

    private async void GetMediaAccentColor(object? context)
    {
        try
        {
            if (context is not (Uri asUri, bool useFfmpegForVideo))
            {
                return;
            }

            Color color = await ColorPaletteUtility.GetMediaAccentColorFromAsync(asUri, useFfmpegForVideo);
            ColorAccentChanged?.Invoke(color);
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"An error has occurred while trying to get media accent color! {ex}",
                                LogType.Error,
                                true);
            SentryHelper.ExceptionHandler(ex);
        }
    }
}