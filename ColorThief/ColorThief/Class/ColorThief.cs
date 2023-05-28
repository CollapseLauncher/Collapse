using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;

namespace ColorThiefDotNet
{
    public partial class ColorThief
    {
        public const int DefaultColorCount = 5;
        public const int DefaultQuality = 10;
        public const bool DefaultIgnoreWhite = true;
        public const int ColorDepth = 3;

        /// <summary>
        ///     Use the median cut algorithm to cluster similar colors.
        /// </summary>
        /// <param name="pixelArray">Pixel array.</param>
        /// <param name="colorCount">The color count.</param>
        /// <returns></returns>
        private CMap GetColorMap(Span<byte> pixelArray, int colorCount)
        {
            // Send array to quantize function which clusters values using median
            // cut algorithm

            if (colorCount > 0)
            {
                --colorCount;
            }

            return Mmcq.Quantize(pixelArray, colorCount);
        }

        [SupportedOSPlatform("windows")]
        private unsafe Span<byte> GetPixels(Bitmap sourceImage, int quality, bool ignoreWhite)
        {

            int numUsedPixels = 0;
            byte[] pixelArray = new byte[sourceImage.Width * sourceImage.Height * 3];


            var data = sourceImage.LockBits(new Rectangle(new Point(), sourceImage.Size), ImageLockMode.ReadOnly, sourceImage.PixelFormat);
            if (data.PixelFormat is System.Drawing.Imaging.PixelFormat.Format24bppRgb)
            {
                var span = new Span<byte>((void*)data.Scan0, data.Stride * data.Height * 3);
                for (int i = 0; i < data.Height; i++)
                {
                    for (int j = 0; j < data.Width; j++)
                    {
                        byte b = span[data.Stride * i + 3 * j + 0];
                        byte g = span[data.Stride * i + 3 * j + 1];
                        byte r = span[data.Stride * i + 3 * j + 2];
                        if (!(ignoreWhite && r > 250 && g > 250 && b > 250))
                        {
                            pixelArray[numUsedPixels * 3 + 0] = r;
                            pixelArray[numUsedPixels * 3 + 1] = g;
                            pixelArray[numUsedPixels * 3 + 2] = b;
                            numUsedPixels++;
                        }
                    }
                }
            }
            if (data.PixelFormat is System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                var span = new Span<byte>((void*)data.Scan0, data.Stride * data.Height * 4);
                for (int i = 0; i < data.Height; i++)
                {
                    for (int j = 0; j < data.Width; j++)
                    {
                        byte b = span[data.Stride * i + 4 * j + 0];
                        byte g = span[data.Stride * i + 4 * j + 1];
                        byte r = span[data.Stride * i + 4 * j + 2];
                        if (!(ignoreWhite && r > 250 && g > 250 && b > 250))
                        {
                            pixelArray[numUsedPixels * 3 + 0] = r;
                            pixelArray[numUsedPixels * 3 + 1] = g;
                            pixelArray[numUsedPixels * 3 + 2] = b;
                            numUsedPixels++;
                        }
                    }
                }
            }

            sourceImage.UnlockBits(data);

            return pixelArray.AsSpan().Slice(0, numUsedPixels * 3);

        }
    }
}