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
            public LangPluginManagerPage _PluginManagerPage { get; set; } = LangFallback?._PluginManagerPage;

            [GeneratedBindableCustomProperty]
            public sealed partial class LangPluginManagerPage
            {
                public string PageTitle { get; set; } = LangFallback?._PluginManagerPage.PageTitle;

                public string FileDialogTitle { get; set; } = LangFallback?._PluginManagerPage.FileDialogTitle;
                public string FileDialogFileFilter1 { get; set; } = LangFallback?._PluginManagerPage.FileDialogFileFilter1;

                public string LeftPanelNoPluginTitle { get; set; } = LangFallback?._PluginManagerPage.LeftPanelNoPluginTitle;
                public string LeftPanelNoPluginButton1 { get; set; } = LangFallback?._PluginManagerPage.LeftPanelNoPluginButton1;
                public string LeftPanelNoPluginButton2 { get; set; } = LangFallback?._PluginManagerPage.LeftPanelNoPluginButton2;
                public string LeftPanelNoPluginButton3 { get; set; } = LangFallback?._PluginManagerPage.LeftPanelNoPluginButton3;

                public string LeftPanelListViewTitle { get; set; } = LangFallback?._PluginManagerPage.LeftPanelListViewTitle;
                public string ListViewMainActionButton1 { get; set; } = LangFallback?._PluginManagerPage.ListViewMainActionButton1;
                public string ListViewMainActionButton1AltChecking { get; set; } = LangFallback?._PluginManagerPage.ListViewMainActionButton1AltChecking;
                public string ListViewMainActionButton2 { get; set; } = LangFallback?._PluginManagerPage.ListViewMainActionButton2;
                public string ListViewMainActionButton3 { get; set; } = LangFallback?._PluginManagerPage.ListViewMainActionButton3;

                public string ListViewItemContextButton1 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton1;
                public string ListViewItemContextButton2 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton2;
                public string ListViewItemContextButton3 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton3;
                public string ListViewItemContextButton4 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton4;
                public string ListViewItemContextButton5 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton5;
                public string ListViewItemContextButton6 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton6;
                public string ListViewItemContextButton7 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton7;
                public string ListViewItemContextButton8 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButton8;

                public string ListViewItemContentButton1 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContentButton1;
                public string ListViewItemContentButton2 { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContentButton2;

                public string ListViewItemContextButtonCheckUpdateOnly { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButtonCheckUpdateOnly;
                public string ListViewItemContextButtonCheckAndDownloadUpdate { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButtonCheckAndDownloadUpdate;
                public string ListViewItemContextButtonDownloadUpdate { get; set; } = LangFallback?._PluginManagerPage.ListViewItemContextButtonDownloadUpdate;

                public string ListViewItemUpdateStatusAvailable { get; set; } = LangFallback?._PluginManagerPage.ListViewItemUpdateStatusAvailable;
                public string ListViewItemUpdateStatusAvailableButton { get; set; } = LangFallback?._PluginManagerPage.ListViewItemUpdateStatusAvailableButton;
                public string ListViewItemUpdateStatusAvailableButtonUpdating { get; set; } = LangFallback?._PluginManagerPage.ListViewItemUpdateStatusAvailableButtonUpdating;
                public string ListViewItemUpdateStatusCompleted { get; set; } = LangFallback?._PluginManagerPage.ListViewItemUpdateStatusCompleted;
                public string ListViewItemUpdateStatusChecking { get; set; } = LangFallback?._PluginManagerPage.ListViewItemUpdateStatusChecking;
                public string ListViewItemUpdateStatusUpToDate { get; set; } = LangFallback?._PluginManagerPage.ListViewItemUpdateStatusUpToDate;

                public string ListViewFooterWarning1 { get; set; } = LangFallback?._PluginManagerPage.ListViewFooterWarning1;
                public string ListViewFooterRestartButton { get; set; } = LangFallback?._PluginManagerPage.ListViewFooterRestartButton;

                public string RightPanelImportTitle1 { get; set; } = LangFallback?._PluginManagerPage.RightPanelImportTitle1;
                public string RightPanelImportTitle2 { get; set; } = LangFallback?._PluginManagerPage.RightPanelImportTitle2;
                public string RightPanelImportTitle3 { get; set; } = LangFallback?._PluginManagerPage.RightPanelImportTitle3;
                public string RightPanelImportTitle4 { get; set; } = LangFallback?._PluginManagerPage.RightPanelImportTitle4;
            }
        }
        #endregion
    }
}
