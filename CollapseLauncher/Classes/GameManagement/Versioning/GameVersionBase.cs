using CollapseLauncher.Interfaces;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase : IGameVersion
    {
        protected GameVersionBase(RegionResourceProp? gameRegionProp, string? gameName, string? gameRegion)
        {
            // ReSharper disable VirtualMemberCallInConstructor
            GameApiProp = gameRegionProp;
            GameName    = gameName;
            GameRegion  = gameRegion;
            // ReSharper enable VirtualMemberCallInConstructor

            // Initialize INI Prop ahead of other operations
            // ReSharper disable once VirtualMemberCallInConstructor
            InitializeIniProp();
        }

        protected GameVersionBase() { }
    }
}