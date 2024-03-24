using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Image;
using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Shared.ClassStruct;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using static Hi3Helper.Logger;
using WColor = Windows.UI.Color;

#nullable enable
namespace CollapseLauncher.Helper.Background
{
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "PossibleNullReferenceException")]
    [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("ReSharper", "AssignNullToNotNullAttribute")]
    internal static class ColorPaletteUtility
    {
        internal static async Task ApplyAccentColor<T>(T page, Bitmap bitmapInput, string bitmapPath, bool forceCreateNewCache = false)
            where T : FrameworkElement
        {
            bool IsLight = InnerLauncherConfig.IsAppThemeLight;

            WColor[] _colors = await TryGetCachedPalette(bitmapInput, IsLight, bitmapPath, forceCreateNewCache);

            SetColorPalette(page, _colors);
        }

        internal static void SetColorPalette<T>(T page, WColor[]? palette = null)
            where T : FrameworkElement
        {
            if (palette == null || palette.Length < 2)
                palette = ConverterTool.EnsureLengthCopyLast(new WColor[] { UIElementExtensions.GetApplicationResource<WColor>("TemplateAccentColor") }, 2);

            string dictColorNameTheme = InnerLauncherConfig.IsAppThemeLight ? "Dark" : "Light";
            UIElementExtensions.SetApplicationResource("SystemAccentColor", palette[0]);
            UIElementExtensions.SetApplicationResource($"SystemAccentColor{dictColorNameTheme}1", palette[0]);
            UIElementExtensions.SetApplicationResource($"SystemAccentColor{dictColorNameTheme}2", palette[0]);
            UIElementExtensions.SetApplicationResource($"SystemAccentColor{dictColorNameTheme}3", palette[0]);
            UIElementExtensions.SetApplicationResource("AccentColor", new SolidColorBrush(palette[0]));

            ReloadPageTheme(page, ConvertAppThemeToElementTheme(InnerLauncherConfig.CurrentAppTheme));
        }

        public static ElementTheme ConvertAppThemeToElementTheme(AppThemeMode Theme)
        {
            switch (Theme)
            {
                default:
                    return ElementTheme.Default;
                case AppThemeMode.Dark:
                    return ElementTheme.Dark;
                case AppThemeMode.Light:
                    return ElementTheme.Light;
            }
        }

        private static async ValueTask<WColor[]> TryGetCachedPalette(Bitmap bitmapInput, bool isLight, string bitmapPath, bool forceCreateNewCache)
        {
            string cachedPalettePath = bitmapPath + $".palette{(isLight ? "Light" : "Dark")}";
            string cachedFileHash = ConverterTool.BytesToCRC32Simple(cachedPalettePath);
            cachedPalettePath = Path.Combine(LauncherConfig.AppGameImgCachedFolder, cachedFileHash);
            if (File.Exists(cachedPalettePath) && !forceCreateNewCache)
            {
                byte[] data = await File.ReadAllBytesAsync(cachedPalettePath);
                if (!ConverterTool.TryDeserializeStruct(data, 4, out WColor[] output))
                {
                    return await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
                }

                return output;
            }

            return await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
        }

        private static async ValueTask<WColor[]> TryGenerateNewCachedPalette(Bitmap bitmapInput, bool IsLight, string cachedPalettePath)
        {
            byte[] buffer = new byte[1 << 10];

            string? cachedPaletteDirPath = Path.GetDirectoryName(cachedPalettePath);
            if (!Directory.Exists(cachedPaletteDirPath)) Directory.CreateDirectory(cachedPaletteDirPath ?? "");

            WColor[] _colors = new WColor[] { await GetPaletteList(bitmapInput, IsLight) };
            _colors = ConverterTool.EnsureLengthCopyLast(_colors, 4);

            if (!ConverterTool.TrySerializeStruct(_colors, buffer, out int read))
            {
                byte DefVal = (byte)(IsLight ? 80 : 255);
                WColor defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
                return new WColor[] { defColor, defColor, defColor, defColor };
            }

            await File.WriteAllBytesAsync(cachedPalettePath, buffer[..read]);
            return _colors;
        }

        internal static void ReloadPageTheme(FrameworkElement page, AppThemeMode startTheme)
            => ReloadPageTheme(page, ConvertAppThemeToElementTheme(startTheme));
        internal static void ReloadPageTheme(FrameworkElement page, ElementTheme startTheme)
        {
            bool IsComplete = false;
            while (!IsComplete)
            {
                try
                {
                    if (page.RequestedTheme == ElementTheme.Dark)
                        page.RequestedTheme = ElementTheme.Light;
                    else if (page.RequestedTheme == ElementTheme.Light)
                        page.RequestedTheme = ElementTheme.Default;
                    else if (page.RequestedTheme == ElementTheme.Default)
                        page.RequestedTheme = ElementTheme.Dark;

                    if (page.RequestedTheme != startTheme)
                        ReloadPageTheme(page, startTheme);
                    IsComplete = true;
                }
                catch (Exception)
                {

                }
            }
        }

        private static async Task<WColor> GetPaletteList(Bitmap bitmapinput, bool IsLight)
        {
            byte DefVal = (byte)(IsLight ? 80 : 255);

            try
            {
                LumaUtils.DarkThreshold = IsLight ? 200f : 400f;
                LumaUtils.IgnoreWhiteThreshold = IsLight ? 900f : 800f;
                LumaUtils.ChangeCoeToBT601();

                int bitmapChannelCount = bitmapinput.PixelFormat switch
                {
                    PixelFormat.Format32bppRgb => 4,
                    PixelFormat.Format32bppArgb => 4,
                    PixelFormat.Format24bppRgb => 3,
                    _ => throw new NotSupportedException($"Pixel format of the image: {bitmapinput.PixelFormat} is unsupported!")
                };

                BitmapData bitmapData = bitmapinput.LockBits(new Rectangle(new Point(), bitmapinput.Size), ImageLockMode.ReadOnly, bitmapinput.PixelFormat);
                int bitmapBufferLength = bitmapData.Width * bitmapData.Height * bitmapChannelCount;
                nint bitmapBufferPtr = bitmapData.Scan0;

                try
                {
                    QuantizedColor averageColor = await Task.Run(() =>
                        ColorThief.GetColor(bitmapBufferPtr, bitmapChannelCount, bitmapData.Width, bitmapData.Height, 1, true))
                        .ConfigureAwait(false);

                    WColor wColor = DrawingColorToColor(averageColor);
                    WColor adjustedColor = wColor.SetSaturation(1.5);
                    adjustedColor = IsLight ? adjustedColor.GetDarkColor() : adjustedColor.GetLightColor();

                    return adjustedColor;
                }
                finally
                {
                    bitmapinput.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Warning, true);
            }

            WColor defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, DefVal, DefVal, DefVal), 1));
            return defColor;
        }

        private static WColor DrawingColorToColor(QuantizedColor i) => new WColor { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };
    }
}
