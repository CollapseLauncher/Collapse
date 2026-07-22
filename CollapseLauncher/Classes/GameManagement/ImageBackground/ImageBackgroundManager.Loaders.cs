using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Pages;
using CollapseLauncher.XAMLs.Theme.CustomControls;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
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

    #endregion

    private void LoadImageAtIndex(int index, bool forceLoadToStatic, CancellationToken token, bool forceReload = false)
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
        new Thread(async void () =>
        {
            try
            {
                await LoadImageAtIndexCore(index, forceLoadToStatic, token, forceReload).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.LogWriteLine($"{e}", LogType.Error, true);
            }
        })
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest
        }.UnsafeStart();
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

    public void PreloadBackground(PresetConfig? preset, Grid? presenterGrid)
    {
        if (preset == null || presenterGrid == null) return;

        CancelCurrentBackgroundLoad();
        Interlocked.Increment(ref _loadGeneration);

        PresenterGrid = presenterGrid;
        string autoPlayKey = $"LastIsEnableBackgroundAutoPlay-{preset.GameName}-{preset.ZoneName}";
        CurrentIsEnableBackgroundAutoPlayKey = autoPlayKey;

        string cachedBgKey = $"CachedBg-{preset.GameName}-{preset.ZoneName}";
        string? backgroundUrl    = LauncherConfig.GetAppConfigValue($"{cachedBgKey}-Background").ToString();
        string? staticBgUrl      = LauncherConfig.GetAppConfigValue($"{cachedBgKey}-StaticBackground").ToString();
        string? overlayUrl       = LauncherConfig.GetAppConfigValue($"{cachedBgKey}-Overlay").ToString();

        if (string.IsNullOrEmpty(backgroundUrl) && string.IsNullOrEmpty(staticBgUrl)) return;

        string? primaryBg = backgroundUrl ?? staticBgUrl;

        LayeredImageBackgroundContext context = new()
        {
            OriginOverlayImagePath          = overlayUrl,
            OriginBackgroundImagePath       = backgroundUrl,
            OriginBackgroundStaticImagePath = staticBgUrl,
            OverlayImagePath                = overlayUrl,
            BackgroundImagePath             = primaryBg,
            BackgroundImageStaticPath       = staticBgUrl,
            IsVideo                         = IsVideoMediaFileExtensionSupported(primaryBg ?? staticBgUrl!),
            IsCustom                        = false
        };

        if (CheckIsContextCached(context))
        {
            string? localOverlay    = TryGetCachedLocalPath(overlayUrl);
            string? localBackground = TryGetCachedLocalPath(primaryBg);
            string? localStatic     = TryGetCachedLocalPath(staticBgUrl);

            bool overlayRequired  = !string.IsNullOrEmpty(overlayUrl);
            bool staticRequired   = !string.IsNullOrEmpty(staticBgUrl);

            if (localBackground != null &&
                (!overlayRequired || localOverlay != null) &&
                (!staticRequired  || localStatic != null))
            {
                bool willDisplayStatic = !IsVideoMediaFileExtensionSupported(primaryBg ?? staticBgUrl!) ||
                                         (!CurrentIsEnableCustomImage &&
                                          !GlobalIsEnableCustomImage &&
                                          !CurrentIsEnableBackgroundAutoPlay);
                Uri accentPreviewUri = willDisplayStatic ? new Uri(localStatic) : new Uri(localBackground);

                RestoreSavedAccent(cachedBgKey, accentPreviewUri);

                ImageContextSources.Clear();
                ImageContextSources.Add(context);
                OnPropertyChanged(nameof(CurrentSelectedBackgroundContext));
                OnPropertyChanged(nameof(CurrentBackgroundCount));

                CurrentBackgroundElement = CreateLayerElement(localOverlay != null ? new Uri(localOverlay) : null,
                                                              new Uri(localBackground),
                                                              localStatic != null ? new Uri(localStatic) : null,
                                                              context,
                                                              IsVideoMediaFileExtensionSupported(primaryBg ?? staticBgUrl!));

                bool isUseFFmpeg = GlobalIsUseFFmpeg && GlobalIsFFmpegAvailable;
                new Thread(async void (ctx) =>
                {
                    try
                    {
                        await GetMediaAccentColor(ctx).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Logger.LogWriteLine($"{e}", LogType.Error, true);
                    }
                })
                {
                    IsBackground = true
                }.UnsafeStart((accentPreviewUri, isUseFFmpeg, cachedBgKey));
                return;
            }
        }

        // Show preset placeholder.
        string placeholderPath = GetPlaceholderBackgroundImageFrom(null);
        PresenterGrid.Background = new ImageBrush
        {
            ImageSource = new BitmapImage(new Uri(placeholderPath)),
            Stretch     = Stretch.UniformToFill
        };
        PresenterGrid.Children.Clear();
        CurrentBackgroundElement = null;
        _displayedContext = null;
        IsBackgroundLoading = true;
    }

    private async Task LoadImageAtIndexCore(int index, bool forceLoadToStatic, CancellationToken token, bool forceReload = false)
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
            if (downloadedBackgroundUri == null)
            {
                if (!context.IsCustom)
                {
                    NeedFallbackPlaceholder = false;
                    OnBackgroundLoadFailed?.Invoke();
                }
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

            // Try to force loading static image if requested.
            if (forceLoadToStatic && downloadedBackgroundStaticUri != null)
            {
                isVideo                       = false;
                downloadedBackgroundUri       = downloadedBackgroundStaticUri;
                downloadedBackgroundStaticUri = null;
            }

            // Read accent color from the source that is actually displayed.
            bool willDisplayStatic = !isVideo ||
                                     (!CurrentIsEnableCustomImage &&
                                      !GlobalIsEnableCustomImage &&
                                      !CurrentIsEnableBackgroundAutoPlay);
            Uri? accentSourceUri = willDisplayStatic
                ? downloadedBackgroundStaticUri ?? downloadedBackgroundUri
                : downloadedBackgroundUri;

            new Thread(async void (ctx) =>
            {
                try
                {
                    await GetMediaAccentColor(ctx).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.LogWriteLine($"{e}", LogType.Error, true);
                }
            })
            {
                IsBackground = true
            }.UnsafeStart((accentSourceUri, isUseFFmpeg, CurrentCachedBgKey));

            if (!context.IsCustom &&
                !string.IsNullOrEmpty(CurrentCachedBgKey))
            {
                LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-Background",       context.OriginBackgroundImagePath ?? "");
                LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-StaticBackground", context.OriginBackgroundStaticImagePath ?? "");
                LauncherConfig.SetAndSaveConfigValue($"{CurrentCachedBgKey}-Overlay",          context.OriginOverlayImagePath ?? "");
            }

            // -- Use UI thread and load image layer
            int captureGen = _loadGeneration;
            bool captureForceReload = forceReload;
            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(() => SpawnImageLayer(downloadedOverlayUri,
                                                  downloadedBackgroundUri,
                                                  downloadedBackgroundStaticUri,
                                                  isVideo,
                                                  context,
                                                  captureGen,
                                                  forceReload: captureForceReload));

            GlobalIsFFmpegCurrentlyUsed = isVideo && isUseFFmpeg;
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
                                 Uri? backgroundStaticFilePath,
                                 bool isVideo,
                                 LayeredImageBackgroundContext context,
                                 int loadGeneration,
                                 bool forceReload = false)
    {
        if (loadGeneration != _loadGeneration)
        {
            return;
        }

        if (!forceReload &&
            CurrentBackgroundElement != null &&
            _displayedContext != null &&
            context.Equals(_displayedContext))
        {
            return;
        }

        CurrentBackgroundElement = CreateLayerElement(overlayFilePath, backgroundFilePath, backgroundStaticFilePath, context, isVideo);
    }

    private void RestoreSavedAccent(string cachedBgKey, Uri? fallbackSourceUri = null)
    {
        string? savedHex = LauncherConfig.GetAppConfigValue($"{cachedBgKey}-AccentColor").ToString();
        if (!string.IsNullOrEmpty(savedHex) && savedHex!.Length >= 6 && ThemeRootElement != null)
        {
            if (TryParseHexColor(savedHex, out Color accentColor))
            {
                ThemeRootElement.ChangeAccentColor(accentColor);
                return;
            }
        }

        if (fallbackSourceUri != null && ThemeRootElement != null)
        {
            try
            {
                Color syncColor = Task.Run(async () =>
                    await ColorPaletteUtility.GetMediaAccentColorFromAsync(fallbackSourceUri, false)
                ).GetAwaiter().GetResult();
                ThemeRootElement.ChangeAccentColor(syncColor);
                LauncherConfig.SetAndSaveConfigValue($"{cachedBgKey}-AccentColor",
                    $"{syncColor.R:X2}{syncColor.G:X2}{syncColor.B:X2}");
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"RestoreSavedAccent failed: {ex}", LogType.Error, true);
            }
        }
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = default;
        if (hex.Length < 6) return false;
        if (hex[0] == '#')
            hex = hex[1..];
        if (!uint.TryParse(hex, NumberStyles.HexNumber, null, out uint argb)) return false;
        if (hex.Length <= 6)
            argb = 0xFF_000000 | argb;
        color = Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);
        return true;
    }

    private LayeredBackgroundImage CreateLayerElement(Uri? overlayFilePath,
                                                      Uri? backgroundFilePath,
                                                      Uri? backgroundStaticFilePath,
                                                      LayeredImageBackgroundContext context,
                                                      bool isVideo)
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

        layerElement.BindProperty(LayeredBackgroundImage.FfmpegDecoderModeProperty,
                                  this,
                                  nameof(GlobalFFmpegDecodingMode),
                                  bindingMode: BindingMode.OneWay);

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

        layerElement.ImageLoaded += LayerElementOnLoaded;
        PresenterGrid?.Children.Add(layerElement);

        layerElement.Tag = isVideo;
        _displayedContext = context;
        return layerElement;
    }

    private void LayerElementOnLoaded(LayeredBackgroundImage layerElement)
    {
        layerElement.ImageLoaded -= LayerElementOnLoaded;
        layerElement.Transitions.Add(new PopupThemeTransition());

        if (PresenterGrid != null)
        {
            List<UIElement> toRemove = PresenterGrid.Children.Where(element => element != layerElement).ToList();
            foreach (UIElement oldElement in toRemove)
            {
                if (oldElement is LayeredBackgroundImage oldLayer)
                    oldLayer.ImageLoaded -= LayerElementOnLoaded;
                PresenterGrid.Children.Remove(oldElement);
            }

            PresenterGrid.Background = null;
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

            if (color == default(Color)) return;

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