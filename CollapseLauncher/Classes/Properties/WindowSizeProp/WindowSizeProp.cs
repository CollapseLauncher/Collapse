using CollapseLauncher.Helper;
using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming

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
            new()
            {
            {
                "Normal",
                new WindowSizeProp
                {
                    WindowBounds                 = new Size(1280, 720),
                    PostEventPanelScaleFactor    = 1.35f,
                    SidePanel1Width              = new GridLength(340, GridUnitType.Pixel),
                    EventPostCarouselBounds      = new Size(340, 158),
                    PostPanelBounds              = new Size(340, 84),
                    PostPanelBottomMargin        = new Thickness(0, 0, 0, 20),
                    PostPanelPaimonHeight        = 110,
                    PostPanelPaimonMargin        = new Thickness(0, -48, -56, 0),
                    PostPanelPaimonInnerMargin   = new Thickness(0, 0,   0,   0),
                    PostPanelPaimonTextMargin    = new Thickness(0, 0,   0,   18),
                    PostPanelPaimonTextSize      = 11,
                    BannerIconHeight             = 40,
                    BannerIconHeightHYP          = 40,
                    BannerIconMargin             = new Thickness(0,  0,   94, 84),
                    BannerIconMarginHYP          = new Thickness(48, 244, 0,  0),
                    BannerIconAlignHorizontal    = HorizontalAlignment.Right,
                    BannerIconAlignHorizontalHYP = HorizontalAlignment.Left,
                    BannerIconAlignVertical      = VerticalAlignment.Bottom,
                    BannerIconAlignVerticalHYP   = VerticalAlignment.Top,
                    SettingsPanelWidth           = 676
                }
            },
            {
                "Small",
                new WindowSizeProp
                {
                    WindowBounds                 = new Size(1024, 576),
                    PostEventPanelScaleFactor    = 1.25f,
                    SidePanel1Width              = new GridLength(280, GridUnitType.Pixel),
                    EventPostCarouselBounds      = new Size(280, 130),
                    PostPanelBounds              = new Size(280, 82),
                    PostPanelBottomMargin        = new Thickness(0, 0, 0, 12),
                    PostPanelPaimonHeight        = 110,
                    PostPanelPaimonMargin        = new Thickness(0, -48, -56, 0),
                    PostPanelPaimonInnerMargin   = new Thickness(0, 0,   0,   0),
                    PostPanelPaimonTextMargin    = new Thickness(0, 0,   0,   18),
                    PostPanelPaimonTextSize      = 11,
                    BannerIconHeight             = 32,
                    BannerIconHeightHYP          = 32,
                    BannerIconMargin             = new Thickness(0,  0,   70, 52),
                    BannerIconMarginHYP          = new Thickness(22, 184, 0,  0),
                    BannerIconAlignHorizontal    = HorizontalAlignment.Right,
                    BannerIconAlignHorizontalHYP = HorizontalAlignment.Left,
                    BannerIconAlignVertical      = VerticalAlignment.Bottom,
                    BannerIconAlignVerticalHYP   = VerticalAlignment.Top,
                    SettingsPanelWidth           = 464
                }
            }
        };

        internal static string CurrentWindowSizeName
        {
            get
            {
                string val = GetAppConfigValue("WindowSizeProfile").ToString();
                return !WindowSizeProfiles.ContainsKey(val ?? "Normal") ? WindowSizeProfiles.Keys.FirstOrDefault() : val;
            }
            set
            {
                SetAppConfigValue("WindowSizeProfile", value);
                WindowUtility.SetWindowSize(CurrentWindowSize.WindowBounds.Width, CurrentWindowSize.WindowBounds.Height);
            }
        }

        internal static WindowSizeProp CurrentWindowSize      { get => WindowSizeProfiles[CurrentWindowSizeName]; }
    }

    internal class WindowSizeProp
    {
        public Size                WindowBounds                 { get; init; }
        public GridLength          SidePanel1Width              { get; init; }
        public float               PostEventPanelScaleFactor    { get; init; }
        public Size                EventPostCarouselBounds      { get; init; }
        public Size                PostPanelBounds              { get; init; }
        public Thickness           PostPanelBottomMargin        { get; init; }
        public double              PostPanelPaimonTextSize      { get; init; }
        public int                 PostPanelPaimonHeight        { get; init; }
        public Thickness           PostPanelPaimonMargin        { get; init; }
        public Thickness           PostPanelPaimonInnerMargin   { get; init; }
        public Thickness           PostPanelPaimonTextMargin    { get; init; }
        public int                 BannerIconHeight             { get; init; }
        public int                 BannerIconHeightHYP          { get; init; }
        public Thickness           BannerIconMargin             { get; init; }
        public Thickness           BannerIconMarginHYP          { get; init; }
        public HorizontalAlignment BannerIconAlignHorizontal    { get; init; }
        public HorizontalAlignment BannerIconAlignHorizontalHYP { get; init; }
        public VerticalAlignment   BannerIconAlignVertical      { get; init; }
        public VerticalAlignment   BannerIconAlignVerticalHYP   { get; init; }
        public int                 SettingsPanelWidth           { get; init; }
    }
}
