﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

using PInvoke;

using static Hi3Helper.Logger;

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

            // LogWriteLine($"Supported Screen Resolutions: {string.Join(", ", screenResolutions.Select(p => $"{p.Width}x{p.Height}"))}", LogType.Scheme);
        }

        public static Size GetScreenSize() => new Size
        {
            Width = User32.GetSystemMetrics(User32.SystemMetric.SM_CXSCREEN),
            Height = User32.GetSystemMetrics(User32.SystemMetric.SM_CYSCREEN)
        };
    }

    public struct ScreenResolution
    {
        public uint Width;
        public uint Height;

        public override string ToString() => $"{Width}x{Height}";
    }
}