namespace Hi3Helper
{
    public static partial class Locale
    {
        #region CutscenesPage
        public partial class LocalizationParams
        {
            public LangCutscenesPage _CutscenesPage { get; set; } = LangFallback?._CutscenesPage;
            public class LangCutscenesPage
            {
                public string PageTitle { get; set; } = LangFallback?._CutscenesPage.PageTitle;
            }
        }
        #endregion
    }
}
