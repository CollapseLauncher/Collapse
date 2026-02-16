using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.StreamUtility;
using CollapseLauncher.XAMLs.Theme.CustomControls.LayeredBackgroundImage;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Media;
using Hi3Helper;
using Hi3Helper.CommunityToolkit.WinUI.Controls;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Hi3Helper.Win32.WinRT.WindowsStream;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;
using UIElementExtensions = CollapseLauncher.Extension.UIElementExtensions;

#pragma warning disable IDE0130
#nullable enable
namespace CollapseLauncher.GameManagement.ImageBackground;

public partial class ImageBackgroundManager
{
    /// <summary>
    /// Get local path of the resized/cropped custom image. The return values are cropped image path.
    /// If the input path is a video, then it will return the input path.
    /// <br/><br/>
    /// To perform cropping request, you must set <paramref name="performCropRequest"/> to <see langword="true"/>.
    /// Otherwise, it will return the input paths.
    /// </summary>
    private static async Task<(string? ProcessedOverlayPath, string? ProcessedBackgroundPath, bool IsCancelResize)>
        GetCroppedCustomImage(string?           overlayUrlOrPath,
                              string?           backgroundUrlOrPath,
                              bool              performCropRequest,
                              CancellationToken token)
    {
        if ((string.IsNullOrEmpty(overlayUrlOrPath) &&
             string.IsNullOrEmpty(backgroundUrlOrPath)) ||
             string.IsNullOrEmpty(backgroundUrlOrPath))
        {
            return (null, null, false); // Skip from processing
        }

        // -- Check if the file is video or image
        if (LayeredBackgroundImage.SupportedVideoExtensionsLookup
                                  .Contains(Path.GetExtension(backgroundUrlOrPath)))
        {
            return (overlayUrlOrPath, backgroundUrlOrPath, false);
        }

        // -- Try to get existing image file path
        if (TryGetCroppedImageFilePath(backgroundUrlOrPath, out string croppedBackgroundPath) &&
            !performCropRequest)
        {
            return (null, croppedBackgroundPath, false);
        }

        try
        {
            // -- Try to get original or decoded image paths.
            (string? decodedOverlayPath, _) = string.IsNullOrEmpty(overlayUrlOrPath)
                ? (null, default)
                : await GetNativeOrDecodedImagePath(overlayUrlOrPath, token);

            (string decodedBackgroundPath, ImageExternalCodecType backgroundCodecType) =
                await GetNativeOrDecodedImagePath(backgroundUrlOrPath, token);

            // -- Create path of image to Uri
            Uri overlayImageUri = !string.IsNullOrEmpty(decodedOverlayPath)
                ? new Uri(decodedOverlayPath)
                : new Uri(Path.Combine(LauncherConfig.AppExecutableDir, @"Assets\Images\ImageCropperOverlay",
                                       LauncherConfig.GetAppConfigValue("WindowSizeProfile") == "Small"
                                           ? "small.png"
                                           : "normal.png"));

            Uri backgroundImageUri = new(decodedBackgroundPath);

            // -- Create elements, load image and spawn the dialog box.
            Grid parentGrid = UIElementExtensions
               .Create<Grid>(SetParentGridProperties);

            ContentDialogOverlay dialog = UIElementExtensions
               .Create<ContentDialogOverlay>(SetDialogProperties);

            ImageCropper cropper = UIElementExtensions
               .Create<ImageCropper>(SetImageCropperProperties);

            // -- Register to close dialog if cancellation is triggered outside the event.
            token.Register(() => dialog.Hide());
            if (token.IsCancellationRequested)
            {
                return (null, null, true);
            }

            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(() => LoadImageCropperDetached(backgroundImageUri,
                                                          cropper,
                                                          parentGrid,
                                                          dialog,
                                                          backgroundCodecType == ImageExternalCodecType.Svg,
                                                          token));

            ContentDialogResult dialogResult = await dialog.QueueAndSpawnDialog();
            if (dialogResult is ContentDialogResult.Secondary or ContentDialogResult.None)
            {
                return (null, null, true);
            }

            // -- Save cropped image
            await SaveCroppedImageToFilePath(backgroundUrlOrPath, croppedBackgroundPath, cropper, token);

            return (null, croppedBackgroundPath, false);

            void SetDialogProperties(ContentDialogOverlay element)
            {
                element.Title                  = Locale.Lang._Misc.ImageCropperTitle;
                element.Content                = parentGrid;
                element.SecondaryButtonText    = Locale.Lang._Misc.Cancel;
                element.PrimaryButtonText      = Locale.Lang._Misc.OkayHappy;
                element.DefaultButton          = ContentDialogButton.Primary;
                element.IsPrimaryButtonEnabled = false;

                element.XamlRoot = (WindowUtility.CurrentWindow as MainWindow)?.Content.XamlRoot;
            }

            static void SetParentGridProperties(Grid element)
            {
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment   = VerticalAlignment.Stretch;
                element.CornerRadius        = new CornerRadius(12);
            }

            void SetImageCropperProperties(ImageCropper element)
            {
                element.AspectRatio         = 16d / 9d;
                element.CropShape           = CropShape.Rectangular;
                element.ThumbPlacement      = ThumbPlacement.Corners;
                element.HorizontalAlignment = HorizontalAlignment.Stretch;
                element.VerticalAlignment   = VerticalAlignment.Stretch;
                element.Opacity             = 0;

                // Why not use ImageBrush?
                // https://github.com/microsoft/microsoft-ui-xaml/issues/7809
                element.Overlay = new Hi3Helper.CommunityToolkit.WinUI.Media.ImageBlendBrush
                {
                    Opacity   = 0.5,
                    Stretch   = Stretch.Fill,
                    Mode      = ImageBlendMode.Multiply,
                    SourceUri = overlayImageUri
                };
            }
        }
        catch (OperationCanceledException)
        {
            return (null, null, true);
        }
    }

    /// <summary>
    /// Load the image to <see cref="ImageCropper"/> instance in asynchronous detached manner.
    /// </summary>
    internal static async void LoadImageCropperDetached(Uri                  filePath,
                                                        ImageCropper         imageCropper,
                                                        Grid                 parentGrid,
                                                        ContentDialogOverlay dialogOverlay,
                                                        bool                 isSvg,
                                                        CancellationToken    token = default)
    {
        try
        {
            StackPanel loadingMsgPanel =
                UIElementExtensions
                   .Create<StackPanel>(x =>
                                                          {
                                                              x.Orientation         = Orientation.Horizontal;
                                                              x.HorizontalAlignment = HorizontalAlignment.Center;
                                                              x.VerticalAlignment   = VerticalAlignment.Center;
                                                              x.Opacity             = 1d;
                                                          });

            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(() =>
                           {
                               loadingMsgPanel.AddElementToStackPanel(new ProgressRing
                               {
                                   IsIndeterminate   = true,
                                   Width             = 16,
                                   Height            = 16,
                                   VerticalAlignment = VerticalAlignment.Center,
                                   Margin            = new Thickness(0, 0, 8, 0)
                               });
                               loadingMsgPanel.AddElementToStackPanel(new TextBlock
                               {
                                   Text       = "Loading the Image",
                                   FontWeight = FontWeights.SemiBold
                               });

                               parentGrid.AddElementToGridRowColumn(imageCropper);
                               parentGrid.AddElementToGridRowColumn(loadingMsgPanel);
                           });

            ImageSource source;
            if (isSvg)
            {
                source = new SvgImageSource(filePath);
            }
            else
            {
                WriteableBitmap           bitmap       = new(1, 1);
                await using Stream        fileStream   = await OpenStreamFromFileOrUrl(filePath, token);
                using IRandomAccessStream randomStream = fileStream.AsRandomAccessStream(true);
                await bitmap.SetSourceAsync(randomStream);
                source = bitmap;
            }

            DispatcherQueueExtensions
               .CurrentDispatcherQueue
               .TryEnqueue(() =>
                           {
                               imageCropper.Source = source;

                               GC.WaitForPendingFinalizers();
                               GC.WaitForFullGCComplete();

                               dialogOverlay.IsPrimaryButtonEnabled = true;

                               Storyboard storyboardAnim = new();
                               DoubleAnimation loadingMsgPanelAnim = loadingMsgPanel.CreateDoubleAnimation("Opacity", 0,
                                   1, null,
                                   TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
                               DoubleAnimation imageCropperAnim = imageCropper.CreateDoubleAnimation("Opacity", 1, 0,
                                   null,
                                   TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
                               storyboardAnim.Children.Add(loadingMsgPanelAnim);
                               storyboardAnim.Children.Add(imageCropperAnim);
                               storyboardAnim.Begin();
                           });
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"An error has occurred while trying to spawn Image Cropper!: {ex}",
                                LogType.Error,
                                true);
            SentryHelper.ExceptionHandler(ex);
            DispatcherQueueExtensions.TryEnqueue(dialogOverlay.Hide);
        }
    }

    /// <summary>
    /// Save the cropped image from <see cref="ImageCropper"/> instance.
    /// </summary>
    internal static async Task SaveCroppedImageToFilePath(
        string            imageSourceFilePath,
        string            imageTargetFilePath,
        ImageCropper      imageCropper,
        CancellationToken token)
    {
        await using Stream imageSourceStream = await OpenStreamFromFileOrUrl(imageSourceFilePath, token);
        (Rect cropArea, _) = DispatcherQueueExtensions.TryEnqueue(imageCropper.GetCropArea);
        try
        {
            Rectangle cropAreaInt = new((int)cropArea.X,
                                        (int)cropArea.Y,
                                        (int)cropArea.Width,
                                        (int)cropArea.Height);
            await ConvertImageToPngFromStream(imageSourceStream, imageTargetFilePath, cropAreaInt, token);
        }
        finally
        {
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();
        }
    }

    /// <summary>
    /// Converts any (seekable) image stream to PNG.
    /// </summary>
    private static async Task ConvertImageToPngFromStream(
        Stream            sourceStream,
        string            targetFilePath,
        Rectangle         cropArea = default,
        CancellationToken token    = default)
    {
        FileInfo targetFileInfo = new FileInfo(targetFilePath)
                                 .EnsureCreationOfDirectory()
                                 .EnsureNoReadOnly();

        bool isError = false;
        try
        {
            await using FileStream targetFileStream =
                targetFileInfo.Open(FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            await ConvertImageToPngFromStream(sourceStream, targetFileStream, cropArea, token);
        }
        catch
        {
            isError = true;
            throw;
        }
        finally
        {
            if (isError)
            {
                targetFileInfo.TryDeleteFile();
            }
        }
    }

    /// <summary>
    /// Converts any (seekable) image stream to PNG.
    /// </summary>
    private static Task ConvertImageToPngFromStream(
        Stream            sourceStream,
        Stream            targetStream,
        Rectangle         cropArea = default,
        CancellationToken token    = default)
    {
        TaskCompletionSource tcs = new();
        new Thread(Impl)
        {
            IsBackground = true
        }.Start((sourceStream, targetStream, tcs, token, cropArea));

        return tcs.Task;

        static void Impl(object? state)
        {
            (Stream innerSourceStream,
             Stream innerTargetStream,
             TaskCompletionSource tcs,
             CancellationToken token,
             Rectangle cropArea) =
                (ValueTuple<Stream, Stream, TaskCompletionSource, CancellationToken, Rectangle>)state!;

            try
            {
                // We won't do any post-processing and just do a straight conversion to PNG.
                // The post-processing (like resizing with Waifu2X) will be done on image loading process.
                ProcessImageSettings settings = new()
                {
                    DpiX             = 96,
                    DpiY             = 96,
                    ColorProfileMode = ColorProfileMode.Preserve,
                    BlendingMode     = GammaMode.Linear,
                    Crop             = cropArea,
                    EncoderOptions   = new PngEncoderOptions()
                };

                if (IsWebpExtendedFormat(innerSourceStream, out ColorProfileMode colorProfileMode))
                {
                    settings.ColorProfileMode = colorProfileMode;
                }

                if (token.IsCancellationRequested)
                {
                    tcs.SetCanceled(token);
                    return;
                }

                using (ProcessingPipeline pipeline = MagicImageProcessor.BuildPipeline(innerSourceStream, settings))
                {
                    pipeline.WriteOutput(innerTargetStream);
                }

                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }
    }

    private static ReadOnlySpan<byte> Vp8XSignature => "VP8X"u8;

    private static bool IsWebpExtendedFormat(Stream stream, out ColorProfileMode colorProfileMode)
    {
        long oldPos = stream.Position;
        stream.Position = 0;

        Span<byte> buffer = stackalloc byte[256];
        int read = stream.Read(buffer);
        buffer = buffer[..read];

        try
        {
            if (buffer.Length < 16 ||
                !buffer.Slice(12, 4).SequenceEqual(Vp8XSignature))
            {
                colorProfileMode = default;
                stream.Position  = oldPos;
                return false;
            }

            colorProfileMode = ColorProfileMode.Ignore;
            return true;
        }
        finally
        {
            stream.Position = oldPos;
        }
    }

    /// <summary>
    /// Get native or redirected decoded image path.
    /// </summary>
    /// <exception cref="NotSupportedException">If the codec of the image is not supported.</exception>
    internal static async Task<(string, ImageExternalCodecType)> GetNativeOrDecodedImagePath(string filePath, CancellationToken token)
    {
        // Check the extension type. If the type is native or a video file, then just return the original path.
        if (await TryGetImageCodecType(filePath, token) is var codecType &&
            codecType is ImageExternalCodecType.Default or ImageExternalCodecType.Svg)
        {
            return (filePath, codecType);
        }

        // Return null if the codec type is not supported.
        if (codecType == ImageExternalCodecType.NotSupported)
        {
            throw new NotSupportedException($"The format of the image: {filePath} is not supported!");
        }

        // Try to get decoded temporary file. If it exists, return the file path.
        if (TryGetDecodedTemporaryFile(filePath, out string decodedFilePath))
        {
            return (decodedFilePath, ImageExternalCodecType.Default);
        }

        // Otherwise, try to convert the image to png and write it to given decoded file path.
        await using Stream sourceStream = await OpenStreamFromFileOrUrl(filePath, token);
        await ConvertImageToPngFromStream(sourceStream, decodedFilePath, token: token);

        return (decodedFilePath, codecType);
    }

    /// <summary>
    /// Get native or redirected decoded image path.
    /// </summary>
    /// <exception cref="NotSupportedException">If the codec of the image is not supported.</exception>
    internal static async Task<(Uri, ImageExternalCodecType)> GetNativeOrDecodedImagePath(
        Uri filePath, CancellationToken token)
    {
        (string, ImageExternalCodecType) result = await GetNativeOrDecodedImagePath(filePath.ToString(), token);
        return (new Uri(result.Item1), result.Item2);
    }
}
