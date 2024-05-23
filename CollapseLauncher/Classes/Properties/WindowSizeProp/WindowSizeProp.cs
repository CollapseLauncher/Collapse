using CollapseLauncher.Helper;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.WindowSize
{
    internal static class WindowSize
    {
        internal static
#if !DEBUG
            readonly
#endif
            Dictionary<string, WindowSizeProp> WindowSizeProfiles
#if DEBUG
            =>
#else
            =
#endif
            new Dictionary<string, WindowSizeProp>
        {
            {
                "Normal",
                new WindowSizeProp()
                {
                    WindowBounds               = new Size(1280, 720),
                    PostEventPanelScaleFactor  = 1.35f,
                    SidePanel1Width            = new GridLength(340, GridUnitType.Pixel),
                    EventPostCarouselBounds    = new Size(340,  158),
                    PostPanelBounds            = new Size(340,  120),
                    PostPanelBottomMargin      = new Thickness(0, 0, 0, 52),
                    PostPanelPaimonHeight      = 138,
                    PostPanelPaimonMargin      = new Thickness(0, -48, -32, 0),
                    PostPanelPaimonInnerMargin = new Thickness(0, 0,   0,   48),
                    PostPanelPaimonTextMargin  = new Thickness(0, 0,   142, 0),
                    BannerIconWidth            = 136,
                    BannerIconMargin           = new Thickness(0, 0, 94, 84),
                    SettingsPanelWidth         = 676
                }
            },
            {
                "Small",
                new WindowSizeProp()
                {
                    WindowBounds               = new Size(1024, 576),
                    PostEventPanelScaleFactor  = 1.25f,
                    SidePanel1Width            = new GridLength(280, GridUnitType.Pixel),
                    EventPostCarouselBounds    = new Size(280,  130),
                    PostPanelBounds            = new Size(280,  118),
                    PostPanelBottomMargin      = new Thickness(0, 0, 0, 48),
                    PostPanelPaimonHeight      = 138,
                    PostPanelPaimonMargin      = new Thickness(0, -48, -32, 0),
                    PostPanelPaimonInnerMargin = new Thickness(0, 0,   0,   48),
                    PostPanelPaimonTextMargin  = new Thickness(0, 0,   142, 0),
                    BannerIconWidth            = 100,
                    BannerIconMargin           = new Thickness(0, 0, 70, 52),
                    SettingsPanelWidth         = 464
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
                WindowUtility.SetWindowSize(CurrentWindowSize.WindowBounds.Width, CurrentWindowSize.WindowBounds.Height);
            }
        }
        internal static WindowSizeProp CurrentWindowSize { get => WindowSizeProfiles[CurrentWindowSizeName]; }
    }

    internal class WindowSizeProp
    {
        public Size       WindowBounds               { get; set; }
        public GridLength SidePanel1Width            { get; set; }
        public float      PostEventPanelScaleFactor  { get; set; }
        public Size       EventPostCarouselBounds    { get; set; }
        public Size       PostPanelBounds            { get; set; }
        public Thickness  PostPanelBottomMargin      { get; set; }
        public int        PostPanelPaimonHeight      { get; set; }
        public Thickness  PostPanelPaimonMargin      { get; set; }
        public Thickness  PostPanelPaimonInnerMargin { get; set; }
        public Thickness  PostPanelPaimonTextMargin  { get; set; }
        public int        BannerIconWidth            { get; set; }
        public Thickness  BannerIconMargin           { get; set; }
        public int        SettingsPanelWidth         { get; set; }
    }
}
