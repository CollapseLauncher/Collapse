using CollapseLauncher.Helper.Metadata;
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo
// ReSharper disable CheckNamespace

#nullable enable
namespace CollapseLauncher.GameManagement.Versioning
{
    internal partial class GameVersionBase
    {
        #region Game Config Properties
        public virtual string         GameName       { get; }
        public virtual string         GameRegion     { get; }
        public virtual string         GameBiz        { get; }
        public virtual string         GameId         { get; }
        public virtual GameVendorProp VendorTypeProp { get; private set; }
        public virtual GameNameType   GameType
        {
            get => GamePreset.GameType;
        }

        public virtual PresetConfig GamePreset { get; }
        #endregion
    }
}