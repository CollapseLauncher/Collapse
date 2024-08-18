using System.Collections.Generic;
using System.Drawing;
using static Hi3Helper.InvokeProp;

namespace Hi3Helper.Screen
{
    public class ScreenProp : ScreenInterop
    {
        internal protected static DEVMODE devMode;
        public static List<Size> screenResolutions;
        public static Size currentResolution { get => GetScreenSize(); }
        public static void InitScreenResolution()
        {
            devMode = new DEVMODE();
            screenResolutions = new List<Size>();

            for (int i = 0; EnumDisplaySettings(null, i, ref devMode); i++)
            {
                if (screenResolutions.Count == 0)
                    screenResolutions.Add(new Size { Width = (int)devMode.dmPelsWidth, Height = (int)devMode.dmPelsHeight });
                else if (!(screenResolutions[^1].Width == devMode.dmPelsWidth && screenResolutions[^1].Height == devMode.dmPelsHeight))
                    screenResolutions.Add(new Size { Width = (int)devMode.dmPelsWidth, Height = (int)devMode.dmPelsHeight });
            }

            // Some corner case
            if (screenResolutions.Count == 0)
            {
                screenResolutions.Add(GetScreenSize());
            }
        }

        public static Size GetScreenSize() => new Size
        {
            Width = GetSystemMetrics(SystemMetric.SM_CXSCREEN),
            Height = GetSystemMetrics(SystemMetric.SM_CYSCREEN)
        };

        public static int GetMaxHeight()
        {
            devMode = new DEVMODE();
            int maxHeight = 0;
            
            for (int i = 0; EnumDisplaySettings(null, i, ref devMode); i++)
            {
                if (devMode.dmPelsHeight > maxHeight) maxHeight = (int)devMode.dmPelsHeight;
            }

            return maxHeight;
        }
    }
}
