using Hi3Helper.Sophon;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.InstallManager.Zenless
{
    internal partial class ZenlessInstall
    {
        protected override async Task FilterSophonPatchAssetList(List<SophonAsset> itemList, CancellationToken token)
        {
            HashSet<int> exceptMatchFieldHashSet = await GetExceptMatchFieldHashSet(token);
            if (exceptMatchFieldHashSet.Count == 0)
            {
                return;
            }

            FilterSophonAsset(itemList, x => x, exceptMatchFieldHashSet, token);
        }
    }
}
