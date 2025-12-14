using Microsoft.UI.Xaml.Controls;

namespace BackgroundTest.CustomControl.NewPipsPager;

public partial class NewPipsPager : Control
{
    public NewPipsPager()
    {
        Loaded += NewPipsPager_Loaded;
        Unloaded += NewPipsPager_Unloaded;

        DefaultStyleKey = typeof(NewPipsPager);
    }

    ~NewPipsPager()
    {
        Loaded -= NewPipsPager_Loaded;
        Unloaded -= NewPipsPager_Unloaded;
    }
}
