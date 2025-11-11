using CollapseLauncher.Helper.LauncherApiLoader.HoYoPlay;
using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
using WinRT;
#pragma warning disable IDE0130

#nullable enable
namespace CollapseLauncher.InstallManagement;

[GeneratedBindableCustomProperty]
internal partial class HypWpfManager : ProgressBase
{
    protected HypWpfManager(UIElement    parentUI,
                            IGameVersion gameVersionManager)
        : base(parentUI,
               gameVersionManager,
               null,
               null,
               null)
    {
    }

    public override string GamePath
    {
        get => GameVersionManager.GameDirPath;
        set => GameVersionManager.GameDirPath = value;
    }

    public HypPackageData? WpfPackage
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            string gameId  = GameVersionManager?.GameId ?? "";
            string gameBiz = GameVersionManager?.GameBiz ?? "";

            if (!(GameVersionManager?
                 .LauncherApi
                 .LauncherGameResourceWpf?
                 .Data?
                 .TryFindByBizOrId(gameId, gameBiz, out HypWpfPackageData? packageData) ?? false))
            {
                return field;
            }

            field = packageData.PackageInfo;
            return field;
        }
    }

    public bool IsWpfPackageAvailable
    {
        get => WpfPackage != null;
    }

    public bool IsDownloadInProgress
    {
        get;
        set
        {
            OnPropertyChanged();
            field = value;
        }
    }
}
