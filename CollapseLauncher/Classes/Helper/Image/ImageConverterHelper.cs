using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using DImage = System.Drawing.Image;

namespace CollapseLauncher.Classes.Helper.Image
{
    internal static class ImageConverterHelper
    {
        public static Icon ConvertToIcon(DImage image)
        {
            using var bmp32 = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);

            using (var g = Graphics.FromImage(bmp32))
            {
                g.DrawImage(image, new Rectangle(0, 0, bmp32.Width, bmp32.Height));
            }

            using (var stream = new MemoryStream())
            {
                bmp32.Save(stream, ImageFormat.Png);
                var bytes = stream.ToArray();

                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // ICONDIR structure (6 bytes)
                // 0-1 reserved (0), 2-3 type (1 for icons), 4-5 count
                bw.Write((ushort)0);     // Reserved
                bw.Write((ushort)1);     // Image type (1 = icon)
                bw.Write((ushort)1);     // Number of images


                int width = image.Width >= 256 ? 0 : image.Width;
                int height = image.Height >= 256 ? 0 : image.Height;

                // ICONDIRENTRY (16 bytes)
                bw.Write((byte)width);  // width
                bw.Write((byte)height); // height
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
    }
}
