using System.Collections.Generic;

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region WpfPackageName
        public sealed partial class LocalizationParams
        {
            public Dictionary<string, string> _WpfPackageName { get; set; } = LangFallback?._WpfPackageName;
        }
        #endregion
    }
}
