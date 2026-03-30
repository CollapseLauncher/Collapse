#nullable enable
using CollapseLauncher.Extension;
using Microsoft.UI.Xaml.Controls;

namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class NewPipsPager : Control
{
    public NewPipsPager()
    {
        DefaultStyleKey = typeof(NewPipsPager);
    }

    ~NewPipsPager()
    {
        UnapplyNavigationButtonEvents();
        UnapplyKeyPressEvents();
        UnapplyItemsRepeaterEvents();
    }
}
