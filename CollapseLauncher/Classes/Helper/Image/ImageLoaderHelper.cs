using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Media;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using static CollapseLauncher.Helper.Image.Waifu2X;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

using ImageBlendBrush = Hi3Helper.CommunityToolkit.WinUI.Media.ImageBlendBrush;
using ImageCropper = Hi3Helper.CommunityToolkit.WinUI.Controls.ImageCropper;
using CropShape = Hi3Helper.CommunityToolkit.WinUI.Controls.CropShape;
using ThumbPlacement = Hi3Helper.CommunityToolkit.WinUI.Controls.ThumbPlacement;
using BitmapFileFormat = Hi3Helper.CommunityToolkit.WinUI.Controls.BitmapFileFormat;

namespace CollapseLauncher.Helper.Image
{
    internal static class ImageLoaderHelper
    {
        internal static readonly Dictionary<string, string> SupportedImageFormats =
            new() {
                { "All supported formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) + ';' + string.Join(';', BackgroundMediaUtility.SupportedMediaPlayerExt.Select(x => $"*{x}")) },
                { "Image formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) },
                { "Video formats", string.Join(';', BackgroundMediaUtility.SupportedMediaPlayerExt.Select(x => $"*{x}")) }
            };

        internal static readonly Dictionary<string, string> SupportedStaticImageFormats = new()
            {
                { "Image formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) }
            };

        #region Waifu2X
        private static Waifu2X _waifu2X;
        private static Waifu2XStatus _cachedStatus = Waifu2XStatus.NotInitialized;

        public static Waifu2XStatus Waifu2XStatus
        {
            get
            {
                // Cache the status of waifu2x
                if (_cachedStatus == Waifu2XStatus.NotInitialized)
                {
                    _cachedStatus = VulkanTest();
                }
                return _cachedStatus;
            }
        }

        public static bool IsWaifu2XEnabled
        {
            get => GetAppConfigValue("EnableWaifu2X").ToBool() && IsWaifu2XUsable;
            set
            {
                SetAndSaveConfigValue("EnableWaifu2X", value);
                if (value) InitWaifu2X();
                else DestroyWaifu2X();
            }
        }

        public static bool IsWaifu2XUsable => Waifu2XStatus < Waifu2XStatus.Error;

        private static Waifu2X CreateWaifu2X()
        {
            var waifu2X = new Waifu2X();
            if (waifu2X.Status < Waifu2XStatus.Error)
            {
                waifu2X.SetParam(Param.Noise, -1);
                waifu2X.SetParam(Param.Scale, 2);
                waifu2X.Load(Path.Combine(AppFolder!, @"Assets\Waifu2X_Models\scale2.0x_model.param.bin"),
                    Path.Combine(AppFolder!, @"Assets\Waifu2X_Models\scale2.0x_model.bin"));
                _cachedStatus = waifu2X.Status;
            }
            return waifu2X;
        }

        public static void InitWaifu2X()
        {
            _waifu2X ??= CreateWaifu2X();
        }

        public static void DestroyWaifu2X()
        {
            _waifu2X?.Dispose();
            _waifu2X = null;
        }
        #endregion

        internal static async Task<FileStream> LoadImage(string path, bool isUseImageCropper = false, bool overwriteCachedImage = false)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            double aspectRatioX = InnerLauncherConfig.m_actualMainFrameSize.Width;
            double aspectRatioY = InnerLauncherConfig.m_actualMainFrameSize.Height;
            double scaleFactor = WindowUtility.CurrentWindowMonitorScaleFactor;
            uint targetSourceImageWidth = (uint)(aspectRatioX * scaleFactor);
            uint targetSourceImageHeight = (uint)(aspectRatioY * scaleFactor);
            bool isError = false;

            if (!Directory.Exists(AppGameImgCachedFolder)) Directory.CreateDirectory(AppGameImgCachedFolder!);

            FileStream resizedImageFileStream = null;

            try
            {
                FileInfo inputFileInfo = new FileInfo(path);
                FileInfo resizedFileInfo = GetCacheFileInfo(inputFileInfo.FullName + inputFileInfo.Length);
                if (resizedFileInfo!.Exists && resizedFileInfo.Length > 1 << 15 && !overwriteCachedImage)
                {
                    resizedImageFileStream = resizedFileInfo.Open(StreamUtility.FileStreamOpenReadOpt);
                    return resizedImageFileStream;
                }

                if (isUseImageCropper)
                {
                    resizedImageFileStream = await SpawnImageCropperDialog(path, resizedFileInfo.FullName,
                                                                           targetSourceImageWidth, targetSourceImageHeight);
                    if (resizedImageFileStream == null) return null;
                    return resizedImageFileStream;
                }

                resizedImageFileStream = await GenerateCachedStream(inputFileInfo, targetSourceImageWidth,
                                                                    targetSourceImageHeight);
            }
            catch
            {
                isError = true;
                throw;
            }
            finally
            {
                if (isError && resizedImageFileStream != null)
                {
                    await resizedImageFileStream.DisposeAsync();
                }
            }

            return resizedImageFileStream;
        }

        private static async Task<FileStream> SpawnImageCropperDialog(string filePath, string cachedFilePath,
                                                                      uint toWidth, uint toHeight)
        {
            Grid parentGrid = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(12),
                // Margin = new Thickness(-23, -19, -23, -19)
            };

            ImageCropper imageCropper = new ImageCropper();
            imageCropper.AspectRatio = 16d / 9d;
            imageCropper.CropShape = CropShape.Rectangular;
            imageCropper.ThumbPlacement = ThumbPlacement.Corners;
            imageCropper.HorizontalAlignment = HorizontalAlignment.Stretch;
            imageCropper.VerticalAlignment = VerticalAlignment.Stretch;
            imageCropper.Opacity = 0;

            // Path of image
            Uri overlayImageUri = new Uri(Path.Combine(AppFolder!, @"Assets\Images\ImageCropperOverlay",
                                                       GetAppConfigValue("WindowSizeProfile").ToString() == "Small" ? "small.png" : "normal.png"));

            // Why not use ImageBrush?
            // https://github.com/microsoft/microsoft-ui-xaml/issues/7809
            imageCropper.Overlay = new ImageBlendBrush()
            {
                Opacity = 0.5,
                Stretch = Stretch.Fill,
                Mode = ImageBlendMode.Multiply,
                SourceUri = overlayImageUri
            };

            ContentDialogOverlay dialogOverlay = new ContentDialogOverlay(ContentDialogTheme.Informational)
            {
                Title = Locale.Lang!._Misc!.ImageCropperTitle,
                Content = parentGrid,
                SecondaryButtonText = Locale.Lang._Misc.Cancel,
                PrimaryButtonText = Locale.Lang._Misc.OkayHappy,
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                XamlRoot = (WindowUtility.CurrentWindow as MainWindow)?.Content!.XamlRoot
            };

            LoadImageCropperDetached(filePath, imageCropper, parentGrid, dialogOverlay);

            ContentDialogResult dialogResult = await dialogOverlay.QueueAndSpawnDialog();
            if (dialogResult == ContentDialogResult.Secondary) return null;

            try
            {
                await using (FileStream cachedFileStream =
                             new FileStream(cachedFilePath!, StreamUtility.FileStreamCreateReadWriteOpt))
                {
                    dialogOverlay.IsPrimaryButtonEnabled   = false;
                    dialogOverlay.IsSecondaryButtonEnabled = false;
                    await imageCropper.SaveAsync(cachedFileStream.AsRandomAccessStream()!, BitmapFileFormat.Png);
                }

                GC.WaitForPendingFinalizers();
                GC.WaitForFullGCComplete();
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"Exception caught at [ImageLoaderHelper::SpawnImageCropperDialog]\r\n{ex}", LogType.Error, true);
            }

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);
            return await GenerateCachedStream(cachedFileInfo, toWidth, toHeight, true);
        }

        private static async void LoadImageCropperDetached(string filePath, ImageCropper imageCropper,
                                                           Grid parentGrid, ContentDialogOverlay dialogOverlay)
        {
            StackPanel loadingMsgPanel = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 1d
            };

            loadingMsgPanel.AddElementToStackPanel(new ProgressRing
            {
                IsIndeterminate = true,
                Width = 16,
                Height = 16,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            loadingMsgPanel.AddElementToStackPanel(new TextBlock
            {
                Text = "Loading the Image",
                FontWeight = FontWeights.SemiBold
            });

            parentGrid.AddElementToGridRowColumn(imageCropper);
            parentGrid.AddElementToGridRowColumn(loadingMsgPanel);

            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            await imageCropper!.LoadImageFromFile(file!);

            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();

            Storyboard storyboardAnim = new();
            DoubleAnimation loadingMsgPanelAnim = loadingMsgPanel.CreateDoubleAnimation("Opacity", 0, 1, null,
                                                                                        TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
            DoubleAnimation imageCropperAnim = imageCropper.CreateDoubleAnimation("Opacity", 1, 0, null,
                                                                                  TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
            storyboardAnim.Children!.Add(loadingMsgPanelAnim);
            storyboardAnim.Children!.Add(imageCropperAnim);
            storyboardAnim.Begin();

            dialogOverlay!.IsPrimaryButtonEnabled = true;
        }

        private static async Task<FileStream> GenerateCachedStream(FileInfo InputFileInfo,
                                                                   uint ToWidth, uint ToHeight,
                                                                   bool isFromCropProcess = false)
        {
            if (isFromCropProcess)
            {
                string InputFileName = InputFileInfo!.FullName;
                try
                {
                    InputFileInfo.MoveTo(InputFileInfo.FullName + "_old", true);
                    FileInfo newCachedFileInfo = new FileInfo(InputFileName);
                    await using (FileStream newCachedFileStream = newCachedFileInfo.Open(StreamUtility.FileStreamCreateWriteOpt))
                        await using (FileStream oldInputFileStream = InputFileInfo.Open(StreamUtility.FileStreamOpenReadOpt))
                            await ResizeImageStream(oldInputFileStream, newCachedFileStream, ToWidth, ToHeight);

                    InputFileInfo.Delete();

                    return newCachedFileInfo.Open(StreamUtility.FileStreamOpenReadOpt);
                }
                catch (IOException ex)
                {
                    Logger.LogWriteLine($"[ImageLoaderHelper::GenerateCachedStream] IOException Caught! Opening InputFile instead...\r\n{ex}", LogType.Error, true);
                    return InputFileInfo.Open(StreamUtility.FileStreamOpenReadOpt);
                }
            }

            FileInfo cachedFileInfo = GetCacheFileInfo(InputFileInfo!.FullName + InputFileInfo.Length);
            bool isCachedFileExist = cachedFileInfo!.Exists && cachedFileInfo.Length > 1 << 15;
            if (isCachedFileExist) return cachedFileInfo.Open(StreamUtility.FileStreamOpenReadOpt);

            await using (FileStream cachedFileStream = cachedFileInfo.Create())
                await using (FileStream inputFileStream = InputFileInfo.Open(StreamUtility.FileStreamOpenReadOpt))
                    await ResizeImageStream(inputFileStream, cachedFileStream, ToWidth, ToHeight);

            return cachedFileInfo.Open(StreamUtility.FileStreamOpenReadOpt);
        }

        internal static FileInfo GetCacheFileInfo(string filePath)
        {
            string cachedFileHash = ConverterTool.BytesToCRC32Simple(filePath);
            string cachedFilePath = Path.Combine(AppGameImgCachedFolder!, cachedFileHash!);
            if (IsWaifu2XEnabled)
                cachedFilePath += "_waifu2x";
            return new FileInfo(cachedFilePath);
        }

        public static async Task ResizeImageStream(Stream input, Stream output, uint ToWidth, uint ToHeight)
        {
            ProcessImageSettings settings = new()
            {
                Width = (int)ToWidth,
                Height = (int)ToHeight,
                HybridMode = HybridScaleMode.Off,
                Interpolation = InterpolationSettings.CubicSmoother,
                Anchor = CropAnchor.Bottom | CropAnchor.Center
            };
            settings.TrySetEncoderFormat(ImageMimeTypes.Png);

            await Task.Run(() =>
            {
                var imageFileInfo = ImageFileInfo.Load(input!);
                var frame = imageFileInfo.Frames[0];
                input.Position = 0;
                if (IsWaifu2XEnabled && (frame.Width < ToWidth || frame.Height < ToHeight))
                {
                    var pipeline = MagicImageProcessor.BuildPipeline(input, ProcessImageSettings.Default)
                        .AddTransform(new Waifu2XTransform(_waifu2X));
                    MagicImageProcessor.ProcessImage(pipeline.PixelSource!, output!, settings);
                    pipeline.Dispose();
                }
                else
                {
                    MagicImageProcessor.ProcessImage(input!, output!, settings);
                }
            });
        }

        public static async Task<(Bitmap, BitmapImage)> GetResizedBitmapNew(string filePath)
        {
            Bitmap bitmapRet;
            BitmapImage bitmapImageRet;

            FileStream cachedFileStream = await LoadImage(filePath);
            if (cachedFileStream == null) return (null, null);
            await using (cachedFileStream)
            {
                bitmapRet = await Task.Run(() => Stream2Bitmap(cachedFileStream.AsRandomAccessStream()));
                bitmapImageRet = await Stream2BitmapImage(cachedFileStream.AsRandomAccessStream());
            }

            return (bitmapRet, bitmapImageRet);
        }

        public static async Task<BitmapImage> Stream2BitmapImage(IRandomAccessStream image)
        {
            var ret = new BitmapImage();
            image!.Seek(0);
            await ret.SetSourceAsync(image);
            return ret;
        }

        public static Bitmap Stream2Bitmap(IRandomAccessStream image)
        {
            image!.Seek(0);
            return new Bitmap(image.AsStream()!);
        }

        public static async ValueTask DownloadAndEnsureCompleteness(string url, string outputPath, CancellationToken token)
        {
            // Initialize the FileInfo and check if the file exist
            FileInfo fI = new FileInfo(outputPath);
            bool isFileExist = IsFileCompletelyDownloaded(fI);

            // If the file and the file assumed to exist, then return
            if (isFileExist) return;

            // If not, then try download the file
            await TryDownloadToCompleteness(url, fI, token);
        }

        public static bool IsFileCompletelyDownloaded(FileInfo fileInfo)
        {
            // Get the parent path and file name
            string outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
            string outputFileName = Path.GetFileName(fileInfo.FullName);

            // Try to get the prop file which includes the filename + the suggested size provided
            // by the network stream if it has been downloaded before
            if (outputParentPath != null)
            {
                string propFilePath = Directory.EnumerateFiles(outputParentPath, $"{outputFileName}#*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                // Check if the file is found (not null), then try parse the information
                if (string.IsNullOrEmpty(propFilePath))
                {
                    return false;
                }

                // Try split the filename into a segment by # char
                string[] propSegment = Path.GetFileName(propFilePath).Split('#');
                // Assign the check if the condition met and set the file existence status
                return propSegment.Length >= 2
                       && long.TryParse(propSegment[1], null, out long suggestedSize)
                       && fileInfo.Exists && fileInfo.Length == suggestedSize;
            }

            // If the prop doesn't exist, then return false to assume that the file doesn't exist
        }

        public static async void TryDownloadToCompletenessAsync(string url, FileInfo fileInfo, CancellationToken token)
            => await TryDownloadToCompleteness(url, fileInfo, token);

        public static async ValueTask TryDownloadToCompleteness(string url, FileInfo fileInfo, CancellationToken token)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                Logger.LogWriteLine($"Start downloading resource from: {url}", LogType.Default, true);

                // Try to get the remote stream and download the file
                await using Stream netStream = await FallbackCDNUtil.GetHttpStreamFromResponse(url, token);
                await using Stream outStream = fileInfo.Open(new FileStreamOptions()
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                    Share = FileShare.ReadWrite,
                    Options = FileOptions.Asynchronous
                });

                // Get the file length
                long fileLength = netStream.Length;

                // Create the prop file for download completeness checking
                string outputParentPath = Path.GetDirectoryName(fileInfo.FullName);
                string outputFilename = Path.GetFileName(fileInfo.FullName);
                if (outputParentPath != null)
                {
                    string propFilePath = Path.Combine(outputParentPath, $"{outputFilename}#{netStream.Length}");
                    await File.Create(propFilePath).DisposeAsync();
                }

                // Copy (and download) the remote streams to local
                int read;
                while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                    await outStream.WriteAsync(buffer, 0, read, token);

                Logger.LogWriteLine($"Resource download from: {url} has been completed and stored locally into:"
                    + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)", LogType.Default, true);
            }
            // Ignore cancellation exceptions
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
#if !DEBUG
            catch (Exception ex)
            {
                // ErrorSender.SendException(ex, ErrorType.Connection);
                Logger.LogWriteLine($"Error has occured while downloading in background for: {url}\r\n{ex}", LogType.Error, true);
            }
#endif
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public static string GetCachedSprites(string URL, CancellationToken token)
        {
            if (string.IsNullOrEmpty(URL)) return URL;
            if (token.IsCancellationRequested) return URL;

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (IsFileCompletelyDownloaded(fInfo))
            {
                return cachePath;
            }

            TryDownloadToCompletenessAsync(URL, fInfo, token);
            return URL;

        }

        public static async ValueTask<string> GetCachedSpritesAsync(string URL, CancellationToken token)
        {
            if (string.IsNullOrEmpty(URL)) return URL;

            string cachePath = Path.Combine(AppGameImgCachedFolder, Path.GetFileNameWithoutExtension(URL));
            if (!Directory.Exists(AppGameImgCachedFolder))
                Directory.CreateDirectory(AppGameImgCachedFolder);

            FileInfo fInfo = new FileInfo(cachePath);
            if (!IsFileCompletelyDownloaded(fInfo))
            {
                await TryDownloadToCompleteness(URL, fInfo, token);
            }
            return cachePath;
        }
    }
}
