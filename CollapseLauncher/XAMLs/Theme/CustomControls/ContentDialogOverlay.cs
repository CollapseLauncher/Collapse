// using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using NColor = Windows.UI.Color;

namespace CollapseLauncher.CustomControls
{
    public class ContentDialogOverlay : ContentDialog
    {
        public string ThemeTitleGlyph { get; set; }
        public ContentDialogTheme Theme { get; set; }
        public ContentDialogOverlay()
            : this(ContentDialogTheme.Warning) { }

        public ContentDialogOverlay(ContentDialogTheme theme = ContentDialogTheme.Warning)
            : base()
        {
            Theme = theme;
            NColor titleColor = (Theme switch
            {
                ContentDialogTheme.Success => Application.Current.Resources["SystemFillColorSuccessBrush"],
                ContentDialogTheme.Warning => Application.Current.Resources["SystemFillColorCautionBrush"],
                ContentDialogTheme.Error => Application.Current.Resources["SystemFillColorCriticalBrush"],
                _ => Application.Current.Resources["SystemFillColorAttentionBrush"]
            } as SolidColorBrush).Color;
            titleColor.A = 255;
            (Application.Current.Resources["DialogTitleBrush"] as SolidColorBrush).Color = titleColor;

            ThemeTitleGlyph = Theme switch
            {
                ContentDialogTheme.Success => "",
                ContentDialogTheme.Warning => "",
                ContentDialogTheme.Error => "",
                _ => ""
            };
        }

        public new IAsyncOperation<ContentDialogResult> ShowAsync()
        {
            if (Title != null && Title.GetType() == typeof(string) && Theme != ContentDialogTheme.Informational)
            {
                StackPanel titleStack = new StackPanel { Orientation = Orientation.Horizontal };
                titleStack.Children.Add(new FontIcon
                {
                    Glyph = ThemeTitleGlyph,
                    Foreground = (SolidColorBrush)Application.Current.Resources["DefaultFGColorAccentBrush"],
                    Margin = new Thickness(0, 0, 10, 0),
                    FontFamily = (FontFamily)Application.Current.Resources["FontAwesomeSolid"],
                    FontSize = 22
                });
                titleStack.Children.Add(new TextBlock { Text = (string)Title, Margin = new Thickness(0, -1, 0, 0) });
                Title = titleStack;
            }
            return base.ShowAsync();
        }
    }
}
