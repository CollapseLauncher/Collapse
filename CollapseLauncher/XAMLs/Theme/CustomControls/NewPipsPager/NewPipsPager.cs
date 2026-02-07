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
        if (_pipsPagerScrollViewer == null!) return;
        _pipsPagerScrollViewer.Loaded   -= NewPipsPager_Loaded;
        _pipsPagerScrollViewer.Unloaded -= NewPipsPager_Unloaded;
    }
}
