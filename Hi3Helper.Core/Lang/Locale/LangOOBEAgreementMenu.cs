using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region Misc
        public sealed partial class LocalizationParams
        {
            public LangOOBEAgreementMenu _OOBEAgreementMenu { get; set; } = LangFallback?._OOBEAgreementMenu;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangOOBEAgreementMenu
            {
                public string AgreementTitle { get; set; } = LangFallback?._OOBEAgreementMenu.AgreementTitle;
            }
        }
        #endregion
    }
}
