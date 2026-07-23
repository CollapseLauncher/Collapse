using CollapseLauncher.Helper.Image;
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

        public static void ChangeAccentColor(this FrameworkElement element, Color baseColor)
        {
            if (Application.Current.Resources.TryGetValue("AccentColor", out object? existing) &&
                existing is SolidColorBrush existingBrush &&
                existingBrush.Color.Equals(baseColor))
            {
                return;
            }

            Color accentColorMask        = baseColor;
            Color accentColorTransparent = baseColor;

            if (InnerLauncherConfig.IsAppThemeLight)
            {
                accentColorMask        = accentColorMask.SetSaturation(.3f).GetLightColor();
                accentColorTransparent = accentColorTransparent.SetSaturation(.3f).GetLightColor();
            }
            else
            {
                accentColorMask        = accentColorMask.GetDarkColor(.8f);
                accentColorTransparent = accentColorTransparent.GetDarkColor(.8f);
            }

            accentColorMask.A        = (byte)(InnerLauncherConfig.IsAppThemeLight ? 240 : 192);
            accentColorTransparent.A = 0;

            const string searchKeyAccentColor     = "AccentColor";
            const string searchKeyMain            = "System" + searchKeyAccentColor;
            const string searchKeyMask            = searchKeyMain + "BackgroundMask";
            const string searchKeyMaskTransparent = searchKeyMain + "BackgroundMaskTransparent";

            string searchReversedKey = CurrentReversedThemeKey;
            string searchKey1        = $"{searchKeyMain}{searchReversedKey}1";
            string searchKey2        = $"{searchKeyMain}{searchReversedKey}2";
            string searchKey3        = $"{searchKeyMain}{searchReversedKey}3";

            SetApplicationResource(searchKeyAccentColor,     new SolidColorBrush(baseColor));
            SetApplicationResource(searchKeyMain,            baseColor);
            SetApplicationResource(searchKey1,               baseColor);
            SetApplicationResource(searchKey2,               baseColor);
            SetApplicationResource(searchKey3,               baseColor);
            SetApplicationResource(searchKeyMask,            accentColorMask);
            SetApplicationResource(searchKeyMaskTransparent, accentColorTransparent);

            element.ReloadPageTheme();
        }

        internal static void ReloadPageTheme(this FrameworkElement page)
        {
            ElementTheme target = InnerLauncherConfig.CurrentAppTheme switch
                                 {
                                     AppThemeMode.Dark => ElementTheme.Dark,
                                     AppThemeMode.Light => ElementTheme.Light,
                                     _ => ElementTheme.Default
                                 };

            // Force the opposite theme first to ensure ThemeResource re-evaluation.
            ElementTheme opposite = target switch
            {
                ElementTheme.Dark  => ElementTheme.Light,
                ElementTheme.Light => ElementTheme.Dark,
                _                  => ElementTheme.Light
            };

            bool isComplete = false;
            while (!isComplete)
            {
                try
                {
                    page.RequestedTheme = opposite;
                    page.RequestedTheme = target;

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
