using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region UnhandledExceptionPage
        public sealed partial class LocalizationParams
        {
            public LangUnhandledExceptionPage _UnhandledExceptionPage { get; set; } = LangFallback?._UnhandledExceptionPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangUnhandledExceptionPage
            {
                public string UnhandledTitle1 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle1;
                public string UnhandledSubtitle1 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle1;
                public string UnhandledTitle2 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle2;
                public string UnhandledSubtitle2 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle2;
                public string UnhandledTitle3 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle3;
                public string UnhandledSubtitle3 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle3;
                public string UnhandledTitle4 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle4;
                public string UnhandledSubtitle4 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle4;
                public string UnhandledTitleDiskCrc { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitleDiskCrc;
                public string UnhandledSubDiskCrc { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubDiskCrc;
                public string CopyClipboardBtn1 { get; set; } = LangFallback?._UnhandledExceptionPage.CopyClipboardBtn1;
                public string CopyClipboardBtn2 { get; set; } = LangFallback?._UnhandledExceptionPage.CopyClipboardBtn2;
                public string GoBackPageBtn1 { get; set; } = LangFallback?._UnhandledExceptionPage.GoBackPageBtn1;
            }
        }
        #endregion
    }
}
