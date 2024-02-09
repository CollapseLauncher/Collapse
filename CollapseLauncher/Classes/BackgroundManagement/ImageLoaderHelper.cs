using CollapseLauncher.CustomControls;
using CollapseLauncher.Dialogs;
using CommunityToolkit.WinUI.Animations;
using CommunityToolkit.WinUI.Controls;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using PhotoSauce.MagicScaler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

using Orientation = Microsoft.UI.Xaml.Controls.Orientation;

namespace CollapseLauncher
{
    internal static class ImageLoaderHelper
    {
        internal static Dictionary<string, string> SupportedImageFormats = new Dictionary<string, string> { { "Supported formats", "*.jpg;*.jpeg;*.jfif;*.png;*.bmp;*.tiff;*.tif;*.webp" } };

        internal static async Task<FileStream> LoadImage(string path, bool isUseImageCropper = false, bool overwriteCachedImage = false)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            double aspectRatioX = InnerLauncherConfig.m_actualMainFrameSize.Width;
            double aspectRatioY = InnerLauncherConfig.m_actualMainFrameSize.Height;
            double dpiScale = InnerLauncherConfig.m_appDPIScale;
            uint targetSourceImageWidth = (uint)(aspectRatioX * 1.5 * dpiScale);
            uint targetSourceImageHeight = (uint)(aspectRatioY * 1.5 * dpiScale);
            bool isCancelled = false, isError = false;

            if (!Directory.Exists(LauncherConfig.AppGameImgCachedFolder)) Directory.CreateDirectory(LauncherConfig.AppGameImgCachedFolder);

            FileStream resizedImageFileStream = null;

            try
            {
                FileInfo inputFileInfo = new FileInfo(path);
                FileInfo resizedFileInfo = GetCacheFileInfo(inputFileInfo.FullName + inputFileInfo.Length);
                if ((resizedFileInfo.Exists && resizedFileInfo.Length > 1 << 15) && !overwriteCachedImage)
                {
                    resizedImageFileStream = resizedFileInfo.OpenRead();
                    return resizedImageFileStream;
                }

                if (isUseImageCropper)
                {
                    resizedImageFileStream = await SpawnImageCropperDialog(path, resizedFileInfo.FullName, targetSourceImageWidth, targetSourceImageHeight);
                    if (resizedImageFileStream == null) return null;
                    return resizedImageFileStream;
                }

                resizedImageFileStream = await GenerateCachedStream(inputFileInfo, targetSourceImageWidth, targetSourceImageHeight, false);
            }
            catch
            {
                isError = true;
                throw;
            }
            finally
            {
                if ((isCancelled || isError) && resizedImageFileStream != null)
                {
                    await resizedImageFileStream.DisposeAsync();
                }
            }

            return resizedImageFileStream;
        }

        private static async Task<FileStream> SpawnImageCropperDialog(string filePath, string cachedFilePath, uint ToWidth, uint ToHeight)
        {
            Grid parentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                CornerRadius = new CornerRadius(12),
                // Margin = new Thickness(-23, -19, -23, -19)
            };

            ImageCropper imageCropper = new ImageCropper();
            imageCropper.AspectRatio = 113d / 66d;
            imageCropper.CropShape = CropShape.Rectangular;
            imageCropper.ThumbPlacement = ThumbPlacement.Corners;
            imageCropper.HorizontalAlignment = HorizontalAlignment.Stretch;
            imageCropper.VerticalAlignment = VerticalAlignment.Stretch;
            imageCropper.Opacity = 0;

            ContentDialogOverlay dialogOverlay = new ContentDialogOverlay(ContentDialogTheme.Informational)
            {
                Title = "Crop the image",
                Content = parentGrid,
                SecondaryButtonText = Locale.Lang._Misc.Cancel,
                PrimaryButtonText = Locale.Lang._Misc.OkayHappy,
                DefaultButton = ContentDialogButton.Primary,
                IsPrimaryButtonEnabled = false,
                XamlRoot = (InnerLauncherConfig.m_window as MainWindow).Content.XamlRoot
            };

            LoadImageCropperDetached(filePath, imageCropper, parentGrid, dialogOverlay);

            ContentDialogResult dialogResult = await dialogOverlay.QueueAndSpawnDialog();
            if (dialogResult == ContentDialogResult.Secondary) return null;

            using (FileStream cachedFileStream = File.Create(cachedFilePath))
            {
                dialogOverlay.IsPrimaryButtonEnabled = false;
                dialogOverlay.IsSecondaryButtonEnabled = false;
                await imageCropper.SaveAsync(cachedFileStream.AsRandomAccessStream(), BitmapFileFormat.Png, false);
            }

            imageCropper = null;
            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();

            FileInfo cachedFileInfo = new FileInfo(cachedFilePath);
            return await GenerateCachedStream(cachedFileInfo, ToWidth, ToHeight, true);
        }

        private static async void LoadImageCropperDetached(string filePath, ImageCropper imageCropper, Grid parentGrid, ContentDialogOverlay dialogOverlay)
        {
            StackPanel loadingMsgPanel = new StackPanel
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

            parentGrid.AddElementToGridRowColumn(imageCropper, 0, 0);
            parentGrid.AddElementToGridRowColumn(loadingMsgPanel, 0, 0);

            StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
            await imageCropper.LoadImageFromFile(file);

            GC.WaitForPendingFinalizers();
            GC.WaitForFullGCComplete();

            Storyboard storyboardAnim = new Storyboard();
            DoubleAnimation loadingMsgPanelAnim = loadingMsgPanel.CreateDoubleAnimation("Opacity", 0, 1, null, TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
            DoubleAnimation imageCropperAnim = imageCropper.CreateDoubleAnimation("Opacity", 1, 0, null, TimeSpan.FromMilliseconds(500), EasingType.Cubic.ToEasingFunction());
            storyboardAnim.Children.Add(loadingMsgPanelAnim);
            storyboardAnim.Children.Add(imageCropperAnim);
            storyboardAnim.Begin();

            dialogOverlay.IsPrimaryButtonEnabled = true;
        }

        private static async Task<FileStream> GenerateCachedStream(FileInfo InputFileInfo, uint ToWidth, uint ToHeight, bool isFromCropProcess = false)
        {
            FileStream cachedFileStream;
            if (isFromCropProcess)
            {
                string InputFileName = InputFileInfo.FullName;
                InputFileInfo.MoveTo(InputFileInfo.FullName + "_old", true);
                FileInfo newCachedFileInfo = new FileInfo(InputFileName);

                cachedFileStream = newCachedFileInfo.Create();
                using (FileStream oldInputFileStream = InputFileInfo.OpenRead())
                {
                    await ResizeImageStream(oldInputFileStream, cachedFileStream, ToWidth, ToHeight);
                    cachedFileStream.Position = 0;
                }
                InputFileInfo.Delete();
                return cachedFileStream;
            }

            FileInfo cachedFileInfo = GetCacheFileInfo(InputFileInfo.FullName + InputFileInfo.Length);
            bool isCachedFileExist = cachedFileInfo.Exists && cachedFileInfo.Length > 1 << 15;
            if (isCachedFileExist) return cachedFileInfo.OpenRead();

            cachedFileStream = cachedFileInfo.Create();
            using (FileStream inputFileStream = InputFileInfo.OpenRead())
            {
                await ResizeImageStream(inputFileStream, cachedFileStream, ToWidth, ToHeight);
                cachedFileStream.Position = 0;
            }
            return cachedFileStream;
        }

        internal static FileInfo GetCacheFileInfo(string filePath)
        {
            string cachedFileHash = ConverterTool.BytesToCRC32Simple(filePath);
            string cachedFilePath = Path.Combine(LauncherConfig.AppGameImgCachedFolder, cachedFileHash);
            return new FileInfo(cachedFilePath);
        }

        private static async Task ResizeImageStream(Stream input, Stream output, uint ToWidth, uint ToHeight)
        {
            ProcessImageSettings settings = new ProcessImageSettings
            {
                Width = (int)ToWidth,
                Height = (int)ToHeight,
                HybridMode = HybridScaleMode.Off,
                Interpolation = InterpolationSettings.CubicSmoother
            };

            await Task.Run(() => MagicImageProcessor.ProcessImage(input, output, settings));
        }
    }
}
