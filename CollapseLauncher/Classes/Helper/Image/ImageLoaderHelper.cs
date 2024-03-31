using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Background;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Controls;
using Hi3Helper;
using Hi3Helper.Data;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using static CollapseLauncher.Helper.Image.Waifu2X;
using static Hi3Helper.Shared.Region.LauncherConfig;
using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

namespace CollapseLauncher.Helper.Image
{
    internal static class ImageLoaderHelper
    {
        internal static Dictionary<string, string> SupportedImageFormats =
            new() {
                { "All supported formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) + ';' + string.Join(';', BackgroundMediaUtility.SupportedMediaPlayerExt.Select(x => $"*{x}")) },
                { "Image formats", string.Join(';', BackgroundMediaUtility.SupportedImageExt.Select(x => $"*{x}")) },
                { "Video formats", string.Join(';', BackgroundMediaUtility.SupportedMediaPlayerExt.Select(x => $"*{x}")) }
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
            double dpiScale = InnerLauncherConfig.m_appDPIScale;
            uint targetSourceImageWidth = (uint)(aspectRatioX * dpiScale);
            uint targetSourceImageHeight = (uint)(aspectRatioY * dpiScale);
            bool isError = false;

            if (!Directory.Exists(AppGameImgCachedFolder)) Directory.CreateDirectory(AppGameImgCachedFolder!);

            FileStream resizedImageFileStream = null;

            try
            {
                FileInfo inputFileInfo = new FileInfo(path);
                FileInfo resizedFileInfo = GetCacheFileInfo(inputFileInfo.FullName + inputFileInfo.Length);
                if (resizedFileInfo!.Exists && resizedFileInfo.Length > 1 << 15 && !overwriteCachedImage)
                {
                    resizedImageFileStream = resizedFileInfo.OpenRead();
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
                                                                      uint ToWidth, uint ToHeight)
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

            ContentDialogOverlay dialogOverlay = new ContentDialogOverlay(ContentDialogTheme.Informational)
            {
                Title = Locale.Lang!._Misc!.ImageCropperTitle,
                Content = parentGrid,
                SecondaryButtonText = Locale.Lang._Misc.Cancel,
                PrimaryButtonText = Locale.Lang._Misc.OkayHappy,
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                XamlRoot = (InnerLauncherConfig.m_window as MainWindow)?.Content!.XamlRoot
            };

            LoadImageCropperDetached(filePath, imageCropper, parentGrid, dialogOverlay);

            ContentDialogResult dialogResult = await dialogOverlay.QueueAndSpawnDialog();
            if (dialogResult == ContentDialogResult.Secondary) return null;

            await using (FileStream cachedFileStream = new FileStream(cachedFilePath!, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                dialogOverlay.IsPrimaryButtonEnabled = false;
                dialogOverlay.IsSecondaryButtonEnabled = false;
                await imageCropper.SaveAsync(cachedFileStream.AsRandomAccessStream()!, BitmapFileFormat.Png);
            }

            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);
            return await GenerateCachedStream(cachedFileInfo, ToWidth, ToHeight, true);
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
                InputFileInfo.MoveTo(InputFileInfo.FullName + "_old", true);
                FileInfo newCachedFileInfo = new FileInfo(InputFileName);

                await using (FileStream newCachedFileStream = newCachedFileInfo.Create())
                await using (FileStream oldInputFileStream = InputFileInfo.OpenRead())
                    await ResizeImageStream(oldInputFileStream, newCachedFileStream, ToWidth, ToHeight);

                InputFileInfo.Delete();
                return newCachedFileInfo.OpenRead();
            }

            FileInfo cachedFileInfo = GetCacheFileInfo(InputFileInfo!.FullName + InputFileInfo.Length);
            bool isCachedFileExist = cachedFileInfo!.Exists && cachedFileInfo.Length > 1 << 15;
            if (isCachedFileExist) return cachedFileInfo.OpenRead();

            await using (FileStream cachedFileStream = cachedFileInfo.Create())
            await using (FileStream inputFileStream = InputFileInfo.OpenRead())
                await ResizeImageStream(inputFileStream, cachedFileStream, ToWidth, ToHeight);

            return cachedFileInfo.OpenRead();
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
    }
}
