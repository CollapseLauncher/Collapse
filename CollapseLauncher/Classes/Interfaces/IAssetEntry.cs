using System;
using System.Collections.ObjectModel;

namespace CollapseLauncher.Interfaces
{
    internal interface IAssetEntry<T> where T : Enum
    {
        ObservableCollection<IAssetProperty> AssetEntry { get; set; }
    }
}
