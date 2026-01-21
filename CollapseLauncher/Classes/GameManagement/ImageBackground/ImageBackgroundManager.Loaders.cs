using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CollapseLauncher.Helper.Image;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.Pages;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using Hi3Helper;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI;

// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    #region Fields

    private CancellationTokenSource? _imageLoadingTokenSource;

    private static IValueConverter BoolInvertConverter => field ??= new InverseBooleanConverter();

    #endregion

    private void LoadImageAtIndex(int index, CancellationToken token) =>
        new Thread(() => LoadImageAtIndexCore(index, token))
        {
            IsBackground = true
        }.Start();

    private async void LoadImageAtIndexCore(int index, CancellationToken token)
    {
        try
        {
            if (ImageContextSources.Count <= index ||
                index < 0)
            {
                return;
            }

            // -- Notify changes on context menu properties
            DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(() =>
                                                                            OnPropertyChanged(nameof(
                                                                                CurrentSelectedBackgroundHasOverlayImage)));

            // -- Get context and invalidate previous CTS
            LayeredImageBackgroundContext context = ImageContextSources[index];
            if (Interlocked.Exchange(ref _imageLoadingTokenSource,
                                     CancellationTokenSource.CreateLinkedTokenSource(token)) is { } lastCts)
            {
                await lastCts.CancelAsync();
                lastCts.Dispose();
            }

            token = _imageLoadingTokenSource.Token;

            Uri? downloadedOverlayUri = null;
            // -- Download overlay and background files.
            if (Uri.TryCreate(context.OverlayImagePath, UriKind.Absolute, out Uri? overlayImageUri))
            {
                downloadedOverlayUri      = await GetLocalOrDownloadedFilePath(overlayImageUri, token);
                (downloadedOverlayUri, _) = await GetNativeOrDecodedImagePath(downloadedOverlayUri, token);
            }

            if (!Uri.TryCreate(context.BackgroundImagePath, UriKind.Absolute, out Uri? backgroundImageUri))
            {
                throw new InvalidOperationException($"URI/Path of the background image is malformed! {context.BackgroundImagePath}");
            }

            Uri? downloadedBackgroundUri = await GetLocalOrDownloadedFilePath(backgroundImageUri, token);
            (downloadedBackgroundUri, _) = await GetNativeOrDecodedImagePath(downloadedBackgroundUri, token);

            // -- Get upscaled image file if Waifu2X is enabled
            if (GlobalIsWaifu2XEnabled)
            {
                downloadedOverlayUri    = await TryGetScaledWaifu2XImagePath(downloadedOverlayUri,    token);
                downloadedBackgroundUri = await TryGetScaledWaifu2XImagePath(downloadedBackgroundUri, token);
            }

            token.ThrowIfCancellationRequested();

            // -- Check for codec support (Also spawn dialog to install either native WIC/MediaFoundation decoder or using Ffmpeg decoder)
            if (!await CheckCodecOrSpawnDialog(downloadedBackgroundUri))
            {
                return;
            }

            // -- Read Color Accent information from current background context.
            new Thread(GetMediaAccentColor)
            {
                IsBackground = true
            }.Start(downloadedBackgroundUri);

            // -- Use UI thread and load image layer
            DispatcherQueueExtensions.CurrentDispatcherQueue.TryEnqueue(() => SpawnImageLayer(downloadedOverlayUri, downloadedBackgroundUri, context));
        }
        catch (Exception ex)
        {
            _ = SentryHelper.ExceptionHandlerAsync(ex);
            Logger.LogWriteLine($"[ImageBackgroundManager::LoadImageAtIndex] {ex}",
                                LogType.Error,
                                true);
        }
    }

    private void SpawnImageLayer(Uri? overlayFilePath, Uri? backgroundFilePath, LayeredImageBackgroundContext context)
    {
        LayeredBackgroundImage layerElement = new()
        {
            BackgroundSource          = backgroundFilePath,
            ForegroundSource          = overlayFilePath,
            Tag                       = context,
            ParallaxResetOnUnfocused  = false,
            BackgroundElevationPixels = 64d
        };

        layerElement.BindProperty(this,
                                  nameof(GlobalParallaxHoverSource),
                                  LayeredBackgroundImage.ParallaxHoverSourceProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(GlobalIsBackgroundParallaxEffectEnabled),
                                  LayeredBackgroundImage.IsParallaxEnabledProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(GlobalBackgroundParallaxPixelShift),
                                  LayeredBackgroundImage.ParallaxHorizontalShiftProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(GlobalBackgroundParallaxPixelShift),
                                  LayeredBackgroundImage.ParallaxVerticalShiftProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this, 
                                  nameof(GlobalBackgroundAudioEnabled),
                                  LayeredBackgroundImage.IsAudioEnabledProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(GlobalBackgroundAudioVolume),
                                  LayeredBackgroundImage.AudioVolumeProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(IsBackgroundElevated),
                                  LayeredBackgroundImage.IsBackgroundElevatedProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(ForegroundOpacity),
                                  LayeredBackgroundImage.ForegroundOpacityProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(this,
                                  nameof(SmokeOpacity),
                                  LayeredBackgroundImage.SmokeOpacityProperty,
                                  BindingMode.OneWay);

        layerElement.BindProperty(context,
                                  nameof(context.ForceReload),
                                  LayeredBackgroundImage.UseImageCacheProperty,
                                  BindingMode.OneWay,
                                  BoolInvertConverter);

        layerElement.Transitions.Add(new PopupThemeTransition());
        layerElement.ImageLoaded += LayerElementOnLoaded;
        PresenterGrid?.Children.Add(layerElement);

        // Notify current displayed element change
        OnPropertyChanged(nameof(CurrentBackgroundElement));
    }

    private void LayerElementOnLoaded(LayeredBackgroundImage layerElement)
    {
        List<UIElement> lastElements = PresenterGrid?.Children.ToList() ?? [];
        foreach (UIElement element in lastElements.Where(element => element != layerElement))
        {
            PresenterGrid?.Children.Remove(element);
        }

        layerElement.ImageLoaded -= LayerElementOnLoaded;
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
        if (LayeredBackgroundImage.SupportedVideoExtensionsLookup
                                  .Contains(Path.GetExtension(inputFilePath)))
        {
            return uri;
        }

        string outputFilePath = Path.Combine(LauncherConfig.AppGameImgCachedFolder,
                                             $"scaled_{Path.GetFileNameWithoutExtension(inputFilePath)}.png");

        FileInfo outputFileInfo = new(outputFilePath);
        if (outputFileInfo.Exists)
        {
            return new Uri(outputFileInfo.FullName);
        }

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
        if (context is not Uri { IsFile: true } asUri)
        {
            return;
        }

        Color color = await ColorPaletteUtility.GetMediaAccentColorFromAsync(asUri);
        ColorAccentChanged?.Invoke(color);
    }
}
