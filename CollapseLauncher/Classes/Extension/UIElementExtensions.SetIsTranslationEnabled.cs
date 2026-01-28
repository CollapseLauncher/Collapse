using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

#nullable enable
#pragma warning disable IDE0130
namespace CollapseLauncher.Extension;

public static partial class UIElementExtensions
{
    public static readonly DependencyProperty IsTranslationEnabledProperty =
        DependencyProperty.RegisterAttached("IsTranslationEnabled", typeof(bool),
                                            typeof(UIElement), new PropertyMetadata(false));

    public static bool GetIsTranslationEnabled(DependencyObject obj) => (bool)((UIElement)obj).GetValue(IsTranslationEnabledProperty);

    public static void SetIsTranslationEnabled(DependencyObject obj, bool value)
    {
        ElementCompositionPreview.SetIsTranslationEnabled((UIElement)obj, value);
        ((UIElement)obj).SetValue(IsTranslationEnabledProperty, value);
    }
}
