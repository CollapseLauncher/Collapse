using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region PluginManagementMenuPage
        public sealed partial class LocalizationParams
        {
            public LangPluginManagementMenuPage _PluginManagementMenuPage { get; set; } = LangFallback?._PluginManagementMenuPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangPluginManagementMenuPage
            {
                public string PageTitle { get; set; } = LangFallback?._PluginManagementMenuPage.PageTitle;
            }
        }
        #endregion
    }
}
