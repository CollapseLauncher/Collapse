using System;
using System.Runtime.InteropServices;

using Color = System.Drawing.Color;
using WColor = Windows.UI.Color;

/*
 * The code included here was mostly coming from Windows Forms Code
 */

namespace CollapseLauncher.Helper.Image
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct HLSColor
    {
        private const int ShadowAdj = -333;
        private const int HilightAdj = 500;
        private const int WatermarkAdj = -50;
        private const int Range = 240;
        private const int HLSMax = 240;
        private const int RGBMax = 0xff;
        private const int Undefined = 160;
        internal int hue;
        internal int saturation;
        internal int luminosity;

        public static HLSColor CreateFromWindowsColor(WColor color) => new HLSColor(Color.FromArgb(color.A, color.R, color.G, color.B));
        public HLSColor(Color color)
        {
            int r = color.R;
            int g = color.G;
            int b = color.B;
            int num4 = Math.Max(Math.Max(r, g), b);
            int num5 = Math.Min(Math.Min(r, g), b);
            int num6 = num4 + num5;
            this.luminosity = ((num6 * 240) + 0xff) / 510;
            int num7 = num4 - num5;
            if (num7 == 0)
            {
                this.saturation = 0;
                this.hue = 160;
            }
            else
            {
                if (this.luminosity <= 120)
                {
                    this.saturation = ((num7 * 240) + (num6 / 2)) / num6;
                }
                else
                {
                    this.saturation = ((num7 * 240) + ((510 - num6) / 2)) / (510 - num6);
                }
                int num8 = (((num4 - r) * 40) + (num7 / 2)) / num7;
                int num9 = (((num4 - g) * 40) + (num7 / 2)) / num7;
                int num10 = (((num4 - b) * 40) + (num7 / 2)) / num7;
                if (r == num4)
                {
                    this.hue = num10 - num9;
                }
                else if (g == num4)
                {
                    this.hue = (80 + num8) - num10;
                }
                else
                {
                    this.hue = (160 + num9) - num8;
                }
                if (this.hue < 0)
                {
                    this.hue += 240;
                }
                if (this.hue > 240)
                {
                    this.hue -= 240;
                }
            }
        }

        public Color Lighter(float percLighter)
        {
            int luminosity = this.luminosity;
            int num5 = this.NewLuma(500, true);
            return this.ColorFromHLS(this.hue, luminosity + ((int)((num5 - luminosity) * percLighter)), this.saturation);
        }

        public Color Darker(float percDarker)
        {
            int num4 = 0;
            int num5 = this.NewLuma(-333, true);
            return this.ColorFromHLS(this.hue, num5 - ((int)((num5 - num4) * percDarker)), this.saturation);
        }

        private int NewLuma(int n, bool scale)
        {
            return this.NewLuma(this.luminosity, n, scale);
        }

        private int NewLuma(int luminosity, int n, bool scale)
        {
            if (n == 0)
            {
                return luminosity;
            }
            if (scale)
            {
                if (n > 0)
                {
                    return (int)(((luminosity * (0x3e8 - n)) + (0xf1L * n)) / 0x3e8L);
                }
                return ((luminosity * (n + 0x3e8)) / 0x3e8);
            }
            int num = luminosity;
            num += (int)((n * 240L) / 0x3e8L);
            if (num < 0)
            {
                num = 0;
            }
            if (num > 240)
            {
                num = 240;
            }
            return num;
        }

        internal Color ColorFromHLS(int hue, int luminosity, int saturation)
        {
            byte num;
            byte num2;
            byte num3;
            if (saturation == 0)
            {
                num = num2 = num3 = (byte)((luminosity * 0xff) / 240);
                if (hue == 160)
                {
                }
            }
            else
            {
                int num5;
                if (luminosity <= 120)
                {
                    num5 = ((luminosity * (240 + saturation)) + 120) / 240;
                }
                else
                {
                    num5 = (luminosity + saturation) - (((luminosity * saturation) + 120) / 240);
                }
                int num4 = (2 * luminosity) - num5;
                num = (byte)(((this.HueToRGB(num4, num5, hue + 80) * 0xff) + 120) / 240);
                num2 = (byte)(((this.HueToRGB(num4, num5, hue) * 0xff) + 120) / 240);
                num3 = (byte)(((this.HueToRGB(num4, num5, hue - 80) * 0xff) + 120) / 240);
            }
            return Color.FromArgb(num, num2, num3);
        }

        private int HueToRGB(int n1, int n2, int hue)
        {
            if (hue < 0)
            {
                hue += 240;
            }
            if (hue > 240)
            {
                hue -= 240;
            }
            if (hue < 40)
            {
                return (n1 + ((((n2 - n1) * hue) + 20) / 40));
            }
            if (hue < 120)
            {
                return n2;
            }
            if (hue < 160)
            {
                return (n1 + ((((n2 - n1) * (160 - hue)) + 20) / 40));
            }
            return n1;
        }
    }

    internal static class ColorHelper
    {
        internal static WColor GetDarkColor(this WColor baseColor)
        {
            HLSColor color = HLSColor.CreateFromWindowsColor(baseColor);
            return color.Darker(0.3f).ToWColor();
        }

        internal static WColor GetLightColor(this WColor baseColor)
        {
            HLSColor color = HLSColor.CreateFromWindowsColor(baseColor);
            return color.Lighter(1f).ToWColor();
        }

        internal static WColor SetSaturation(this WColor baseColor, double saturation)
        {
            HLSColor color = HLSColor.CreateFromWindowsColor(baseColor);
            Color colorFromHLS = color.ColorFromHLS(color.hue, color.luminosity, (int)Math.Round(color.saturation * saturation, 2));
            return WColor.FromArgb(colorFromHLS.A, colorFromHLS.R, colorFromHLS.G, colorFromHLS.B);
        }

        private static WColor ToWColor(this Color color) => WColor.FromArgb(color.A, color.R, color.G, color.B);
    }
}
