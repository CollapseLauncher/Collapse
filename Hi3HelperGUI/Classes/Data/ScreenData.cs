﻿using System;
using System.Collections.Generic;
using System.Linq;

using static Hi3HelperGUI.Logger;

namespace Hi3HelperGUI.Screen
{
    public class ScreenProp : ScreenInterop
    {
        internal protected static DEVMODE devMode;
        public static List<ScreenResolution> screenResolutions;
        public static void InitScreenResolution()
        {
            devMode = new();
            screenResolutions = new List<ScreenResolution>();

            int i = 0;

            i++;

            while (EnumDisplaySettings(null, i, ref devMode))
            {
                if (screenResolutions.Count == 0)
                    screenResolutions.Add(new ScreenResolution { Width = devMode.dmPelsWidth, Height = devMode.dmPelsHeight });
                else if (!(screenResolutions[screenResolutions.Count - 1].Width == devMode.dmPelsWidth
                    && screenResolutions[screenResolutions.Count - 1].Height == devMode.dmPelsHeight))
                    screenResolutions.Add(new ScreenResolution { Width = devMode.dmPelsWidth, Height = devMode.dmPelsHeight });

                i++;
            }

            LogWriteLine($"Supported Screen Resolutions: {string.Join(", ", screenResolutions.Select(p => $"{p.Width}x{p.Height}"))}", LogType.Scheme);
        }

        public class ScreenResolution
        {
            public uint Width { get; set; } = 0;
            public uint Height { get; set; } = 0;
        }
    }
}