using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.Extension
{
    public static partial class UIElementExtensions
    {
        private static string CurrentReversedThemeKey => InnerLauncherConfig.IsAppThemeLight ? "Dark" : "Light";

        public static void ChangeAccentColor(this FrameworkElement element, Color accentColor, Color accentColorMask, Color accentColorTransparent)
        {
            const string searchKeyAccentColor     = "AccentColor";
            const string searchKeyMain            = "System" + searchKeyAccentColor;
            const string searchKeyMask            = searchKeyMain + "BackgroundMask";
            const string searchKeyMaskTransparent = searchKeyMain + "BackgroundMaskTransparent";

            string searchReversedKey = CurrentReversedThemeKey;
            string searchKey1        = $"{searchKeyMain}{searchReversedKey}1";
            string searchKey2        = $"{searchKeyMain}{searchReversedKey}2";
            string searchKey3        = $"{searchKeyMain}{searchReversedKey}3";

            SetApplicationResource(searchKeyAccentColor,     new SolidColorBrush(accentColor));
            SetApplicationResource(searchKeyMain,            accentColor);
            SetApplicationResource(searchKey1,               accentColor);
            SetApplicationResource(searchKey2,               accentColor);
            SetApplicationResource(searchKey3,               accentColor);
            SetApplicationResource(searchKeyMask,            accentColorMask);
            SetApplicationResource(searchKeyMaskTransparent, accentColorTransparent);

            element.ReloadPageTheme();
        }

        internal static void ReloadPageTheme(this FrameworkElement page)
        {
            ElementTheme theme = InnerLauncherConfig.CurrentAppTheme switch
                                 {
                                     AppThemeMode.Dark => ElementTheme.Dark,
                                     AppThemeMode.Light => ElementTheme.Light,
                                     _ => ElementTheme.Default
                                 };

            bool isComplete = false;
            while (!isComplete)
            {
                try
                {
                    page.RequestedTheme = page.RequestedTheme switch
                                          {
                                              ElementTheme.Dark => ElementTheme.Light,
                                              ElementTheme.Light => ElementTheme.Default,
                                              ElementTheme.Default => ElementTheme.Dark,
                                              _ => page.RequestedTheme
                                          };

                    if (page.RequestedTheme != theme)
                    {
                        ReloadPageTheme(page);
                    }

                    isComplete = true;
                }
                catch
                {
                    // ignored
                }
            }
        }
    }
}
