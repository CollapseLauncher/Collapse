using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using DImage = System.Drawing.Image;

namespace CollapseLauncher.Classes.Helper.Image
{
    internal static class ImageConverterHelper
    {
        const int MaxIconSize = 256;

        public static Icon ConvertToIcon(DImage image)
        {
            using var bitmap = ResizeToIconSize(image);
            if (bitmap == null || bitmap.Width > MaxIconSize || bitmap.Height > MaxIconSize)
                throw new Exception("Failed to resize image for icon conversion.");

            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();

                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // ICONDIR structure (6 bytes)
                // 0-1 reserved (0), 2-3 type (1 for icons), 4-5 count
                bw.Write((ushort)0);     // Reserved
                bw.Write((ushort)1);     // Image type (1 = icon)
                bw.Write((ushort)1);     // Number of images


                // ICONDIRENTRY (16 bytes)
                bw.Write((byte)bitmap.Width);  // width
                bw.Write((byte)bitmap.Height); // height
                bw.Write((byte)0);      // Color palette (0 = no palette)
                bw.Write((byte)0);      // Reserved
                bw.Write((ushort)0);    // Color planes
                bw.Write((ushort)32);    // Bits per pixel

                uint bytesInRes = (uint)bytes.Length;
                uint imageOffset = 6 + 16;

                bw.Write(bytesInRes);
                bw.Write(imageOffset);

                bw.Write(bytes);

                bw.Flush();
                ms.Position = 0;

                return new Icon(ms);
            }
        }

        private static Bitmap ResizeToIconSize(DImage sourceImage)
        {
            if (sourceImage.Width == MaxIconSize && sourceImage.Height == MaxIconSize)
                return new Bitmap(sourceImage);

            float scaleFactor = Math.Min(
                (float)MaxIconSize / sourceImage.Width,
                (float)MaxIconSize / sourceImage.Height
            );

            int scaledWidth = (int)Math.Round(sourceImage.Width * scaleFactor);
            int scaledHeight = (int)Math.Round(sourceImage.Height * scaleFactor);

            var bitmap = new Bitmap(scaledWidth, scaledHeight, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.DrawImage(sourceImage, 0, 0, scaledWidth, scaledHeight);
            }

            return bitmap;
        }
    }
}
