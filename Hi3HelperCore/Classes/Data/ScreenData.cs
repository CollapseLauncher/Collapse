using System.Collections.Generic;
using System.Drawing;
using static Hi3Helper.InvokeProp;

namespace Hi3Helper.Screen
{
    public class ScreenProp : ScreenInterop
    {
        internal protected static DEVMODE devMode;
        public static List<Size> screenResolutions;
        public static void InitScreenResolution()
        {
            devMode = new DEVMODE();
            screenResolutions = new List<Size>();

            int i = 0;

            i++;

            while (EnumDisplaySettings(null, i, ref devMode))
            {
                if (screenResolutions.Count == 0)
                    screenResolutions.Add(new Size { Width = (int)devMode.dmPelsWidth, Height = (int)devMode.dmPelsHeight });
                else if (!(screenResolutions[screenResolutions.Count - 1].Width == devMode.dmPelsWidth
                    && screenResolutions[screenResolutions.Count - 1].Height == devMode.dmPelsHeight))
                    screenResolutions.Add(new Size { Width = (int)devMode.dmPelsWidth, Height = (int)devMode.dmPelsHeight });

                i++;
            }
        }

        public static Size GetScreenSize() => new Size
        {
            Width = GetSystemMetrics((int)SystemMetric.SM_CXSCREEN),
            Height = GetSystemMetrics((int)SystemMetric.SM_CYSCREEN)
        };
    }
}