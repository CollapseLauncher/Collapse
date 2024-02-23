using System.Collections.ObjectModel;

namespace CollapseLauncher.Interfaces
{
    internal interface IAssetEntry
    {
        ObservableCollection<IAssetProperty> AssetEntry { get; set; }
    }
}
