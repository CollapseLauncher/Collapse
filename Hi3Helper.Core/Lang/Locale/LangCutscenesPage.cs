﻿namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region CutscenesPage
        public sealed partial class LocalizationParams
        {
            public LangCutscenesPage _CutscenesPage { get; set; } = LangFallback?._CutscenesPage;
            public sealed class LangCutscenesPage
            {
                public string PageTitle { get; set; } = LangFallback?._CutscenesPage.PageTitle;
            }
        }
        #endregion
    }
}
