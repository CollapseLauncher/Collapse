using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.WindowSize
{
    internal static class WindowSize
    {
        internal static readonly Dictionary<string, WindowSizeProp> WindowSizeProfiles = new Dictionary<string, WindowSizeProp>
        {
            {
                "Normal",
                new WindowSizeProp()
                {
                    WindowBounds = new Size(1243, 726),
                    EventPostCarouselBounds = new Size(399, 185),
                    PostPanelBounds = new Size(399, 134),
                    PostPanelBottomMargin = new Thickness(0, 0, 0, 52),
                    PostPanelPaimonHeight = 138,
                    PostPanelPaimonMargin = new Thickness(0, -48, -32, 0),
                    PostPanelPaimonInnerMargin = new Thickness(0, 0, 0, 48),
                    PostPanelPaimonTextMargin = new Thickness(0, 0, 142, 0),
                    BannerIconWidth = 136,
                    BannerIconMargin = new Thickness(0, 0, 80, 70)
                }
            },
            {
                "Small",
                new WindowSizeProp()
                {
                    WindowBounds = new Size(1028, 622),
                    EventPostCarouselBounds = new Size(336, 156),
                    PostPanelBounds = new Size(336, 118),
                    PostPanelBottomMargin = new Thickness(0, 0, 0, 38),
                    PostPanelPaimonHeight = 138,
                    PostPanelPaimonMargin = new Thickness(0, -48, -32, 0),
                    PostPanelPaimonInnerMargin = new Thickness(0, 0, 0, 48),
                    PostPanelPaimonTextMargin = new Thickness(0, 0, 142, 0),
                    BannerIconWidth = 136,
                    BannerIconMargin = new Thickness(0,0,70,46)
                }
            }
        };

        internal static string CurrentWindowSizeName
        {
            get
            {
                string val = GetAppConfigValue("WindowSizeProfile").ToString();
                if (!WindowSizeProfiles.ContainsKey(val))
                {
                    return WindowSizeProfiles.Keys.FirstOrDefault();
                }

                return val;
            }
            set
            {
                SetAppConfigValue("WindowSizeProfile", value);
                (m_window as MainWindow).SetWindowSize(m_windowHandle, CurrentWindowSize.WindowBounds.Width, CurrentWindowSize.WindowBounds.Height);
            }
        }
        internal static WindowSizeProp CurrentWindowSize { get => WindowSizeProfiles[CurrentWindowSizeName]; }
    }

    internal class WindowSizeProp
    {
        public Size WindowBounds { get; set; }
        public Size EventPostCarouselBounds { get; set; }
        public Size PostPanelBounds { get; set; }
        public Thickness PostPanelBottomMargin { get; set; }
        public int PostPanelPaimonHeight { get; set; }
        public Thickness PostPanelPaimonMargin { get; set; }
        public Thickness PostPanelPaimonInnerMargin { get; set; }
        public Thickness PostPanelPaimonTextMargin { get; set; }
        public int BannerIconWidth { get; set; }
        public Thickness BannerIconMargin { get; set; }
    }
}
