#nullable enable
    using CollapseLauncher.Extension;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media;
    using Windows.Foundation;
    using NColor = Windows.UI.Color;
// ReSharper disable UnusedMember.Global

    namespace CollapseLauncher.CustomControls
{
    // ReSharper disable once PartialTypeWithSinglePart
    public partial class ContentDialogOverlay : ContentDialog
    {
        private string             ThemeTitleGlyph { get; }
        private ContentDialogTheme Theme           { get; }
        public ContentDialogOverlay()
            : this(ContentDialogTheme.Informational) { }

        public ContentDialogOverlay(ContentDialogTheme theme = ContentDialogTheme.Warning)
        {
            Theme = theme;
            object brushObj = Theme switch
                              {
                                  ContentDialogTheme.Success => UIElementExtensions.GetApplicationResource<object>("SystemFillColorSuccessBrush"),
                                  ContentDialogTheme.Warning => UIElementExtensions.GetApplicationResource<object>("SystemFillColorCautionBrush"),
                                  ContentDialogTheme.Error => UIElementExtensions.GetApplicationResource<object>("SystemFillColorCriticalBrush"),
                                  _ => UIElementExtensions.GetApplicationResource<object>("SystemFillColorAttentionBrush")
                              };

            if (brushObj is SolidColorBrush brush)
            {
                NColor titleColor = brush.Color;
                titleColor.A = 255;

                if (UIElementExtensions.GetApplicationResource<object>("DialogTitleBrush") is SolidColorBrush brushTitle)
                    brushTitle.Color = titleColor;
            }

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
            if (Title is string titleString && Theme != ContentDialogTheme.Informational)
            {
                Grid titleStack = UIElementExtensions.CreateIconTextGrid(
                        text: titleString,
                        iconGlyph: ThemeTitleGlyph,
                        iconSize: 20,
                        iconFontFamily: "FontAwesomeSolid"
                    ).WithPadding(-8d, 0d, 0d, 0d);
                Title = titleStack;
            }
            return base.ShowAsync();
        }
    }
}
