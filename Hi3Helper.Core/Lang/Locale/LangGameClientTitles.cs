using System.Collections.Generic;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region GameClientTitles
        public sealed partial class LocalizationParams
        {
            public Dictionary<string, string> _GameClientTitles { get; set; } = LangFallback?._GameClientTitles;
        }
        #endregion
    }
}
