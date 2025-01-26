using CollapseLauncher.Interfaces;
using Microsoft.UI.Xaml;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase : IGameVersion
    {
        #region Properties
        protected UIElement ParentUIElement { get; set; }
        #endregion

        protected GameVersionBase(UIElement parentUIElement, RegionResourceProp gameRegionProp, string gameName, string gameRegion)
        {
            ParentUIElement = parentUIElement;
            GameApiProp     = gameRegionProp;
            GameName        = gameName;
            GameRegion      = gameRegion;

            // Initialize INI Prop ahead of other operations
            // ReSharper disable once VirtualMemberCallInConstructor
            InitializeIniProp();
        }
    }
}