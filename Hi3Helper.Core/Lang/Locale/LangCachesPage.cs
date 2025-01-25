using WinRT;
// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace Hi3Helper
{
    public sealed partial class Locale
    {
        #region CachesPage
        public sealed partial class LocalizationParams
        {
            public LangCachesPage _CachesPage { get; set; } = LangFallback?._CachesPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangCachesPage
            {
                public string PageTitle { get; set; } = LangFallback?._CachesPage.PageTitle;
                public string ListCol1 { get; set; } = LangFallback?._CachesPage.ListCol1;
                public string ListCol2 { get; set; } = LangFallback?._CachesPage.ListCol2;
                public string ListCol3 { get; set; } = LangFallback?._CachesPage.ListCol3;
                public string ListCol4 { get; set; } = LangFallback?._CachesPage.ListCol4;
                public string ListCol5 { get; set; } = LangFallback?._CachesPage.ListCol5;
                public string ListCol6 { get; set; } = LangFallback?._CachesPage.ListCol6;
                public string ListCol7 { get; set; } = LangFallback?._CachesPage.ListCol7;
                public string Status1 { get; set; } = LangFallback?._CachesPage.Status1;
                public string Status2 { get; set; } = LangFallback?._CachesPage.Status2;
                public string CachesStatusHeader1 { get; set; } = LangFallback?._CachesPage.CachesStatusHeader1;
                public string CachesStatusCancelled { get; set; } = LangFallback?._CachesPage.CachesStatusCancelled;
                public string CachesStatusFetchingType { get; set; } = LangFallback?._CachesPage.CachesStatusFetchingType;
                public string CachesStatusNeedUpdate { get; set; } = LangFallback?._CachesPage.CachesStatusNeedUpdate;
                public string CachesStatusUpToDate { get; set; } = LangFallback?._CachesPage.CachesStatusUpToDate;
                public string CachesStatusChecking { get; set; } = LangFallback?._CachesPage.CachesStatusChecking;
                public string CachesTotalStatusNone { get; set; } = LangFallback?._CachesPage.CachesTotalStatusNone;
                public string CachesTotalStatusChecking { get; set; } = LangFallback?._CachesPage.CachesTotalStatusChecking;
                public string CachesBtn1 { get; set; } = LangFallback?._CachesPage.CachesBtn1;
                public string CachesBtn2 { get; set; } = LangFallback?._CachesPage.CachesBtn2;
                public string CachesBtn2Full { get; set; } = LangFallback?._CachesPage.CachesBtn2Full;
                public string CachesBtn2FullDesc { get; set; } = LangFallback?._CachesPage.CachesBtn2FullDesc;
                public string CachesBtn2Quick { get; set; } = LangFallback?._CachesPage.CachesBtn2Quick;
                public string CachesBtn2QuickDesc { get; set; } = LangFallback?._CachesPage.CachesBtn2QuickDesc;
                public string OverlayNotInstalledTitle { get; set; } = LangFallback?._CachesPage.OverlayNotInstalledTitle;
                public string OverlayNotInstalledSubtitle { get; set; } = LangFallback?._CachesPage.OverlayNotInstalledSubtitle;
                public string OverlayGameRunningTitle { get; set; } = LangFallback?._CachesPage.OverlayGameRunningTitle;
                public string OverlayGameRunningSubtitle { get; set; } = LangFallback?._CachesPage.OverlayGameRunningSubtitle;
            }
        }
        #endregion
    }
}
