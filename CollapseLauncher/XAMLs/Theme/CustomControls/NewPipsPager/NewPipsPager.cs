using CollapseLauncher.Extension;
using Microsoft.UI.Xaml.Controls;

#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls.NewPipsPager;

public partial class NewPipsPager : Control
{
    public NewPipsPager()
    {
        DefaultStyleKey = typeof(NewPipsPager);
    }

    ~NewPipsPager()
    {
        if (!this.IsObjectDisposed())
        {
            Loaded   -= NewPipsPager_Loaded;
            Unloaded -= NewPipsPager_Unloaded;
        }

        UnapplyNavigationButtonEvents();
        UnapplyKeyPressEvents();
        UnapplyItemsRepeaterEvents();
    }
}
