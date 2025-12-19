using CollapseLauncher.Interfaces;
using System.Drawing;
using System.Text.Json.Serialization;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.GameSettings.Base
{
    internal class BaseScreenSettingData(IGameSettings gameSettings)
    {
        [JsonIgnore]
        public IGameSettings ParentGameSettings { get; protected set; } = gameSettings;

        public virtual Size          sizeRes                          { get; set; }
        public virtual string        sizeResString                    { get; set; }
        public virtual int           width                            { get; set; }
        public virtual int           height                           { get; set; }
        public virtual bool          isfullScreen                     { get; set; }
        public virtual void          Save() {}
    }
}
