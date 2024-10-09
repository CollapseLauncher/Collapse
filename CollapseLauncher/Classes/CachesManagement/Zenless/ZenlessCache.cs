using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace CollapseLauncher
{
    internal class ZenlessCache : ZenlessRepair, ICache, ICacheBase<ZenlessCache>
    {
        public ZenlessCache(UIElement parentUI, IGameVersionCheck gameVersionManager, ZenlessSettings gameSettings)
            : base(parentUI, gameVersionManager, gameSettings, false, null, true)
        { }

        public ZenlessCache AsBaseType() => this;

        public async Task StartUpdateRoutine(bool showInteractivePrompt = false)
            => await base.StartRepairRoutine(showInteractivePrompt);
    }
}
