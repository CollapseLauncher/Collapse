﻿namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region GameRepairPage
        public sealed partial class LocalizationParams
        {
            public LangDisconnectedPage _DisconnectedPage { get; set; } = LangFallback?._DisconnectedPage;
            public sealed class LangDisconnectedPage
            {
                public string PageTitle { get; set; } = LangFallback?._DisconnectedPage.PageTitle;
                public string Header1 { get; set; } = LangFallback?._DisconnectedPage.Header1;
                public string Header2 { get; set; } = LangFallback?._DisconnectedPage.Header2;
                public string Footer1 { get; set; } = LangFallback?._DisconnectedPage.Footer1;
                public string Footer2 { get; set; } = LangFallback?._DisconnectedPage.Footer2;
                public string Footer3 { get; set; } = LangFallback?._DisconnectedPage.Footer3;
                public string ShowErrorBtn { get; set; } = LangFallback?._DisconnectedPage.ShowErrorBtn;
            }
        }
        #endregion
    }
}
