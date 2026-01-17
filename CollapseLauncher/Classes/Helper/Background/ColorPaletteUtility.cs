using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Image;
using ColorThiefDotNet;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.EncTool.Hashes;
using Hi3Helper.SentryHelper;
using Hi3Helper.Shared.Region;
using Microsoft.UI.Xaml;
using PhotoSauce.MagicScaler;
using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using static Hi3Helper.Logger;
using WColor = Windows.UI.Color;
// ReSharper disable SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault

#nullable enable
namespace CollapseLauncher.Helper.Background
{
    internal struct BitmapInputStruct
    {
        internal IntPtr Buffer;
        internal int    Width;
        internal int    Height;
        internal int    Channel;
    }

    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    internal static class ColorPaletteUtility
    {
        internal static async Task ApplyAccentColor(FrameworkElement element,
                                                    Stream           stream,
                                                    string           filePath,
                                                    bool             isImageLoadForFirstTime,
                                                    bool             isContinuousGeneration)
        {
            stream.Position = 0;
            ImageFileInfo           fileInfo  = ImageFileInfo.Load(stream);
            ImageFileInfo.FrameInfo frameInfo = fileInfo.Frames[0];

            stream.Position = 0;

            int bitmapChannelCount = frameInfo.HasAlpha ? 4 : 3;
            int bufferSize = frameInfo.Width * frameInfo.Height * bitmapChannelCount;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);

            try
            {
                using MemoryStream   bufferStream = new(buffer);
                ProcessImageSettings settings     = new();

                MagicImageProcessor.ProcessImage(stream, bufferStream, ProcessImageSettings.Default);

                Span<byte> bufferSpan = buffer.AsSpan(54, (int)bufferStream.Position - 54); // 54 is the size of BMP header

                BitmapInputStruct bitmapInputStruct = new()
                {
                    Buffer  = AsPointer(bufferSpan),
                    Width   = frameInfo.Width,
                    Height  = frameInfo.Height,
                    Channel = bitmapChannelCount
                };

                await ApplyAccentColor(element,
                                       bitmapInputStruct,
                                       filePath,
                                       isImageLoadForFirstTime,
                                       isContinuousGeneration);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return;

            static unsafe nint AsPointer(Span<byte> span) => (nint)Unsafe.AsPointer(ref MemoryMarshal.AsRef<byte>(span));
        }

        internal static async Task ApplyAccentColor<T>(T                 page,
                                                       BitmapInputStruct bitmapInput,
                                                       string            bitmapPath,
                                                       bool              forceCreateNewCache    = false,
                                                       bool              isContinuousGeneration = false)
            where T : FrameworkElement
        {
            bool isLight = InnerLauncherConfig.IsAppThemeLight;
            WColor[] colors = await TryGetCachedPalette(bitmapInput, isLight, bitmapPath, forceCreateNewCache,
                                                        isContinuousGeneration);
            SetColorPalette(page, colors[0]);
        }

        private static async ValueTask<WColor[]> TryGetCachedPalette(BitmapInputStruct bitmapInput, bool isLight,
                                                                     string bitmapPath, bool forceCreateNewCache,
                                                                     bool isContinuousGeneration)
        {
            if (isContinuousGeneration)
            {
                return await TryGenerateNewCachedPalette(bitmapInput, isLight, null);
            }

            string cachedPalettePath = bitmapPath + $".palette{(isLight ? "Light" : "Dark")}";
            string cachedFileHash    = HexTool.BytesToHexUnsafe(HashUtility<Crc32>.Shared.GetHashFromString(cachedPalettePath))!;
            cachedPalettePath = Path.Combine(LauncherConfig.AppGameImgCachedFolder, cachedFileHash);

            if (!File.Exists(cachedPalettePath) || forceCreateNewCache)
            {
                await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
            }

            byte[] data = await File.ReadAllBytesAsync(cachedPalettePath);
            if (!ConverterTool.TryDeserializeStruct(data, 4, out WColor[] output))
            {
                await TryGenerateNewCachedPalette(bitmapInput, isLight, cachedPalettePath);
            }

            return output;
        }

        private static void SetColorPalette<T>(T page, WColor? palette = null)
            where T : FrameworkElement
        {
            if (!palette.HasValue) return;

            WColor maskTransparentPalette = new()
            {
                A = 0,
                R = palette.Value.R,
                G = palette.Value.G,
                B = palette.Value.B
            };
            maskTransparentPalette = ChangeColorBrightness(maskTransparentPalette, !InnerLauncherConfig.IsAppThemeLight ? -0.75f : 0.85f);

            WColor maskPalette = new()
            {
                A = 255,
                R = palette.Value.R,
                G = palette.Value.G,
                B = palette.Value.B
            };
            maskPalette = ChangeColorBrightness(maskPalette, !InnerLauncherConfig.IsAppThemeLight ? -0.75f : 0.85f);

            page.ChangeAccentColor(palette.Value, maskPalette, maskTransparentPalette);
        }

        // Credit:
        // https://gist.github.com/zihotki/09fc41d52981fb6f93a81ebf20b35cd5
        private static WColor ChangeColorBrightness(WColor color, float correctionFactor)
        {
            float red   = color.R;
            float green = color.G;
            float blue  = color.B;

            if (correctionFactor < 0)
            {
                correctionFactor =  1 + correctionFactor;
                red              *= correctionFactor;
                green            *= correctionFactor;
                blue             *= correctionFactor;
            }
            else
            {
                red   = (255 - red) * correctionFactor + red;
                green = (255 - green) * correctionFactor + green;
                blue  = (255 - blue) * correctionFactor + blue;
            }

            return WColor.FromArgb(color.A, (byte)red, (byte)green, (byte)blue);
        }

        private static async ValueTask<WColor[]> TryGenerateNewCachedPalette(BitmapInputStruct bitmapInput,
                                                                             bool isLight,
                                                                             string? cachedPalettePath)
        {
            WColor[] colors = [await GetPaletteList(bitmapInput, isLight)];
            colors = ConverterTool.EnsureLengthCopyLast(colors, 4);

            if (string.IsNullOrEmpty(cachedPalettePath))
            {
                return colors;
            }

            byte[] buffer = new byte[1 << 10];

            string? cachedPaletteDirPath = Path.GetDirectoryName(cachedPalettePath);
            if (!Directory.Exists(cachedPaletteDirPath))
            {
                Directory.CreateDirectory(cachedPaletteDirPath ?? "");
            }

            if (!ConverterTool.TrySerializeStruct(buffer, out int read, colors))
            {
                byte defVal = (byte)(isLight ? 80 : 255);
                WColor defColor =
                    DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, defVal, defVal, defVal), 1));
                return [defColor, defColor, defColor, defColor];
            }

            await File.WriteAllBytesAsync(cachedPalettePath, buffer[..read]);

            return colors;
        }

        private static async Task<WColor> GetPaletteList(BitmapInputStruct bitmapInput, bool isLight)
        {
            byte defVal = (byte)(isLight ? 80 : 255);

            try
            {
                LumaUtils.DarkThreshold        = isLight ? 200f : 400f;
                LumaUtils.IgnoreWhiteThreshold = isLight ? 900f : 800f;
                // LumaUtils.ChangeCoeToBT601();

                QuantizedColor averageColor = await Task.Run(() =>
                                                                 ColorThief.GetColor(bitmapInput.Buffer,
                                                                     bitmapInput.Channel, bitmapInput.Width,
                                                                     bitmapInput.Height, 1))
                                                        ;

                WColor wColor        = DrawingColorToColor(averageColor);
                WColor adjustedColor = wColor.SetSaturation(1.2);
                adjustedColor = isLight ? adjustedColor.GetDarkColor() : adjustedColor.GetLightColor();

                return adjustedColor;
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Warning, true);
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
            }

            WColor defColor = DrawingColorToColor(new QuantizedColor(Color.FromArgb(255, defVal, defVal, defVal), 1));
            return defColor;
        }

        private static WColor DrawingColorToColor(QuantizedColor i)
        {
            return new WColor { R = i.Color.R, G = i.Color.G, B = i.Color.B, A = i.Color.A };
        }
    }
}