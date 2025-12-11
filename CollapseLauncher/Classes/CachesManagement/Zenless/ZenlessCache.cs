using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher
{
    internal partial class ZenlessCache(UIElement parentUI, IGameVersion gameVersionManager, IGameSettings gameSettings)
        : ZenlessRepair(parentUI, gameVersionManager, gameSettings, false, null, true), ICache, ICacheBase<ZenlessCache>
    {
        public ZenlessCache AsBaseType() => this;

        public async Task StartUpdateRoutine(bool showInteractivePrompt = false)
            => await StartRepairRoutine(showInteractivePrompt);
    }
}
