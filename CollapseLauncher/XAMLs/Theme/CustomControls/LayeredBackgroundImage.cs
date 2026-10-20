using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#nullable enable
namespace CollapseLauncher.XAMLs.Theme.CustomControls;

public partial class LayeredBackgroundImage : Control
{
    public LayeredBackgroundImage()
    {
        DefaultStyleKey =  typeof(LayeredBackgroundImage);
        Loaded          += LayeredBackgroundImage_OnLoaded;
        Unloaded        += LayeredBackgroundImage_OnUnloaded;
    }

    private static void LayeredBackgroundImage_OnLoaded(object sender, RoutedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)sender;

        // Avoid to perform Subscribe and LoadAll calls before OnApplyTemplate getting called
        // first. The issue is that, due to Microsoft's design flaw, the OnApplyTemplate
        // didn't get called first (while it should've been) if the parent (which containing
        // this element) is not visible (due to Opacity = 0 or Visibility = Collapsed).
        // So, we need a bit of help here...
        // 
        // Let's just ignore this routine. If the said case happen, let OnApplyTemplate try
        // to call OnLoaded event once more.
        if (!instance._templateInitialized)
            return;

        // Gate it. Avoid the routine to be called twice.
        if (!Interlocked.Exchange(ref instance._requireOnLoadedCall, false))
            return;

        instance.SubscribeGridEvents();
        instance.LoadAllPresenters();
    }

    private static void LayeredBackgroundImage_OnUnloaded(object sender, RoutedEventArgs e)
    {
        LayeredBackgroundImage instance = (LayeredBackgroundImage)sender;

        // Same reason as above. If OnApplyTemplate not yet called, don't do anything.
        if (!instance._templateInitialized)
            return;

        instance.UnsubscribeGridEvents();
        instance.UnloadAllPresenters();
    }
}
