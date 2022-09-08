namespace Hi3Helper
{
    public static partial class Locale
    {
        #region UnhandledExceptionPage
        public partial class LocalizationParams
        {
            public LangUnhandledExceptionPage _UnhandledExceptionPage { get; set; } = LangFallback?._UnhandledExceptionPage;
            public class LangUnhandledExceptionPage
            {
                public string UnhandledTitle1 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle1;
                public string UnhandledSubtitle1 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle1;
                public string UnhandledTitle2 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle2;
                public string UnhandledSubtitle2 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle2;
                public string UnhandledTitle3 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledTitle3;
                public string UnhandledSubtitle3 { get; set; } = LangFallback?._UnhandledExceptionPage.UnhandledSubtitle3;
                public string CopyClipboardBtn1 { get; set; } = LangFallback?._UnhandledExceptionPage.CopyClipboardBtn1;
                public string CopyClipboardBtn2 { get; set; } = LangFallback?._UnhandledExceptionPage.CopyClipboardBtn2;
                public string GoBackPageBtn1 { get; set; } = LangFallback?._UnhandledExceptionPage.GoBackPageBtn1;
            }
        }
        #endregion
    }
}
