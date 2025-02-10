using System.Drawing;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace CollapseLauncher.GameSettings.Base
{
    internal class BaseScreenSettingData
    {
        public virtual Size sizeRes { get; set; }
        public virtual string sizeResString { get; set; }
        public virtual int width { get; set; }
        public virtual int height { get; set; }
        public virtual bool isfullScreen { get; set; }
        public virtual void Save() { }
    }
}
