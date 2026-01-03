using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using System.Threading.Tasks;
// ReSharper disable UnusedMember.Global

#pragma warning disable IDE0290 // Shut the fuck up
#pragma warning disable IDE0130
#nullable enable

namespace CollapseLauncher
{
    internal partial class StarRailCacheV2(UIElement parentUI, IGameVersion gameVersionManager, IGameSettings gameSettings)
        : StarRailRepairV2(parentUI, gameVersionManager, gameSettings, false, null, true), ICache, ICacheBase<StarRailCacheV2>
    {
        public StarRailCacheV2 AsBaseType() => this;

        public Task StartUpdateRoutine(bool showInteractivePrompt = false)
            => StartRepairRoutine(showInteractivePrompt);
    }
}
