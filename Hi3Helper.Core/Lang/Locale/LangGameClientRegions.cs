using System.Collections.Generic;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region GameClientRegions
        public sealed partial class LocalizationParams
        {
            public Dictionary<string, string> _GameClientRegions { get; set; } = LangFallback?._GameClientRegions;
        }
        #endregion
    }
}
